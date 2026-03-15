#!/usr/bin/env python3
"""
가중치 역산 스크립트 (Weight Backtesting)

과거 데이터의 실제 1위 빈도(win_rate)와 3개 요소 점수 간의
Spearman 상관계수를 최대화하는 가중치 조합을 탐색한다.

모든 계산은 기초대비사정률 기준.
"""

import pandas as pd
import numpy as np
import json
from pathlib import Path
from datetime import datetime, timedelta
from collections import defaultdict
import sys

try:
    from scipy.stats import spearmanr, gaussian_kde
    HAS_SCIPY = True
except ImportError:
    HAS_SCIPY = False

# 경로 설정
BASE_DIR = Path(__file__).resolve().parents[4]
if str(BASE_DIR) not in sys.path:
    sys.path.insert(0, str(BASE_DIR))

from nara_workspace_paths import analysis_dir, master_path

DATA_PATH = master_path()
OUTPUT_DIR = analysis_dir() / "optimal-position"

# 분석 범위: 기초대비사정률 99.5~100.0%
MIN_RATE = 99.5
MAX_RATE = 100.0
ZONE_SIZE = 0.001
NUM_ZONES = int((MAX_RATE - MIN_RATE) / ZONE_SIZE)  # 500

MY_COMPANY = "동국문화재연구원"

PERIODS = {
    '2y': timedelta(days=730),
    '1y': timedelta(days=365),
    '6m': timedelta(days=180),
    '3m': timedelta(days=90),
}
PERIOD_ORDER = ['2y', '1y', '6m', '3m']


def load_data():
    """전체 데이터 로드 (기초대비사정률 필터, 내 업체 제외)"""
    df = pd.read_excel(DATA_PATH)
    df = df[(df['기초대비사정률'] >= MIN_RATE) & (df['기초대비사정률'] <= MAX_RATE)]
    df['투찰일시'] = pd.to_datetime(df['투찰일시'], errors='coerce')
    df = df[df['업체명'] != MY_COMPANY]
    return df


def build_position_data(df):
    """위치별 데이터 구축 (기초대비사정률 기준, 0.001% 단위)"""
    position_data = {}
    for i in range(NUM_ZONES):
        position = round(MIN_RATE + i * ZONE_SIZE, 3)
        position_data[position] = {'total': 0, 'wins': 0, 'companies': set()}

    for _, row in df.iterrows():
        rate = row['기초대비사정률']
        position = round(round(rate, 3), 3)
        if position not in position_data:
            continue
        position_data[position]['total'] += 1
        position_data[position]['companies'].add(row['업체명'])
        if row['순위'] == 1:
            position_data[position]['wins'] += 1

    return position_data


def compute_announcement_rates(df):
    """공고별 기초대비사정률 사전 계산"""
    ann_rates = {}
    for ann_id in df['공고번호'].unique():
        ann_df = df[df['공고번호'] == ann_id]
        rates = ann_df['기초대비사정률'].values
        rates = rates[(rates >= MIN_RATE) & (rates <= MAX_RATE)]
        ann_rates[ann_id] = np.sort(rates)
    return ann_rates


def calculate_left_gap(position, ann_rates):
    """공고별 왼쪽 빈 공간 → 중앙값 (기초대비사정률 기준)"""
    gaps = []
    for rates in ann_rates.values():
        left = rates[rates < position]
        if len(left) > 0:
            gaps.append(position - left[-1])
        else:
            gaps.append(position - MIN_RATE)
    return np.median(gaps) if gaps else 0


def compute_avg_gap(ann_rates):
    """공고별 투찰 간격의 중앙값"""
    all_gaps = []
    for rates in ann_rates.values():
        if len(rates) >= 2:
            all_gaps.extend(np.diff(rates))
    return np.median(all_gaps) if all_gaps else 0.01


def compute_company_movements(df):
    """업체별 이동 분석 (기초대비사정률 기준)"""
    now = datetime.now()
    company_timeline = defaultdict(dict)

    for period_name, delta in PERIODS.items():
        cutoff = now - delta
        period_df = df[df['투찰일시'] >= cutoff]
        for company in period_df['업체명'].unique():
            c_df = period_df[period_df['업체명'] == company]
            if len(c_df) > 0:
                company_timeline[company][period_name] = {
                    'avg_position': c_df['기초대비사정률'].mean(),
                    'min': c_df['기초대비사정률'].min(),
                    'std': c_df['기초대비사정률'].std() if len(c_df) > 1 else 0,
                    'count': len(c_df)
                }

    movements = {}
    for company, timeline in company_timeline.items():
        periods_with_data = [p for p in PERIOD_ORDER if p in timeline]
        if len(periods_with_data) < 2:
            continue
        newest_period = periods_with_data[-1]
        movements[company] = {
            'newest_min': timeline[newest_period]['min'],
            'newest_position': timeline[newest_period]['avg_position'],
            'newest_std': timeline[newest_period].get('std', 0),
            'total_bids': sum(timeline[p]['count'] for p in timeline)
        }

    return movements


def calculate_scores(position_data, ann_rates, movements):
    """각 위치별 3개 요소 점수 계산 (기초대비사정률 기준)"""
    positions = sorted(position_data.keys())
    avg_gap = max(compute_avg_gap(ann_rates), 0.001)

    # 경쟁밀도: 주변 ±0.01% bin counting → 백분위수
    all_nearby = {}
    for pos in positions:
        nearby = 0
        for j in range(-10, 11):
            check_pos = round(pos + j * ZONE_SIZE, 3)
            nearby += position_data.get(check_pos, {}).get('total', 0)
        all_nearby[pos] = nearby

    nearby_values = sorted(all_nearby.values())
    num_positions = len(nearby_values)

    scores = {}
    for pos in positions:
        # 경쟁밀도 점수 (백분위수)
        nearby_total = all_nearby[pos]
        rank = sum(1 for x in nearby_values if x <= nearby_total)
        percentile = rank / num_positions if num_positions > 0 else 0.5
        comp_score = (1 - percentile) * 100

        # 왼쪽빈공간 점수
        left_gap = calculate_left_gap(pos, ann_rates)
        gap_score = min(100, (left_gap / avg_gap) * 50)

        # 가림막 점수
        shadow_score = 0
        for company, mov in movements.items():
            safe_pos = mov.get('newest_min', mov['newest_position'])
            distance = safe_pos - pos
            if 0.001 <= distance <= 0.02:
                base = min(100, mov['total_bids'] * 2)
                decay = np.exp(-69.3 * (distance - 0.001))
                consistency = max(0, 1 - mov.get('newest_std', 0) * 100)
                s = base * decay * (0.7 + 0.3 * consistency)
                if s > shadow_score:
                    shadow_score = s

        scores[pos] = {
            'competition': comp_score,
            'left_gap': gap_score,
            'shadow': shadow_score
        }

    return scores


def smooth_win_rates(position_data):
    """승률 KDE 평활화 (기초대비사정률 기준)"""
    positions = []
    win_rates = []
    weights = []

    for pos in sorted(position_data.keys()):
        data = position_data[pos]
        if data['total'] >= 1:
            positions.append(pos)
            win_rates.append(data['wins'] / data['total'])
            weights.append(data['total'])

    if not HAS_SCIPY or len(positions) < 20:
        return dict(zip(positions, win_rates))

    pos_arr = np.array(positions)
    wr_arr = np.array(win_rates)
    weight_arr = np.array(weights, dtype=float)

    try:
        # 가중 KDE: 데이터 많은 구간의 승률에 더 높은 신뢰도
        kde = gaussian_kde(pos_arr, weights=weight_arr, bw_method='silverman')
        density = kde(pos_arr)

        # 승률 가중 KDE
        kde_wr = gaussian_kde(pos_arr, weights=wr_arr * weight_arr + 1e-10, bw_method='silverman')
        density_wr = kde_wr(pos_arr)

        # 평활화된 승률 = 승률 가중 밀도 / 밀도
        smoothed = np.where(density > 1e-10, density_wr / density, 0)

        return dict(zip(positions, smoothed))
    except Exception:
        return dict(zip(positions, win_rates))


def grid_search(scores, win_rates):
    """Grid search로 최적 가중치 탐색 (Spearman 상관계수 최대화)"""
    if not HAS_SCIPY:
        print("  scipy 미설치: Spearman 상관계수 계산 불가")
        return {
            'w_competition': 0.50, 'w_left_gap': 0.30, 'w_shadow': 0.20,
            'correlation': 0, 'baseline_correlation': 0
        }

    # 공통 위치 (데이터 있는 위치만)
    common_positions = sorted(set(scores.keys()) & set(win_rates.keys()))
    if len(common_positions) < 10:
        print(f"  공통 위치 부족: {len(common_positions)}개")
        return {
            'w_competition': 0.50, 'w_left_gap': 0.30, 'w_shadow': 0.20,
            'correlation': 0, 'baseline_correlation': 0
        }

    comp_arr = np.array([scores[p]['competition'] for p in common_positions])
    gap_arr = np.array([scores[p]['left_gap'] for p in common_positions])
    shadow_arr = np.array([scores[p]['shadow'] for p in common_positions])
    wr_arr = np.array([win_rates[p] for p in common_positions])

    best_corr = -1
    best_weights = (0.50, 0.30, 0.20)

    # Grid search: 0.05 단위, w1+w2+w3=1.0
    steps = [round(x * 0.05, 2) for x in range(1, 19)]  # 0.05 ~ 0.90

    for w1 in steps:
        for w2 in steps:
            w3 = round(1.0 - w1 - w2, 2)
            if w3 < 0.05 or w3 > 0.90:
                continue

            total_scores = w1 * comp_arr + w2 * gap_arr + w3 * shadow_arr
            corr, _ = spearmanr(total_scores, wr_arr)

            if not np.isnan(corr) and corr > best_corr:
                best_corr = corr
                best_weights = (w1, w2, w3)

    # 기본 50-30-20 상관계수
    baseline_scores = 0.50 * comp_arr + 0.30 * gap_arr + 0.20 * shadow_arr
    baseline_corr, _ = spearmanr(baseline_scores, wr_arr)

    return {
        'w_competition': best_weights[0],
        'w_left_gap': best_weights[1],
        'w_shadow': best_weights[2],
        'correlation': round(best_corr, 4),
        'baseline_correlation': round(baseline_corr, 4) if not np.isnan(baseline_corr) else 0
    }


def cross_validate(df):
    """시간 분할 교차검증: 전반 1년 vs 후반 1년"""
    now = datetime.now()
    mid = now - timedelta(days=365)
    cutoff_2y = now - timedelta(days=730)

    first_half = df[(df['투찰일시'] >= cutoff_2y) & (df['투찰일시'] < mid)]
    second_half = df[df['투찰일시'] >= mid]

    results = {}
    for name, half_df in [('first_half', first_half), ('second_half', second_half)]:
        label = "전반 1년" if name == "first_half" else "후반 1년"

        if len(half_df) < 100:
            print(f"  {label}: 데이터 부족 ({len(half_df)}건)")
            results[name] = {
                'w_competition': 0.50, 'w_left_gap': 0.30, 'w_shadow': 0.20,
                'correlation': 0
            }
            continue

        print(f"  {label}: {len(half_df)}건 분석 중...")
        pos_data = build_position_data(half_df)
        ann_rates = compute_announcement_rates(half_df)
        movements = compute_company_movements(half_df)
        scores = calculate_scores(pos_data, ann_rates, movements)
        win_rates = smooth_win_rates(pos_data)
        result = grid_search(scores, win_rates)
        results[name] = result
        print(f"  {label}: {result['w_competition']:.0%}-{result['w_left_gap']:.0%}-{result['w_shadow']:.0%} (상관계수: {result['correlation']:.4f})")

    return results


def main():
    print("=" * 60)
    print("가중치 역산 (Weight Backtesting)")
    print("기초대비사정률 기준 — Spearman 상관계수 최대화")
    print("=" * 60)

    if not HAS_SCIPY:
        print("\n[경고] scipy 미설치 — pip install scipy 후 재실행")
        print("기본 가중치 50-30-20을 유지합니다.")
        return

    # 1. 데이터 로드
    print("\n[1/4] 데이터 로드...")
    df = load_data()
    print(f"  - 전체: {len(df)}건, {df['공고번호'].nunique()}개 공고")

    # 2. 전체 데이터로 최적 가중치 탐색
    print("\n[2/4] 전체 데이터 최적 가중치 탐색...")
    pos_data = build_position_data(df)
    ann_rates = compute_announcement_rates(df)
    movements = compute_company_movements(df)
    scores = calculate_scores(pos_data, ann_rates, movements)
    win_rates = smooth_win_rates(pos_data)

    filled = sum(1 for d in pos_data.values() if d['total'] > 0)
    print(f"  - 데이터 있는 구간: {filled}/{NUM_ZONES}")
    print(f"  - 승률 데이터: {len(win_rates)}개 위치")

    result = grid_search(scores, win_rates)

    print(f"\n  현재 가중치 (50-30-20) 상관계수: {result['baseline_correlation']:.4f}")
    print(f"  최적 가중치 ({result['w_competition']:.0%}-{result['w_left_gap']:.0%}-{result['w_shadow']:.0%}) 상관계수: {result['correlation']:.4f}")

    if result['baseline_correlation'] != 0:
        improvement = (result['correlation'] - result['baseline_correlation']) / abs(result['baseline_correlation']) * 100
        print(f"  개선율: {improvement:+.1f}%")

    # 3. 교차검증
    print("\n[3/4] 교차검증 (전반 1년 vs 후반 1년)...")
    cv = cross_validate(df)
    result['cross_validation'] = cv

    # 4. 저장
    print("\n[4/4] 저장...")
    OUTPUT_DIR.mkdir(exist_ok=True)
    output_path = OUTPUT_DIR / "optimal_weights.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"  - 저장: {output_path}")
    print("\n완료!")


if __name__ == "__main__":
    main()
