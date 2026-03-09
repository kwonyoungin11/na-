#!/usr/bin/env python3
"""
업체별 고정 색상맵 생성 스크립트
- 전체 업체에 고유 색상을 하드코딩 팔레트로 고정 할당
- 동국문화재연구원은 고정 대표색 사용
"""

import argparse
import json
from pathlib import Path

import pandas as pd

PROJECT_ROOT = Path(__file__).resolve().parents[4]
DEFAULT_DATA_PATH = PROJECT_ROOT / "전체_통합_데이터.xlsx"
DEFAULT_OUTPUT_PATH = Path(__file__).resolve().parents[1] / "bidder_colors.json"
MY_COMPANY = "동국문화재연구원"
MY_COMPANY_COLOR = "#FF0000"
PALETTE_JUMP = 53  # 고정 점프 할당(하드코딩): 인접 업체 간 색상 유사도 완화

# 고유 색상 하드코딩 팔레트 (중복 없음, 192개)
PREMIUM_PALETTE = [
    '#BF4A4A', '#4ABF6B', '#8C4ABF', '#BFAE4A', '#4AB0BF', '#BF4A8E', '#6DBF4A', '#4A4CBF', '#BF694A', '#4ABF8A', '#AC4ABF', '#B2BF4A',
    '#4A90BF', '#BF4A6F', '#4EBF4A', '#674ABF', '#BF894A', '#4ABFAA', '#BF4AB4', '#92BF4A', '#4A71BF', '#BF4A50', '#4ABF65', '#874ABF',
    '#BFA84A', '#4AB6BF', '#BF4A94', '#73BF4A', '#4A52BF', '#BF634A', '#4ABF85', '#A64ABF', '#B8BF4A', '#4A96BF', '#BF4A75', '#54BF4A',
    '#614ABF', '#BF834A', '#4ABFA4', '#BF4AB9', '#98BF4A', '#4A77BF', '#BF4A56', '#4ABF5F', '#814ABF', '#BFA24A', '#4ABBBF', '#BF4A9A',
    '#79BF4A', '#4A58BF', '#BF5D4A', '#4ABF7F', '#A04ABF', '#BDBF4A', '#4A9CBF', '#BF4A7B', '#5ABF4A', '#5B4ABF', '#BF7D4A', '#4ABF9E',
    '#BF4ABF', '#9EBF4A', '#4A7DBF', '#BF4A5B', '#9F4141', '#419F5C', '#76419F', '#9F9141', '#41939F', '#9F4178', '#5D9F41', '#41439F',
    '#9F5A41', '#419F75', '#90419F', '#949F41', '#417A9F', '#9F415F', '#449F41', '#59419F', '#9F7341', '#419F8E', '#9F4196', '#7B9F41',
    '#41609F', '#9F4146', '#419F57', '#72419F', '#9F8C41', '#41979F', '#9F417D', '#629F41', '#41479F', '#9F5541', '#419F70', '#8B419F',
    '#999F41', '#417E9F', '#9F4164', '#499F41', '#54419F', '#9F6F41', '#419F89', '#9F419B', '#809F41', '#41659F', '#9F414B', '#419F52',
    '#6D419F', '#9F8841', '#419C9F', '#9F4181', '#679F41', '#414C9F', '#9F5141', '#419F6B', '#86419F', '#9E9F41', '#41839F', '#9F4168',
    '#4E9F41', '#4F419F', '#9F6A41', '#419F85', '#9F419F', '#859F41', '#416A9F', '#9F414F', '#C37979', '#79C38E', '#A379C3', '#C3B879',
    '#79B9C3', '#C379A4', '#8FC379', '#797BC3', '#C38D79', '#79C3A2', '#B779C3', '#BAC379', '#79A5C3', '#C37991', '#7CC379', '#8C79C3',
    '#C3A179', '#79C3B5', '#C379BC', '#A7C379', '#7992C3', '#C3797D', '#79C38A', '#9F79C3', '#C3B479', '#79BDC3', '#C379A8', '#93C379',
    '#797EC3', '#C38979', '#79C39E', '#B379C3', '#BEC379', '#79A9C3', '#C37994', '#7FC379', '#8879C3', '#C39D79', '#79C3B2', '#C379BF',
    '#AAC379', '#7996C3', '#C37981', '#79C387', '#9C79C3', '#C3B179', '#79C0C3', '#C379AC', '#97C379', '#7982C3', '#C38679', '#79C39A',
    '#AF79C3', '#C2C379', '#79ADC3', '#C37998', '#83C379', '#8479C3', '#C39979', '#79C3AE', '#C379C3', '#AEC379', '#7999C3', '#C37984',
]


def main():
    parser = argparse.ArgumentParser(description="업체별 고정 색상맵 생성 (하드코딩 팔레트)")
    parser.add_argument("--data", default=str(DEFAULT_DATA_PATH), help="전처리 데이터 경로")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT_PATH), help="출력 JSON 경로")
    args = parser.parse_args()

    data_path = Path(args.data)
    output_path = Path(args.output)

    if not data_path.exists():
        raise FileNotFoundError(f"데이터 파일 없음: {data_path}")

    df = pd.read_excel(data_path)
    if "업체명" not in df.columns:
        raise ValueError("필수 컬럼 없음: 업체명")

    companies = sorted({str(name).strip() for name in df["업체명"].dropna() if str(name).strip()})

    # 동국문화재연구원은 1번으로 고정
    ordered_companies = [c for c in companies if c == MY_COMPANY] + [c for c in companies if c != MY_COMPANY]

    required_palette = len([c for c in ordered_companies if c != MY_COMPANY])
    if required_palette > len(PREMIUM_PALETTE):
        raise ValueError(
            f"팔레트 색상 부족: 필요 {required_palette}개 / 보유 {len(PREMIUM_PALETTE)}개. "
            "PREMIUM_PALETTE를 확장하세요."
        )

    result = {}
    palette_idx = 0
    for idx, company in enumerate(ordered_companies, start=1):
        if company == MY_COMPANY:
            result[company] = {"id": 1, "color": MY_COMPANY_COLOR}
            continue

        color = PREMIUM_PALETTE[(palette_idx * PALETTE_JUMP) % len(PREMIUM_PALETTE)]
        palette_idx += 1
        result[company] = {"id": idx, "color": color}

    # 색상 중복 안전 점검
    used_colors = [info["color"].lower() for info in result.values()]
    if len(used_colors) != len(set(used_colors)):
        raise ValueError("색상 중복 발생: 하드코딩 팔레트를 확인하세요.")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"✅ 색상맵 생성 완료: {output_path}")
    print(f"  - 업체 수: {len(result)}개")
    print(f"  - 팔레트 사용: {palette_idx}개 (동국 고정색 제외)")


if __name__ == "__main__":
    main()
