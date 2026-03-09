#!/usr/bin/env python3
"""
최적 투찰 위치 분석기 (Optimal Position Analyzer)

전체 시계열 분석 (2년→1년→6개월→3개월→1개월)
- 각 업체의 투찰 위치 이동 패턴 추적
- 경쟁업체 분석
- 1개월 그래프에 최적 투찰 위치 화살표 표시

원칙:
- 모든 분석은 실제 데이터에 기반
- 하드코딩 없음, 오직 데이터로만 계산
- 기존 스킬 결과 활용
"""

import matplotlib
matplotlib.use('Agg')
import logging
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
import matplotlib.patheffects as patheffects
import json
import argparse
from pathlib import Path
from datetime import datetime, timedelta
from collections import defaultdict

try:
    from scipy.stats import gaussian_kde
    HAS_SCIPY = True
except ImportError:
    HAS_SCIPY = False

# 한글 폰트 설정
plt.rcParams['font.family'] = ['Noto Sans CJK JP', 'NanumGothic', 'Malgun Gothic', 'sans-serif']
plt.rcParams['axes.unicode_minus'] = False
logging.getLogger('matplotlib.font_manager').setLevel(logging.ERROR)

# 경로 설정
BASE_DIR = Path(__file__).resolve().parents[4]
DATA_PATH = BASE_DIR / "전체_통합_데이터.xlsx"
COLOR_MAP_PATH = BASE_DIR / ".agents" / "skills" / "fixed-bidder-map" / "bidder_colors.json"
SHADOW_STRATEGY_PATH = BASE_DIR / ".agents" / "skills" / "shadow-strategy" / "output"
OUTPUT_DIR = BASE_DIR / "console-workspace" / "analysis" / "optimal-position"

# 분석 기간 정의
PERIODS = {
    '2y': timedelta(days=730),
    '1y': timedelta(days=365),
    '6m': timedelta(days=180),
    '3m': timedelta(days=90),
    '1m': timedelta(days=30),
}

PERIOD_ORDER = ['2y', '1y', '6m', '3m', '1m']

# 분석 범위: 99.500% ~ 100.000% (0.001% 단위)
MIN_RATE = 99.5
MAX_RATE = 100.0
ZONE_SIZE = 0.001
NUM_ZONES = int((MAX_RATE - MIN_RATE) / ZONE_SIZE)  # 500

# 사용자 업체 (분석에서 제외)
MY_COMPANY = "동국문화재연구원"


class OptimalPositionAnalyzer:
    """최적 투찰 위치 분석기 - 전체 시계열 분석"""

    def __init__(self, top_n=5, create_image=False):
        self.top_n = top_n
        self.create_image = create_image
        self.df_full = None  # 전체 데이터 (2년)
        self.color_map = {}

        # 기간별 데이터
        self.period_data = {}  # {period: df}
        self.period_stats = {}  # {period: {announcements, companies, bids, wins}}

        # 업체별 시계열 분석
        self.company_timeline = {}  # {company: {period: avg_position}}
        self.company_movement = {}  # {company: {direction, oldest_position, newest_position, total_bids}}

        # 위치별 분석
        self.position_data = {}
        self.position_scores = {}

        # 최종 추천
        self.recommendations = []

        # 게임이론 분석
        self.strategic_stability = {}
        self.company_strategy_profiles = {}
        self.next_bid_forecast = {}

        # 1개월 데이터 범위
        self.date_min_1m = None
        self.date_max_1m = None

    def load_data(self):
        """전체 데이터 로드 (2년)"""
        print("=" * 60)
        print("최적 투찰 위치 분석기")
        print("전체 시계열 분석 (2년→1년→6개월→3개월→1개월)")
        print("=" * 60)

        print("\n[1/7] 데이터 로드...")

        # 색상 맵 로드
        if COLOR_MAP_PATH.exists():
            with open(COLOR_MAP_PATH, 'r', encoding='utf-8') as f:
                self.color_map = json.load(f)
            print(f"  - 색상맵: {len(self.color_map)}개 업체")

        # 전체 데이터 로드
        df = pd.read_excel(DATA_PATH)
        df = df[(df['기초대비사정률'] >= MIN_RATE) & (df['기초대비사정률'] <= MAX_RATE)]
        df['투찰일시'] = pd.to_datetime(df['투찰일시'], errors='coerce')

        self.df_full = df

        # 기간별 데이터 분리
        now = datetime.now()
        for period, delta in PERIODS.items():
            cutoff = now - delta
            period_df = df[df['투찰일시'] >= cutoff].copy()
            self.period_data[period] = period_df

            self.period_stats[period] = {
                'announcements': period_df['공고번호'].nunique() if '공고번호' in period_df.columns else 0,
                'companies': period_df['업체명'].nunique() if '업체명' in period_df.columns else 0,
                'bids': len(period_df),
                'wins': len(period_df[period_df['순위'] == 1])
            }

        # 1개월 날짜 범위
        df_2y = self.period_data['1m']
        self.date_min_1m = df_2y['투찰일시'].min().strftime('%Y-%m-%d') if len(df_2y) > 0 else ''
        self.date_max_1m = df_2y['투찰일시'].max().strftime('%Y-%m-%d') if len(df_2y) > 0 else ''

        print("\n  기간별 통계:")
        print(f"  {'기간':<6} {'공고':<8} {'업체':<8} {'투찰':<8} {'1위':<8}")
        print("  " + "-" * 40)
        for period in PERIOD_ORDER:
            stats = self.period_stats[period]
            print(f"  {period:<6} {stats['announcements']:<8} {stats['companies']:<8} {stats['bids']:<8} {stats['wins']:<8}")

        return self

    def analyze_company_timeline(self):
        """업체별 시계열 분석 - 투찰 위치 이동 패턴"""
        print("\n[2/7] 업체별 시계열 분석...")

        # 모든 업체 수집
        all_companies = set()
        for period_df in self.period_data.values():
            all_companies.update(period_df['업체명'].unique())

        # 업체별 기간별 평균 위치 계산
        for company in all_companies:
            self.company_timeline[company] = {}

            for period in PERIOD_ORDER:
                period_df = self.period_data[period]
                company_df = period_df[period_df['업체명'] == company]

                if len(company_df) > 0:
                    avg_pos = company_df['기초대비사정률'].mean()
                    count = len(company_df)
                    self.company_timeline[company][period] = {
                        'avg_position': round(avg_pos, 5),
                        'count': count,
                        'min': round(company_df['기초대비사정률'].min(), 5),
                        'max': round(company_df['기초대비사정률'].max(), 5),
                        'std_position': round(company_df['기초대비사정률'].std(), 5) if count > 1 else 0
                    }

        print(f"  - {len(all_companies)}개 업체 분석")
        return self

    def predict_company_movement(self):
        """업체 이동 방향 분석 (예측 아님)"""
        print("\n[3/7] 경쟁업체 분석...")

        for company, timeline in self.company_timeline.items():
            # 내 업체 제외
            if company == MY_COMPANY:
                continue

            # 최소 2개 기간 데이터 필요
            periods_with_data = [p for p in PERIOD_ORDER if p in timeline]

            if len(periods_with_data) < 2:
                continue

            # 장기 vs 단기 비교
            positions = []
            for period in PERIOD_ORDER:
                if period in timeline:
                    positions.append((period, timeline[period]['avg_position']))

            if len(positions) >= 2:
                # 가장 오래된 것과 가장 최근 것 비교
                oldest_pos = positions[0][1]
                newest_pos = positions[-1][1]

                diff = newest_pos - oldest_pos

                # 이동 방향
                if diff > 0.02:
                    direction = '상향'
                elif diff < -0.02:
                    direction = '하향'
                else:
                    direction = '유지'

                # [FIX 2] 최소값/표준편차 추가 (평균 대신 최소값으로 가림막 안전 위치)
                newest_period = periods_with_data[-1]
                newest_min = timeline[newest_period]['min']
                newest_std = timeline[newest_period].get('std_position', 0)

                self.company_movement[company] = {
                    'direction': direction,
                    'oldest_position': oldest_pos,
                    'newest_position': newest_pos,
                    'newest_min': newest_min,
                    'newest_std': newest_std,
                    'total_bids': sum(timeline[p]['count'] for p in timeline)
                }

        # 통계
        directions = [m['direction'] for m in self.company_movement.values()]
        print(f"  - 분석 업체: {len(self.company_movement)}개")
        print(f"  - 상향: {directions.count('상향')}개, 하향: {directions.count('하향')}개, 유지: {directions.count('유지')}개")

        return self

    def analyze_company_distribution_strategy(self):
        """업체별 분포 전략 분석 (중앙값/분포 기반, 전체업체)"""
        print("\n[3.2/7] 업체별 분포 전략 분석...")

        # 기간별 전체 경쟁밀도(업체 분포) 계산
        period_density = {}
        for period in PERIOD_ORDER:
            period_df = self.period_data.get(period, pd.DataFrame())
            rates = period_df[period_df['업체명'] != MY_COMPANY]['기초대비사정률'].dropna().astype(float).values
            bins = np.floor(((rates - MIN_RATE) / ZONE_SIZE) + 1e-12).astype(int)
            bins = bins[(bins >= 0) & (bins < NUM_ZONES)]
            counts = np.bincount(bins, minlength=NUM_ZONES).astype(float)
            total = float(counts.sum())
            if total > 0:
                period_density[period] = counts / total
            else:
                period_density[period] = np.zeros(NUM_ZONES, dtype=float)

        # 전체 업체 수집 (내 업체 제외)
        companies = sorted({
            str(name).strip()
            for name in self.df_full['업체명'].dropna()
            if str(name).strip() and str(name).strip() != MY_COMPANY
        })

        profiles = {}
        for company in companies:
            period_profiles = {}

            for period in PERIOD_ORDER:
                period_df = self.period_data.get(period, pd.DataFrame())
                rates = period_df[period_df['업체명'] == company]['기초대비사정률'].dropna().astype(float).values
                if len(rates) == 0:
                    continue

                bins = np.floor(((rates - MIN_RATE) / ZONE_SIZE) + 1e-12).astype(int)
                bins = bins[(bins >= 0) & (bins < NUM_ZONES)]
                if len(bins) == 0:
                    continue

                counts = np.bincount(bins, minlength=NUM_ZONES).astype(float)
                total = float(counts.sum())
                probs = counts[counts > 0] / total

                median_rate = float(np.median(rates))
                q10 = float(np.percentile(rates, 10))
                q25 = float(np.percentile(rates, 25))
                q75 = float(np.percentile(rates, 75))
                q90 = float(np.percentile(rates, 90))
                iqr = q75 - q25
                mad = float(np.median(np.abs(rates - median_rate)))

                modal_bin = int(np.argmax(counts))
                modal_rate = float(MIN_RATE + modal_bin * ZONE_SIZE)
                modal_share = float(counts[modal_bin] / total)
                occupied_bins = int(np.count_nonzero(counts))

                if len(probs) > 1:
                    entropy_norm = float(-(probs * np.log(probs)).sum() / np.log(NUM_ZONES))
                else:
                    entropy_norm = 0.0

                period_profiles[period] = {
                    'count': int(total),
                    'median_rate': round(median_rate, 5),
                    'q10': round(q10, 5),
                    'q25': round(q25, 5),
                    'q75': round(q75, 5),
                    'q90': round(q90, 5),
                    'iqr': round(iqr, 5),
                    'mad': round(mad, 5),
                    'modal_bin': modal_bin,
                    'modal_rate': round(modal_rate, 5),
                    'modal_share': round(modal_share, 5),
                    'occupied_bins': occupied_bins,
                    'entropy_norm': round(entropy_norm, 5),
                }

            if len(period_profiles) < 2:
                continue

            oldest_period = next((p for p in PERIOD_ORDER if p in period_profiles), None)
            newest_period = next((p for p in reversed(PERIOD_ORDER) if p in period_profiles), None)
            if not oldest_period or not newest_period:
                continue

            oldest = period_profiles[oldest_period]
            newest = period_profiles[newest_period]

            shift_median = newest['median_rate'] - oldest['median_rate']
            pooled_mad = float(np.median([oldest['mad'], newest['mad']]))
            direction_threshold = max(0.00200, pooled_mad * 1.20000)

            if shift_median > direction_threshold:
                direction = '상향'
            elif shift_median < -direction_threshold:
                direction = '하향'
            else:
                direction = '유지'

            newest_entropy = newest['entropy_norm']
            newest_iqr = newest['iqr']
            newest_modal_share = newest['modal_share']
            newest_occupied_bins = newest['occupied_bins']

            if newest_modal_share >= 0.50000 and newest_iqr <= 0.00800:
                strategy_type = '집중-고정'
            elif newest_entropy >= 0.60000 and newest_occupied_bins >= 12:
                strategy_type = '분산-다점'
            else:
                strategy_type = '혼합-전환'

            reasons = []

            old_bin = int(oldest['modal_bin'])
            new_bin = int(newest['modal_bin'])
            old_density = float(period_density.get(oldest_period, np.zeros(NUM_ZONES))[old_bin])
            new_density = float(period_density.get(newest_period, np.zeros(NUM_ZONES))[new_bin])
            density_delta = new_density - old_density

            if density_delta <= -0.00080:
                reasons.append("경쟁밀도 낮은 구간으로 이동")
            elif density_delta >= 0.00080:
                reasons.append("경쟁밀도 높은 구간으로 이동")

            if direction == '상향':
                reasons.append("중앙값 기준 상향 이동")
            elif direction == '하향':
                reasons.append("중앙값 기준 하향 이동")
            else:
                reasons.append("중앙값 변동 미미")

            entropy_delta = newest['entropy_norm'] - oldest['entropy_norm']
            iqr_delta = newest['iqr'] - oldest['iqr']
            if entropy_delta >= 0.08000 or iqr_delta >= 0.00800:
                reasons.append("복수 구간 분산 배치 확대")
            elif entropy_delta <= -0.08000 or iqr_delta <= -0.00800:
                reasons.append("특정 구간 집중 강화")

            win_rates = self.df_full[
                (self.df_full['업체명'] == company) & (self.df_full['순위'] == 1)
            ]['기초대비사정률'].dropna().astype(float).values
            if len(win_rates) >= 2:
                win_median = float(np.median(win_rates))
                win_distance = abs(newest['median_rate'] - win_median)
                if win_distance <= 0.00300:
                    reasons.append("과거 낙찰 구간 근처 재진입")
                elif win_distance >= 0.01500:
                    reasons.append("과거 낙찰 구간에서 이탈 시도")

            # 중복 제거 및 최대 3개만 사용
            compact_reasons = []
            for reason in reasons:
                if reason not in compact_reasons:
                    compact_reasons.append(reason)
            if not compact_reasons:
                compact_reasons = ["분포 변화가 작아 기존 전략 유지"]

            profiles[company] = {
                'direction': direction,
                'strategy_type': strategy_type,
                'period_from': oldest_period,
                'period_to': newest_period,
                'shift_median': round(shift_median, 5),
                'direction_threshold': round(direction_threshold, 5),
                'density_delta': round(density_delta, 5),
                'entropy_delta': round(entropy_delta, 5),
                'iqr_delta': round(iqr_delta, 5),
                'reasons': compact_reasons[:3],
                'summary': {
                    'oldest_count': oldest['count'],
                    'newest_count': newest['count'],
                    'oldest_median': oldest['median_rate'],
                    'newest_median': newest['median_rate'],
                    'oldest_iqr': oldest['iqr'],
                    'newest_iqr': newest['iqr'],
                    'oldest_entropy': oldest['entropy_norm'],
                    'newest_entropy': newest['entropy_norm'],
                    'oldest_modal_rate': oldest['modal_rate'],
                    'newest_modal_rate': newest['modal_rate'],
                },
                'period_profiles': period_profiles
            }

        self.company_strategy_profiles = profiles
        print(f"  - 전체 업체 전략 프로파일: {len(self.company_strategy_profiles)}개")
        return self

    def analyze_strategic_stability(self):
        """게임이론 관점의 전략 안정성 분석 (기초대비사정률 기준)"""
        print("\n[3.5/7] 게임이론 분석...")

        # === A. 전략 변동성 (Strategy Volatility) ===
        volatile_companies = []
        for company, timeline in self.company_timeline.items():
            if company == MY_COMPANY:
                continue
            positions_over_time = []
            for period in PERIOD_ORDER:
                if period in timeline:
                    positions_over_time.append(timeline[period]['avg_position'])
            if len(positions_over_time) >= 2:
                volatility = float(np.std(positions_over_time))
                movement = self.company_movement.get(company, {})
                direction = movement.get('direction', '불명')
                volatile_companies.append({
                    'company': company,
                    'volatility': round(volatility, 5),
                    'direction': direction
                })

        volatile_companies.sort(key=lambda x: x['volatility'], reverse=True)
        top_volatile = volatile_companies[:5]

        print(f"  - 변동성 상위 5개 업체:")
        for vc in top_volatile:
            print(f"    {vc['company']}: {vc['volatility']:.5f} ({vc['direction']})")

        # === B. 수렴 경고 (Convergence Warning) ===
        # 인기 구간: 3개월 데이터에서 1위 빈도 상위 10%
        df_3m = self.period_data['3m']
        win_df = df_3m[df_3m['순위'] == 1]
        win_positions = win_df['기초대비사정률'].apply(lambda x: round(round(x, 3), 3))
        win_counts = win_positions.value_counts()

        if len(win_counts) > 0:
            threshold = np.percentile(win_counts.values, 90)
            hot_zones = set(win_counts[win_counts >= threshold].index)
        else:
            hot_zones = set()

        # 최근(3m) 인기 구간으로 이동한 업체 수
        converging_count = 0
        total_tracked = max(len(self.company_movement), 1)

        for company, movement in self.company_movement.items():
            newest_pos = round(movement['newest_position'], 3)
            for hz in hot_zones:
                if abs(newest_pos - hz) < 0.01:
                    converging_count += 1
                    break

        convergence_ratio = converging_count / total_tracked
        convergence_warning = convergence_ratio >= 0.30

        print(f"  - 인기 구간: {len(hot_zones)}개")
        print(f"  - 수렴 비율: {convergence_ratio:.1%} ({'경고' if convergence_warning else '안전'})")

        # === C. 포지션 엔트로피 (Position Entropy) ===
        df_3m_other = df_3m[df_3m['업체명'] != MY_COMPANY]
        all_positions = df_3m_other['기초대비사정률'].apply(lambda x: round(round(x, 3), 3))
        pos_counts = all_positions.value_counts()
        total_bids = pos_counts.sum()

        if total_bids > 0:
            probabilities = (pos_counts / total_bids).values
            probabilities = probabilities[probabilities > 0]
            position_entropy = float(-np.sum(probabilities * np.log(probabilities)))
        else:
            position_entropy = 0

        # 엔트로피 해석
        max_entropy = np.log(NUM_ZONES) if NUM_ZONES > 0 else 1
        entropy_ratio = position_entropy / max_entropy if max_entropy > 0 else 0

        if entropy_ratio > 0.7:
            entropy_interp = "분산 투찰 → 빈 공간 전략 유리"
            strategy_rec = "왼쪽빈공간 중심 전략 권장"
        elif entropy_ratio < 0.3:
            entropy_interp = "밀집 투찰 → 가림막 전략 유리"
            strategy_rec = "가림막 중심 전략 권장"
        else:
            entropy_interp = "혼합 분포 → 종합 전략 유지"
            strategy_rec = "경쟁밀도+빈공간+가림막 균형 유지"

        print(f"  - 포지션 엔트로피: {position_entropy:.2f} (최대 {max_entropy:.2f}의 {entropy_ratio:.1%})")
        print(f"  - 해석: {entropy_interp}")

        # 결과 저장
        self.strategic_stability = {
            'volatile_companies': top_volatile,
            'convergence_ratio': round(convergence_ratio, 3),
            'convergence_warning': convergence_warning,
            'position_entropy': round(position_entropy, 2),
            'entropy_ratio': round(entropy_ratio, 3),
            'entropy_interpretation': entropy_interp,
            'strategy_recommendation': strategy_rec
        }

        return self

    def build_position_analysis(self):
        """위치별 분석 (3개월+1개월 데이터)"""
        print("\n[4/7] 위치별 분석 (3개월+1개월)...")

        # 3개월 데이터 사용 (1개월 포함)
        df = self.period_data['3m']

        # 초기화
        for i in range(NUM_ZONES):
            position = round(MIN_RATE + i * ZONE_SIZE, 3)
            self.position_data[position] = {
                'position': position,
                'total': 0,
                'wins': 0,
                'companies': set()
            }

        # 데이터 채우기
        for _, row in df.iterrows():
            rate = row['기초대비사정률']
            company = row['업체명']
            rank = row['순위']

            position = round(round(rate, 3), 3)
            if position not in self.position_data:
                continue

            self.position_data[position]['total'] += 1
            self.position_data[position]['companies'].add(company)
            if rank == 1:
                self.position_data[position]['wins'] += 1

        filled = sum(1 for p in self.position_data.values() if p['total'] > 0)
        print(f"  - 데이터 있는 구간: {filled}/{NUM_ZONES}")

        return self

    def _precompute_announcement_rates(self):
        """[FIX 3] 공고별 투찰률 사전 계산 (성능 최적화)"""
        df = self.period_data['3m']
        self._ann_rates = {}

        for ann_id in df['공고번호'].unique():
            ann_df = df[df['공고번호'] == ann_id]
            rates = ann_df[ann_df['업체명'] != MY_COMPANY]['기초대비사정률'].values
            rates = rates[(rates >= MIN_RATE) & (rates <= MAX_RATE)]
            self._ann_rates[ann_id] = np.sort(rates)

        print(f"  - 공고별 사전계산: {len(self._ann_rates)}개 공고")

    def _calculate_left_gap_per_announcement(self, position):
        """[FIX 3] 공고별 왼쪽 빈 공간 계산 → 중앙값 반환"""
        gaps = []

        for ann_id, rates in self._ann_rates.items():
            left_rates = rates[rates < position]

            if len(left_rates) > 0:
                nearest_left = left_rates[-1]  # 정렬된 배열의 마지막 = 가장 가까운
                gaps.append(position - nearest_left)
            else:
                gaps.append(position - MIN_RATE)

        if gaps:
            return np.median(gaps)
        return 0

    def _calculate_avg_gap_per_announcement(self):
        """[FIX 3] 공고별 투찰 간격의 중앙값"""
        all_gaps = []

        for ann_id, rates in self._ann_rates.items():
            if len(rates) >= 2:
                diffs = np.diff(rates)
                all_gaps.extend(diffs)

        if all_gaps:
            return np.median(all_gaps)
        return 0.01

    def calculate_optimal_positions(self):
        """최적 위치 계산 — KDE 평활화 + 최적 가중치 적용 (기초대비사정률 기준)"""
        print("\n[5/7] 최적 위치 계산...")

        # 공고별 데이터 사전 계산
        self._precompute_announcement_rates()
        avg_gap_per_ann = self._calculate_avg_gap_per_announcement()
        print(f"  - 공고별 평균 gap (중앙값): {avg_gap_per_ann:.4f}%")

        # === 최적 가중치 로드 (없으면 기본값) ===
        weights_path = OUTPUT_DIR / "optimal_weights.json"
        if weights_path.exists():
            try:
                with open(weights_path, 'r') as f:
                    w = json.load(f)
                w_comp = w.get('w_competition', 0.50)
                w_gap = w.get('w_left_gap', 0.30)
                w_shadow = w.get('w_shadow', 0.20)
                if abs(w_comp + w_gap + w_shadow - 1.0) > 0.01:
                    w_comp, w_gap, w_shadow = 0.50, 0.30, 0.20
                    print(f"  - 가중치 합계 오류 → 기본값: 50%-30%-20%")
                else:
                    print(f"  - 최적 가중치 로드: {w_comp:.0%}-{w_gap:.0%}-{w_shadow:.0%} (상관계수: {w.get('correlation', 'N/A')})")
            except (json.JSONDecodeError, KeyError):
                w_comp, w_gap, w_shadow = 0.50, 0.30, 0.20
                print(f"  - 가중치 파일 오류 → 기본값: 50%-30%-20%")
        else:
            w_comp, w_gap, w_shadow = 0.50, 0.30, 0.20
            print(f"  - 기본 가중치: 50%-30%-20%")

        self._weights = {'competition': w_comp, 'left_gap': w_gap, 'shadow': w_shadow}

        # === KDE 경쟁밀도 (기초대비사정률 기준) ===
        kde_density = None
        kde_density_sorted = None
        if HAS_SCIPY:
            all_rates = []
            for pos, data in self.position_data.items():
                if data['total'] > 0:
                    all_rates.extend([pos] * data['total'])
            if len(all_rates) > 10:
                try:
                    kde = gaussian_kde(all_rates, bw_method='silverman')
                    positions_array = np.array(sorted(self.position_data.keys()))
                    density_values = kde(positions_array)
                    kde_density = dict(zip(positions_array, density_values))
                    kde_density_sorted = np.sort(density_values)
                    print(f"  - KDE 경쟁밀도: {len(all_rates)}개 데이터 → 평활화 완료")
                except Exception:
                    kde_density = None
                    print(f"  - KDE 경쟁밀도: 실패 → raw 방식 사용")

        # Fallback: raw bin counting (display용 + KDE 실패 시)
        all_nearby = {}
        for position_key in self.position_data:
            nearby = 0
            for j in range(-10, 11):
                check_pos = round(position_key + j * ZONE_SIZE, 3)
                nearby += self.position_data.get(check_pos, {}).get('total', 0)
            all_nearby[position_key] = nearby

        nearby_values = sorted(all_nearby.values())
        num_positions = len(nearby_values)

        # === 왼쪽빈공간 사전 계산 + 평활화 (기초대비사정률 기준) ===
        raw_left_gaps = {}
        for position in sorted(self.position_data.keys()):
            raw_left_gaps[position] = self._calculate_left_gap_per_announcement(position)

        if HAS_SCIPY and len(raw_left_gaps) > 10:
            positions_sorted = sorted(raw_left_gaps.keys())
            gap_values = np.array([raw_left_gaps[p] for p in positions_sorted])
            sigma = 5  # 5 bins = 0.005%
            kernel_size = sigma * 6 + 1
            x_kern = np.arange(kernel_size) - kernel_size // 2
            kernel = np.exp(-x_kern**2 / (2 * sigma**2))
            kernel /= kernel.sum()
            smoothed_values = np.convolve(gap_values, kernel, mode='same')
            smoothed_left_gaps = dict(zip(positions_sorted, smoothed_values))
            print(f"  - 왼쪽빈공간 평활화 완료 (sigma={sigma})")
        else:
            smoothed_left_gaps = raw_left_gaps

        print(f"  - 경쟁밀도 백분위수 계산: {num_positions}개 위치")

        # 각 위치별 점수 계산
        for position, data in self.position_data.items():

            # === 가림막 점수 (기존 로직 유지) ===
            shadow_score = 0
            shadow_target = None
            shadow_candidates = []

            for company, movement in self.company_movement.items():
                if company == MY_COMPANY:
                    continue

                safe_pos = movement.get('newest_min', movement['newest_position'])
                total_bids = movement['total_bids']
                newest_std = movement.get('newest_std', 0)

                distance = safe_pos - position
                if 0.001 <= distance <= 0.02:
                    shadow_candidates.append({
                        'company': company,
                        'distance': distance,
                        'total_bids': total_bids,
                        'std': newest_std
                    })

            if shadow_candidates:
                shadow_candidates.sort(key=lambda x: (x['distance'], -x['total_bids']))
                best = shadow_candidates[0]

                base_score = min(100, best['total_bids'] * 2)
                decay = np.exp(-69.3 * (best['distance'] - 0.001))

                consistency = max(0, 1 - best['std'] * 100) if best['std'] else 1
                shadow_score = base_score * decay * (0.7 + 0.3 * consistency)

                shadow_target = best['company']

                if len(shadow_candidates) > 1:
                    shadow_score = min(100, shadow_score + len(shadow_candidates) * 3)

            # === 왼쪽 빈 공간 — 평활화된 값 사용 ===
            left_gap = smoothed_left_gaps.get(position, 0)
            left_gap_score = min(100, (left_gap / max(avg_gap_per_ann, 0.001)) * 50)

            # === 경쟁밀도 — KDE 또는 raw 백분위수 ===
            nearby_total = all_nearby[position]
            if kde_density is not None:
                d = kde_density.get(position, 0)
                rank = int(np.searchsorted(kde_density_sorted, d, side='right'))
                percentile = rank / len(kde_density_sorted)
            else:
                rank = sum(1 for x in nearby_values if x <= nearby_total)
                percentile = rank / num_positions if num_positions > 0 else 0.5
            competition_score = (1 - percentile) * 100

            # === 종합 점수 — 최적 가중치 적용 ===
            total_score = (
                competition_score * w_comp +
                left_gap_score * w_gap +
                shadow_score * w_shadow
            )

            self.position_scores[position] = {
                'position': position,
                'shadow_score': round(shadow_score, 2),
                'shadow_target': shadow_target,
                'left_gap_score': round(left_gap_score, 2),
                'left_gap': round(left_gap, 4),
                'competition_score': round(competition_score, 2),
                'nearby_total': nearby_total,
                'total_bids': data['total'],
                'total_score': round(total_score, 2)
            }

        return self

    def generate_recommendations(self):
        """[FIX 6] 최종 추천 생성 - 동적 최소 거리 + 가림막 대상 중복 제거"""
        print("\n[6/7] 최종 추천 생성...")

        # 점수 순 정렬
        sorted_positions = sorted(
            self.position_scores.values(),
            key=lambda x: x['total_score'],
            reverse=True
        )

        # [FIX 6] 동적 최소 거리: 고정업체 간 평균 거리의 절반
        fixed_positions = sorted([
            m.get('newest_min', m['newest_position'])
            for m in self.company_movement.values()
        ])
        if len(fixed_positions) >= 2:
            fixed_gaps = [fixed_positions[i+1] - fixed_positions[i]
                          for i in range(len(fixed_positions)-1)]
            min_distance = max(0.01, np.median(fixed_gaps) * 0.5)
        else:
            min_distance = 0.02

        print(f"  - 추천 간 최소 거리: {min_distance:.3f}%")

        self.recommendations = []
        used_shadow_targets = set()

        for pos_info in sorted_positions:
            if len(self.recommendations) >= self.top_n:
                break

            # 기존 추천과 최소 거리 확인
            is_far = True
            for rec in self.recommendations:
                if abs(pos_info['position'] - rec['position']) < min_distance:
                    is_far = False
                    break

            # 같은 가림막 대상 중복 제거 (다양한 전략 보장)
            shadow_target = pos_info.get('shadow_target')
            if shadow_target and shadow_target in used_shadow_targets:
                continue

            if is_far and pos_info['total_score'] > 0:
                reasons = []
                if shadow_target:
                    movement = self.company_movement.get(shadow_target, {})
                    direction = movement.get('direction', '')
                    reasons.append(f"가림막:{shadow_target[:4]}({direction})")
                if pos_info['left_gap'] >= 0.005:
                    reasons.append(f"왼쪽빈공간:{pos_info['left_gap']:.3f}%")
                if pos_info['competition_score'] >= 70:
                    reasons.append("경쟁↓")

                self.recommendations.append({
                    **pos_info,
                    'rank': len(self.recommendations) + 1,
                    'reasons': reasons
                })

                if shadow_target:
                    used_shadow_targets.add(shadow_target)

        print(f"  - Top {len(self.recommendations)} 추천 생성")
        return self

    def create_visualization(self):
        """기존 1개월 그래프(bidder_chart_1m.png)에 화살표만 추가"""
        print("\n[7/7] 시각화 생성...")

        from PIL import Image, ImageDraw, ImageFont
        Image.MAX_IMAGE_PIXELS = None

        # 기존 bidder-visualization 1개월 그래프 로드
        src_path = OUTPUT_DIR / "bidder_chart_1m.png"
        if not src_path.exists():
            print(f"  - 오류: {src_path} 없음")
            return None

        img = Image.open(src_path)
        draw = ImageDraw.Draw(img)
        img_width, img_height = img.size
        print(f"  - 기존 그래프 로드: {img_width}x{img_height}")

        # 1순위 추천 위치
        if not self.recommendations:
            print("  - 추천 없음")
            return None

        rec = self.recommendations[0]
        zone_idx = int(round((rec['position'] - MIN_RATE) / ZONE_SIZE))

        # X좌표 계산: bidder_chart_1m.png의 실제 축 범위(99.50~100.00) 기준
        chart_min_rate = 99.50
        chart_max_rate = 100.00
        pos_for_chart = min(max(rec['position'], chart_min_rate), chart_max_rate)
        x_ratio = (pos_for_chart - chart_min_rate) / max(chart_max_rate - chart_min_rate, 1e-9)
        x = int(round(x_ratio * (img_width - 1)))

        # Y좌표: 하단에서 상단 방향
        y_bottom = img_height - 1
        y_arrow_start = int(round(y_bottom - max(24, img_height * 0.02)))
        y_arrow_end = int(round(max(10, img_height * 0.18)))

        # 빨간 화살표 (아래에서 위로)
        draw.line([(x, y_arrow_start), (x, y_arrow_end)], fill='red', width=max(4, img_height // 120))

        # 화살표 머리
        draw.polygon([
            (x, y_arrow_end),
            (x - max(10, img_height // 80), y_arrow_end + max(16, img_height // 45)),
            (x + max(10, img_height // 80), y_arrow_end + max(16, img_height // 45))
        ], fill='red')

        # 라벨
        try:
            font = ImageFont.truetype("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 48)
        except:
            try:
                font = ImageFont.truetype("/usr/share/fonts/truetype/nanum/NanumGothicBold.ttf", 48)
            except:
                font = ImageFont.load_default()

        label = f"추천: {rec['position']:.5f}%"
        label_x = max(10, x - 180)
        label_y = max(10, y_arrow_start - 80)

        # 라벨 배경
        draw.rectangle([label_x - 10, label_y - 10, label_x + 420, label_y + 60], fill='white')
        draw.text((label_x, label_y), label, fill='red', font=font)

        # 저장
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        output_path = OUTPUT_DIR / "optimal_position.png"
        img.save(output_path)

        print(f"  - 저장: {output_path}")
        print(f"  - 추천 위치: {rec['position']:.5f}% (zone_idx={zone_idx}, x={x})")
        return output_path

    def save_report(self):
        """리포트 저장"""
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

        report = []
        report.append("=" * 60)
        report.append("최적 투찰 위치 추천")
        report.append("2년→1년→6개월→3개월→1개월 시계열 분석 기반")
        report.append("=" * 60)
        report.append(f"분석일: {datetime.now().strftime('%Y-%m-%d %H:%M')}")
        report.append("")

        report.append("[기간별 통계]")
        report.append(f"{'기간':<6} {'공고':<8} {'업체':<8} {'투찰':<8} {'1위':<8}")
        report.append("-" * 40)
        for period in PERIOD_ORDER:
            stats = self.period_stats[period]
            report.append(f"{period:<6} {stats['announcements']:<8} {stats['companies']:<8} {stats['bids']:<8} {stats['wins']:<8}")
        report.append("")

        report.append("[경쟁업체 분석 (투찰 많은 순)]")
        sorted_movement = sorted(
            self.company_movement.items(),
            key=lambda x: x[1]['total_bids'],
            reverse=True
        )[:10]
        for company, mov in sorted_movement:
            report.append(f"  {company}: {mov['oldest_position']:.3f}% → {mov['newest_position']:.3f}% ({mov['direction']}, {mov['total_bids']}건)")
        report.append("")

        if self.company_strategy_profiles:
            report.append("[업체별 분포전략 요약 (투찰 많은 순 상위 10)]")
            sorted_profiles = sorted(
                self.company_strategy_profiles.items(),
                key=lambda x: x[1]['summary']['newest_count'],
                reverse=True
            )[:10]
            for company, profile in sorted_profiles:
                summary = profile['summary']
                report.append(
                    f"  {company}: {summary['oldest_median']:.5f}%→{summary['newest_median']:.5f}% "
                    f"({profile['direction']}, {profile['strategy_type']}, 사유: {profile['reasons'][0]})"
                )
            report.append("")

        report.append("[최적 투찰 위치 추천]")
        for rec in self.recommendations:
            report.append(f"\n[{rec['rank']}순위] {rec['position']:.3f}%")
            report.append(f"├ 종합점수: {rec['total_score']:.1f}점")
            report.append(f"├ 경쟁밀도: {rec['nearby_total']}건 (주변, {rec['competition_score']:.1f}점)")
            report.append(f"├ 왼쪽빈공간: {rec['left_gap']:.4f}% ({rec['left_gap_score']:.1f}점)")
            report.append(f"├ 가림막: {rec['shadow_target'] or '없음'} ({rec['shadow_score']:.1f}점)")
            report.append(f"└ 이유: {', '.join(rec['reasons']) if rec['reasons'] else '종합분석'}")

        # === 가중치 정보 ===
        report.append("")
        w = getattr(self, '_weights', {'competition': 0.50, 'left_gap': 0.30, 'shadow': 0.20})
        weights_path = OUTPUT_DIR / "optimal_weights.json"
        if weights_path.exists():
            report.append(f"[가중치] 최적 가중치 적용: 경쟁밀도 {w['competition']:.0%} + 빈공간 {w['left_gap']:.0%} + 가림막 {w['shadow']:.0%}")
        else:
            report.append(f"[가중치] 기본값: 경쟁밀도 {w['competition']:.0%} + 빈공간 {w['left_gap']:.0%} + 가림막 {w['shadow']:.0%}")
        report.append(f"  (backtest_weights.py로 역산 가능)")

        # === 게임이론 분석 ===
        if self.strategic_stability:
            ss = self.strategic_stability
            report.append("")
            report.append("[게임이론 분석]")

            # 전략 변동성
            report.append("├ 전략 변동성 (상위 5개 업체):")
            for vc in ss.get('volatile_companies', []):
                report.append(f"│   {vc['company']}: {vc['volatility']:.5f} ({vc['direction']})")

            # 수렴 경고
            cr = ss.get('convergence_ratio', 0)
            cw = ss.get('convergence_warning', False)
            report.append(f"├ 수렴 비율: {cr:.1%} ({'경고 — 인기 구간 과밀화' if cw else '안전'})")

            # 엔트로피
            report.append(f"├ 포지션 엔트로피: {ss.get('position_entropy', 0):.2f} (분포 비율: {ss.get('entropy_ratio', 0):.1%})")
            report.append(f"└ 해석: {ss.get('entropy_interpretation', '')}")
            report.append(f"  → {ss.get('strategy_recommendation', '')}")

        report.append("")
        report.append("=" * 60)
        report.append("모든 분석은 실제 데이터 기반 (하드코딩 없음)")
        report.append("사정률은 추첨이므로 확률은 참고용")
        report.append("=" * 60)

        report_text = "\n".join(report)
        print("\n" + report_text)

        # 저장
        with open(OUTPUT_DIR / "recommendation.md", 'w', encoding='utf-8') as f:
            f.write(report_text)

        with open(OUTPUT_DIR / "recommendation.json", 'w', encoding='utf-8') as f:
            json.dump({
                'analysis_date': datetime.now().isoformat(),
                'period_stats': self.period_stats,
                'company_movement': self.company_movement,
                'company_strategy_profiles': self.company_strategy_profiles,
                'recommendations': self.recommendations,
                'weights': w,
                'strategic_stability': self.strategic_stability
            }, f, ensure_ascii=False, indent=2, default=str)

        if self.company_strategy_profiles:
            strategy_json_path = OUTPUT_DIR / "company_strategy_analysis.json"
            strategy_md_path = OUTPUT_DIR / "company_strategy_report.md"

            with open(strategy_json_path, 'w', encoding='utf-8') as f:
                json.dump({
                    'analysis_date': datetime.now().isoformat(),
                    'period_order': PERIOD_ORDER,
                    'company_strategy_profiles': self.company_strategy_profiles
                }, f, ensure_ascii=False, indent=2, default=str)

            lines = []
            lines.append("=" * 72)
            lines.append("업체별 기초대비사정률 이동방향/전략/원인 분석")
            lines.append("=" * 72)
            lines.append(f"분석일: {datetime.now().strftime('%Y-%m-%d %H:%M')}")
            lines.append(f"범위: {MIN_RATE:.5f}% ~ {MAX_RATE:.5f}%")
            lines.append("")

            sorted_profiles = sorted(
                self.company_strategy_profiles.items(),
                key=lambda x: x[1]['summary']['newest_count'],
                reverse=True
            )

            for idx, (company, profile) in enumerate(sorted_profiles, start=1):
                summary = profile['summary']
                lines.append(f"[{idx}] {company}")
                lines.append(
                    f"- 이동방향: {profile['direction']} "
                    f"(중앙값 {summary['oldest_median']:.5f}% → {summary['newest_median']:.5f}%, "
                    f"변화 {profile['shift_median']:+.5f}%)"
                )
                lines.append(
                    f"- 전략유형: {profile['strategy_type']} "
                    f"(IQR {summary['oldest_iqr']:.5f}% → {summary['newest_iqr']:.5f}%, "
                    f"엔트로피 {summary['oldest_entropy']:.5f} → {summary['newest_entropy']:.5f})"
                )
                lines.append(
                    f"- 대표구간: {summary['oldest_modal_rate']:.5f}% → {summary['newest_modal_rate']:.5f}%"
                )
                lines.append(f"- 추정원인: {', '.join(profile['reasons'])}")
                lines.append("")

            with open(strategy_md_path, 'w', encoding='utf-8') as f:
                f.write("\n".join(lines))

        print(f"\n[저장 완료]")
        if self.create_image:
            print(f"  - 그래프: {OUTPUT_DIR}/optimal_position.png")
        print(f"  - 리포트: {OUTPUT_DIR}/recommendation.md")
        if self.company_strategy_profiles:
            print(f"  - 업체전략 JSON: {OUTPUT_DIR}/company_strategy_analysis.json")
            print(f"  - 업체전략 리포트: {OUTPUT_DIR}/company_strategy_report.md")

    def run(self):
        """전체 분석 실행"""
        self.load_data()
        self.analyze_company_timeline()
        self.predict_company_movement()
        self.analyze_company_distribution_strategy()
        self.analyze_strategic_stability()
        self.build_position_analysis()
        self.calculate_optimal_positions()
        self.generate_recommendations()
        if self.create_image:
            self.create_visualization()
        self.save_report()

        print("\n완료!")
        return self


def main():
    parser = argparse.ArgumentParser(description='최적 투찰 위치 분석기')
    parser.add_argument('--top', type=int, default=5, help='추천 개수 (기본: 5)')
    parser.add_argument('--create-image', action='store_true',
                        help='optimal_position.png 생성 (기본: 생성 안 함)')

    args = parser.parse_args()

    analyzer = OptimalPositionAnalyzer(top_n=args.top, create_image=args.create_image)
    analyzer.run()


if __name__ == "__main__":
    main()
