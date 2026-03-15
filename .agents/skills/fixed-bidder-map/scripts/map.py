#!/usr/bin/env python3
"""
고정업체 지도 (Fixed Bidder Map)
99.5~100.0% 구간에서 고정업체의 실제 투찰 위치를 기간별로 추출
"""

import pandas as pd
import json
import argparse
from pathlib import Path
from datetime import datetime, timedelta
from collections import defaultdict
import sys

PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import analysis_dir, master_path

DATA_PATH = master_path()
OUTPUT_DIR = analysis_dir() / "fixed_bidder_map"

PERIODS = {
    '2y': timedelta(days=730),
    '1y': timedelta(days=365),
    '6m': timedelta(days=180),
    '3m': timedelta(days=90),
}


def load_data():
    """데이터 로드 및 기초대비사정률 99.5~100.0% 필터링"""
    df = pd.read_excel(DATA_PATH)
    df = df[(df['기초대비사정률'] >= 99.5) & (df['기초대비사정률'] <= 100.0)]
    df['투찰일시'] = pd.to_datetime(df['투찰일시'], errors='coerce')
    return df


def find_fixed_bidders(df, min_bids=5, concentration=60):
    """고정업체 탐지 (0.1% 구간 기준)"""
    zone_size = 0.1
    company_zones = defaultdict(list)

    for _, row in df.iterrows():
        company = row['업체명']
        rate = row['기초대비사정률']
        if pd.isna(rate) or rate < 99.5 or rate > 100.0:
            continue
        zone_idx = int((rate - 99.5) / zone_size)
        company_zones[company].append(zone_idx)

    fixed_bidders = []
    for company, zones in company_zones.items():
        if len(zones) < min_bids:
            continue
        zone_counts = defaultdict(int)
        for z in zones:
            zone_counts[z] += 1
        main_zone = max(zone_counts, key=zone_counts.get)
        main_count = zone_counts[main_zone]
        conc = (main_count / len(zones)) * 100
        if conc >= concentration:
            fixed_bidders.append(company)

    return fixed_bidders


def get_positions_by_period(df, company):
    """업체별 기간별 실제 투찰 위치 추출"""
    now = datetime.now()
    company_df = df[df['업체명'] == company]

    result = {}
    for period_name, delta in PERIODS.items():
        cutoff = now - delta
        period_df = company_df[company_df['투찰일시'] >= cutoff]
        positions = period_df['기초대비사정률'].dropna().tolist()
        # 소수점 4자리
        result[period_name] = sorted([round(p, 4) for p in positions])

    return result


def main():
    parser = argparse.ArgumentParser(description='고정업체 지도')
    parser.add_argument('--min-bids', type=int, default=5, help='최소 투찰 횟수')
    parser.add_argument('--concentration', type=int, default=60, help='집중도 기준(%%)')
    args = parser.parse_args()

    OUTPUT_DIR.mkdir(exist_ok=True)

    print("=" * 60)
    print("고정업체 지도 (Fixed Bidder Map)")
    print("=" * 60)

    # 데이터 로드
    print("\n[1/3] 데이터 로드...")
    df = load_data()
    print(f"  - 총 {len(df)}건 (기초대비사정률 99.5~100.0%)")

    # 고정업체 탐지
    print("\n[2/3] 고정업체 탐지...")
    fixed_bidders = find_fixed_bidders(df, args.min_bids, args.concentration)
    print(f"  - 고정업체: {len(fixed_bidders)}개")

    # 기간별 위치 추출
    print("\n[3/3] 기간별 위치 추출...")
    bidder_map = {}
    for company in fixed_bidders:
        positions = get_positions_by_period(df, company)
        bidder_map[company] = positions

        # 출력
        print(f"\n  {company}")
        for period, pos in positions.items():
            if pos:
                print(f"    [{period}] {len(pos)}건: {pos[:5]}{'...' if len(pos) > 5 else ''}")

    # JSON 저장
    output_json = OUTPUT_DIR / "fixed_bidder_map.json"
    with open(output_json, 'w', encoding='utf-8') as f:
        json.dump(bidder_map, f, ensure_ascii=False, indent=2)
    print(f"\n[저장] {output_json}")

    # Excel 저장
    rows = []
    for company, periods in bidder_map.items():
        for period, positions in periods.items():
            for pos in positions:
                rows.append({
                    '업체명': company,
                    '기간': period,
                    '기초대비사정률': pos
                })

    if rows:
        output_xlsx = OUTPUT_DIR / "fixed_bidder_map.xlsx"
        pd.DataFrame(rows).to_excel(output_xlsx, index=False)
        print(f"[저장] {output_xlsx}")


if __name__ == "__main__":
    main()
