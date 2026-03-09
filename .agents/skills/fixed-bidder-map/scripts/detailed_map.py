#!/usr/bin/env python3
"""
전체업체 고정자리 상세 지도 생성기

- 입력: /mnt/a/c/전체_통합_데이터.xlsx
- 분석구간: 기초대비사정률 99.5 ~ 100.0
- 기간: 12개월 / 6개월 / 3개월 / 1개월
- 기본 제외 없음(필요 시 명시적으로 지정)
- 산출:
  * 회사별 기간별 실제 위치 목록
  * 기간별 중앙값/편차/집중도
  * 0.1 / 0.01 / 0.001 구간 기준 고정성 지표
  * 12m/6m/3m/1m 기간 간 중심값(중앙값) 편차가 가장 작은 업체 전수 목록
"""

from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import asdict, dataclass
from datetime import timedelta
from pathlib import Path
from typing import Any

import pandas as pd


PROJECT_ROOT = Path(__file__).resolve().parents[4]
DATA_PATH = PROJECT_ROOT / "전체_통합_데이터.xlsx"
OUTPUT_DIR = Path(__file__).parent.parent / "output"

PERIODS: dict[str, timedelta] = {
    "12m": timedelta(days=365),
    "6m": timedelta(days=180),
    "3m": timedelta(days=90),
    "1m": timedelta(days=30),
}
DEFAULT_EXCLUDE_COMPANY_TOKENS: list[str] = []


def normalize_company_name(value: object) -> str:
    text = str(value or "").strip()
    if not text:
        return ""

    patterns = [
        r"\(재\)",
        "재단법인",
        "재단",
        r"\(주\)",
        "주식회사",
        r"\(사\)",
        "사단법인",
        r"\(유\)",
        "유한회사",
        r"\(합\)",
        "합명회사",
        r"\(합자\)",
        "합자회사",
    ]
    for pattern in patterns:
        text = re.sub(pattern, "", text)

    text = re.sub(r"문화유산연구원", "문화재연구원", text)
    text = re.sub(r"문화재보존센터", "문화재연구원", text)
    text = re.sub(r"고고학연구소", "문화재연구원", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def safe_float(value: Any) -> float | None:
    if pd.isna(value):
        return None
    try:
        return float(value)
    except Exception:
        return None


def format_band(value: float | None, decimals: int) -> str | None:
    if value is None:
        return None
    return f"{value:.{decimals}f}"


def dominant_value(series: pd.Series, decimals: int) -> tuple[float | None, int, float]:
    if series.empty:
        return None, 0, 0.0

    rounded = series.round(decimals)
    counts = rounded.value_counts()
    top_value = float(counts.index[0])
    top_count = int(counts.iloc[0])
    concentration = (top_count / len(series)) * 100.0
    return top_value, top_count, concentration


def series_std(series: pd.Series) -> float:
    if len(series) <= 1:
        return 0.0
    value = float(series.std(ddof=0))
    return 0.0 if math.isnan(value) else value


def series_mad(series: pd.Series) -> float:
    if len(series) <= 1:
        return 0.0
    median = float(series.median())
    mad = float((series - median).abs().median())
    return 0.0 if math.isnan(mad) else mad


def apply_company_exclusions(df: pd.DataFrame, exclude_tokens: list[str]) -> pd.DataFrame:
    if not exclude_tokens:
        return df
    mask = pd.Series(False, index=df.index)
    normalized = df["업체명정규화"].astype(str)
    for token in exclude_tokens:
        normalized_token = normalize_company_name(token)
        if normalized_token:
            mask = mask | normalized.str.contains(normalized_token, na=False)
    return df[~mask].copy()


@dataclass
class PeriodMetrics:
    company: str
    period: str
    sample_count: int
    median_rate: float | None
    std_rate: float | None
    mad_rate: float | None
    min_rate: float | None
    max_rate: float | None
    range_rate: float | None
    dominant_band_01: str | None
    dominant_count_01: int
    dominant_concentration_01: float
    dominant_band_001: str | None
    dominant_count_001: int
    dominant_concentration_001: float
    dominant_band_0001: str | None
    dominant_count_0001: int
    dominant_concentration_0001: float
    legacy_fixed_candidate: bool
    detailed_fixed_candidate: bool
    micro_fixed_candidate: bool
    position_list: list[float]


def load_data(data_path: Path, exclude_company_tokens: list[str] | None = None) -> tuple[pd.DataFrame, pd.Timestamp]:
    df = pd.read_excel(data_path)
    if "기초대비사정률" not in df.columns and "기초대비사정률(%)" in df.columns:
        df = df.rename(columns={"기초대비사정률(%)": "기초대비사정률"})

    df["업체명정규화"] = df["업체명"].map(normalize_company_name)
    bid_dt = pd.to_datetime(df.get("투찰일시"), errors="coerce")
    open_dt = pd.to_datetime(df.get("개찰일시"), errors="coerce")
    df["기준일시"] = bid_dt.fillna(open_dt)
    df["기초대비사정률"] = df["기초대비사정률"].map(safe_float)
    df = df.dropna(subset=["업체명정규화", "기준일시", "기초대비사정률"]).copy()
    df = df[(df["기초대비사정률"] >= 99.5) & (df["기초대비사정률"] <= 100.0)].copy()
    df = apply_company_exclusions(df, exclude_company_tokens or [])
    if df.empty:
        raise SystemExit("99.5~100.0 구간 데이터가 없습니다.")

    reference_date = pd.Timestamp(df["기준일시"].max()).normalize()
    return df, reference_date


def build_period_metrics(df: pd.DataFrame, period_name: str, cutoff: pd.Timestamp) -> pd.DataFrame:
    period_df = df[df["기준일시"] >= cutoff].copy()
    rows: list[dict[str, Any]] = []
    for company, group in period_df.groupby("업체명정규화", dropna=False):
        rates = group["기초대비사정률"].astype(float)
        if rates.empty:
            continue

        median_rate = float(rates.median())
        std_rate = series_std(rates)
        mad_rate = series_mad(rates)
        min_rate = float(rates.min())
        max_rate = float(rates.max())
        range_rate = max_rate - min_rate

        dom01, cnt01, conc01 = dominant_value(rates, 1)
        dom001, cnt001, conc001 = dominant_value(rates, 2)
        dom0001, cnt0001, conc0001 = dominant_value(rates, 3)

        sample_count = int(len(rates))
        legacy_fixed_candidate = sample_count >= 5 and conc01 >= 60.0
        detailed_fixed_candidate = sample_count >= 5 and conc001 >= 50.0
        micro_fixed_candidate = sample_count >= 5 and conc0001 >= 40.0

        rows.append(
            asdict(
                PeriodMetrics(
                    company=company,
                    period=period_name,
                    sample_count=sample_count,
                    median_rate=round(median_rate, 5),
                    std_rate=round(std_rate, 5),
                    mad_rate=round(mad_rate, 5),
                    min_rate=round(min_rate, 5),
                    max_rate=round(max_rate, 5),
                    range_rate=round(range_rate, 5),
                    dominant_band_01=format_band(dom01, 1),
                    dominant_count_01=cnt01,
                    dominant_concentration_01=round(conc01, 2),
                    dominant_band_001=format_band(dom001, 2),
                    dominant_count_001=cnt001,
                    dominant_concentration_001=round(conc001, 2),
                    dominant_band_0001=format_band(dom0001, 3),
                    dominant_count_0001=cnt0001,
                    dominant_concentration_0001=round(conc0001, 2),
                    legacy_fixed_candidate=legacy_fixed_candidate,
                    detailed_fixed_candidate=detailed_fixed_candidate,
                    micro_fixed_candidate=micro_fixed_candidate,
                    position_list=sorted(round(float(x), 4) for x in rates.tolist()),
                )
            )
        )

    result = pd.DataFrame(rows)
    if result.empty:
        return result
    return result.sort_values(["sample_count", "company"], ascending=[False, True]).reset_index(drop=True)


def build_cross_period_summary(period_frames: dict[str, pd.DataFrame]) -> pd.DataFrame:
    companies = set()
    for frame in period_frames.values():
        companies.update(frame["company"].tolist())

    rows: list[dict[str, Any]] = []
    for company in sorted(companies):
        centers: dict[str, float | None] = {}
        sample_counts: dict[str, int] = {}
        internal_std: dict[str, float | None] = {}
        legacy_flags: dict[str, bool] = {}
        detailed_flags: dict[str, bool] = {}
        micro_flags: dict[str, bool] = {}

        for period_name, frame in period_frames.items():
            row = frame[frame["company"] == company]
            if row.empty:
                centers[period_name] = None
                sample_counts[period_name] = 0
                internal_std[period_name] = None
                legacy_flags[period_name] = False
                detailed_flags[period_name] = False
                micro_flags[period_name] = False
                continue

            r = row.iloc[0]
            centers[period_name] = safe_float(r["median_rate"])
            sample_counts[period_name] = int(r["sample_count"])
            internal_std[period_name] = safe_float(r["std_rate"])
            legacy_flags[period_name] = bool(r["legacy_fixed_candidate"])
            detailed_flags[period_name] = bool(r["detailed_fixed_candidate"])
            micro_flags[period_name] = bool(r["micro_fixed_candidate"])

        available_centers = [c for c in centers.values() if c is not None]
        periods_present = sum(1 for c in centers.values() if c is not None)
        if available_centers:
            center_std = pd.Series(available_centers, dtype=float).std(ddof=0)
            center_range = max(available_centers) - min(available_centers)
            center_mad = float((pd.Series(available_centers, dtype=float) - pd.Series(available_centers, dtype=float).median()).abs().median())
        else:
            center_std = None
            center_range = None
            center_mad = None

        rows.append(
            {
                "company": company,
                "periods_present": periods_present,
                "sample_12m": sample_counts["12m"],
                "sample_6m": sample_counts["6m"],
                "sample_3m": sample_counts["3m"],
                "sample_1m": sample_counts["1m"],
                "median_12m": centers["12m"],
                "median_6m": centers["6m"],
                "median_3m": centers["3m"],
                "median_1m": centers["1m"],
                "internal_std_12m": internal_std["12m"],
                "internal_std_6m": internal_std["6m"],
                "internal_std_3m": internal_std["3m"],
                "internal_std_1m": internal_std["1m"],
                "legacy_fixed_12m": legacy_flags["12m"],
                "legacy_fixed_6m": legacy_flags["6m"],
                "legacy_fixed_3m": legacy_flags["3m"],
                "legacy_fixed_1m": legacy_flags["1m"],
                "detailed_fixed_12m": detailed_flags["12m"],
                "detailed_fixed_6m": detailed_flags["6m"],
                "detailed_fixed_3m": detailed_flags["3m"],
                "detailed_fixed_1m": detailed_flags["1m"],
                "micro_fixed_12m": micro_flags["12m"],
                "micro_fixed_6m": micro_flags["6m"],
                "micro_fixed_3m": micro_flags["3m"],
                "micro_fixed_1m": micro_flags["1m"],
                "cross_period_center_std": round(float(center_std), 5) if center_std is not None else None,
                "cross_period_center_range": round(float(center_range), 5) if center_range is not None else None,
                "cross_period_center_mad": round(float(center_mad), 5) if center_mad is not None else None,
                "all_periods_present": periods_present == 4,
                "all_periods_detailed_fixed": all(detailed_flags.values()) if periods_present == 4 else False,
            }
        )

    result = pd.DataFrame(rows)
    if result.empty:
        return result
    result = result.sort_values(
        ["all_periods_present", "cross_period_center_std", "sample_12m", "company"],
        ascending=[False, True, False, True],
        na_position="last",
    ).reset_index(drop=True)
    return result


def top_stable_companies(cross_df: pd.DataFrame, top_n: int | None = 20) -> pd.DataFrame:
    if cross_df.empty:
        return cross_df
    filtered = cross_df[(cross_df["all_periods_present"]) & cross_df["cross_period_center_std"].notna()].copy()
    filtered = filtered.sort_values(
        ["cross_period_center_std", "cross_period_center_range", "sample_12m"],
        ascending=[True, True, False],
    )
    if top_n is not None:
        return filtered.head(top_n)
    return filtered


def build_json_output(
    reference_date: pd.Timestamp,
    df: pd.DataFrame,
    period_frames: dict[str, pd.DataFrame],
    cross_df: pd.DataFrame,
    exclude_company_tokens: list[str],
) -> dict[str, Any]:
    companies: dict[str, Any] = {}
    company_names = sorted(set(cross_df["company"].tolist()))

    cross_lookup = {row["company"]: row for row in cross_df.to_dict(orient="records")}
    period_lookup = {
        period: {row["company"]: row for row in frame.to_dict(orient="records")}
        for period, frame in period_frames.items()
    }

    for company in company_names:
        companies[company] = {
            "cross_period_summary": cross_lookup.get(company),
            "periods": {period: period_lookup.get(period, {}).get(company) for period in PERIODS},
        }

    metadata = {
        "source_file": str(DATA_PATH),
        "reference_date": reference_date.strftime("%Y-%m-%d"),
        "row_count_filtered": int(len(df)),
        "company_count_all_history_filtered": int(df["업체명정규화"].nunique()),
        "company_count_in_target_periods": len(company_names),
        "periods": list(PERIODS.keys()),
        "analysis_range": [99.5, 100.0],
        "excluded_companies": exclude_company_tokens,
    }

    period_summary: dict[str, Any] = {}
    for period_name, frame in period_frames.items():
        period_summary[period_name] = {
            "company_count": int(len(frame)),
            "legacy_fixed_candidates": int(frame["legacy_fixed_candidate"].sum()) if not frame.empty else 0,
            "detailed_fixed_candidates": int(frame["detailed_fixed_candidate"].sum()) if not frame.empty else 0,
            "micro_fixed_candidates": int(frame["micro_fixed_candidate"].sum()) if not frame.empty else 0,
            "all_companies_by_samples": frame[["company", "sample_count", "median_rate", "std_rate", "mad_rate"]].to_dict(orient="records")
            if not frame.empty
            else [],
        }

    stable_df = top_stable_companies(cross_df, top_n=20)
    stable_all_df = top_stable_companies(cross_df, top_n=None)
    cross_summary = {
        "all_periods_present_companies": int(cross_df["all_periods_present"].sum()) if not cross_df.empty else 0,
        "all_periods_detailed_fixed_companies": int(cross_df["all_periods_detailed_fixed"].sum()) if not cross_df.empty else 0,
        "top_smallest_cross_period_deviation": stable_df.to_dict(orient="records") if not stable_df.empty else [],
        "all_companies_sorted_by_cross_period_deviation": stable_all_df.to_dict(orient="records") if not stable_all_df.empty else [],
    }

    return {
        "metadata": metadata,
        "period_summary": period_summary,
        "cross_period_summary": cross_summary,
        "companies": companies,
    }


def write_markdown_report(
    output_path: Path,
    data_path: Path,
    reference_date: pd.Timestamp,
    df: pd.DataFrame,
    period_frames: dict[str, pd.DataFrame],
    cross_df: pd.DataFrame,
    exclude_company_tokens: list[str],
) -> None:
    stable_df = top_stable_companies(cross_df, top_n=20)
    stable_all_df = top_stable_companies(cross_df, top_n=None)

    lines: list[str] = []
    lines.append("# 전체업체 고정자리 상세 지도")
    lines.append("")
    lines.append(f"- 원본 데이터: `{data_path}`")
    lines.append(f"- 기준일자: `{reference_date:%Y-%m-%d}`")
    lines.append(f"- 분석 구간: `기초대비사정률 99.5 ~ 100.0`")
    lines.append(f"- 필터 후 행수: `{len(df):,}`")
    lines.append(f"- 전체 히스토리 정규화 업체 수: `{df['업체명정규화'].nunique():,}`")
    lines.append(f"- 12m/6m/3m/1m 조사대상 업체 수: `{cross_df['company'].nunique() if not cross_df.empty else 0:,}`")
    lines.append(f"- 제외 업체: `{', '.join(exclude_company_tokens) if exclude_company_tokens else '없음'}`")
    lines.append("")
    lines.append("## 기간별 요약")
    lines.append("")
    lines.append("| 기간 | 업체수 | legacy 고정후보(0.1) | detailed 고정후보(0.01) | micro 고정후보(0.001) |")
    lines.append("|---|---:|---:|---:|---:|")
    for period_name, frame in period_frames.items():
        lines.append(
            f"| {period_name} | {len(frame):,} | {int(frame['legacy_fixed_candidate'].sum()) if not frame.empty else 0:,} | "
            f"{int(frame['detailed_fixed_candidate'].sum()) if not frame.empty else 0:,} | "
            f"{int(frame['micro_fixed_candidate'].sum()) if not frame.empty else 0:,} |"
        )

    lines.append("")
    lines.append("## 기간 간 중심값 편차가 가장 작은 업체 TOP 20")
    lines.append("")
    if stable_df.empty:
        lines.append("- 해당 없음")
    else:
        lines.append("| 순위 | 업체명 | 기간간 중심값 표준편차 | 기간간 중심값 범위 | 12m/6m/3m/1m 중심값 | 12m/6m/3m/1m 표본수 |")
        lines.append("|---:|---|---:|---:|---|---|")
        for idx, row in enumerate(stable_df.itertuples(index=False), 1):
            centers = f"{row.median_12m}/{row.median_6m}/{row.median_3m}/{row.median_1m}"
            samples = f"{row.sample_12m}/{row.sample_6m}/{row.sample_3m}/{row.sample_1m}"
            lines.append(
                f"| {idx} | {row.company} | {row.cross_period_center_std:.5f} | {row.cross_period_center_range:.5f} | {centers} | {samples} |"
            )

    for period_name, frame in period_frames.items():
        lines.append("")
        lines.append(f"## {period_name} 전체업체 상세")
        lines.append("")
        if frame.empty:
            lines.append("- 데이터 없음")
            continue
        top_frame = frame.sort_values(
            ["detailed_fixed_candidate", "micro_fixed_candidate", "sample_count", "dominant_concentration_001", "std_rate"],
            ascending=[False, False, False, False, True],
        )
        lines.append("| 순위 | 업체명 | 표본수 | 중앙값 | 표준편차 | MAD | 0.1 집중도 | 0.01 집중도 | 0.001 집중도 | legacy | detailed | micro |")
        lines.append("|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---|---|")
        for idx, row in enumerate(top_frame.itertuples(index=False), 1):
            lines.append(
                f"| {idx} | {row.company} | {row.sample_count} | {row.median_rate:.5f} | {row.std_rate:.5f} | {row.mad_rate:.5f} | "
                f"{row.dominant_concentration_01:.2f}% | {row.dominant_concentration_001:.2f}% | {row.dominant_concentration_0001:.2f}% | "
                f"{'Y' if row.legacy_fixed_candidate else '-'} | {'Y' if row.detailed_fixed_candidate else '-'} | {'Y' if row.micro_fixed_candidate else '-'} |"
            )

    lines.append("")
    lines.append("## 전체업체 기간 간 중심값 편차 전체 목록")
    lines.append("")
    if stable_all_df.empty:
        lines.append("- 해당 없음")
    else:
        lines.append("| 순위 | 업체명 | 기간간 중심값 표준편차 | 기간간 MAD | 기간간 범위 | 12m/6m/3m/1m 중앙값 |")
        lines.append("|---:|---|---:|---:|---:|---|")
        for idx, row in enumerate(stable_all_df.itertuples(index=False), 1):
            centers = f"{row.median_12m}/{row.median_6m}/{row.median_3m}/{row.median_1m}"
            lines.append(
                f"| {idx} | {row.company} | {row.cross_period_center_std:.5f} | {row.cross_period_center_mad:.5f} | {row.cross_period_center_range:.5f} | {centers} |"
            )

    output_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="전체업체 고정자리 상세 지도")
    parser.add_argument("--data", default=str(DATA_PATH), help="분석 대상 Excel 파일")
    parser.add_argument("--output-dir", default=str(OUTPUT_DIR), help="출력 디렉터리")
    parser.add_argument(
        "--exclude-company",
        action="append",
        default=[],
        help="제외할 업체명(복수 지정 가능). 미지정 시 제외 없음.",
    )
    args = parser.parse_args()

    data_path = Path(args.data)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    exclude_company_tokens = list(DEFAULT_EXCLUDE_COMPANY_TOKENS)
    for token in args.exclude_company:
        if token and token not in exclude_company_tokens:
            exclude_company_tokens.append(token)

    print("=" * 72)
    print("전체업체 고정자리 상세 지도")
    print("=" * 72)

    print("\n[1/4] 데이터 로드...")
    df, reference_date = load_data(data_path, exclude_company_tokens=exclude_company_tokens)
    print(f"  - 필터 후 행수: {len(df):,}")
    print(f"  - 정규화 업체 수: {df['업체명정규화'].nunique():,}")
    print(f"  - 기준일자: {reference_date:%Y-%m-%d}")
    print(f"  - 제외 업체: {', '.join(exclude_company_tokens)}")

    print("\n[2/4] 기간별 고정자리 지표 계산...")
    period_frames: dict[str, pd.DataFrame] = {}
    for period_name, delta in PERIODS.items():
        cutoff = reference_date - delta
        frame = build_period_metrics(df, period_name, cutoff)
        period_frames[period_name] = frame
        print(
            f"  - {period_name}: 업체 {len(frame):,}개 / "
            f"legacy {int(frame['legacy_fixed_candidate'].sum()) if not frame.empty else 0:,} / "
            f"detailed {int(frame['detailed_fixed_candidate'].sum()) if not frame.empty else 0:,} / "
            f"micro {int(frame['micro_fixed_candidate'].sum()) if not frame.empty else 0:,}"
        )

    print("\n[3/4] 기간 간 편차 분석...")
    cross_df = build_cross_period_summary(period_frames)
    stable_df = top_stable_companies(cross_df, top_n=10)
    print(f"  - 모든 기간 존재 업체 수: {int(cross_df['all_periods_present'].sum()) if not cross_df.empty else 0:,}")
    print(f"  - 모든 기간 detailed 고정 후보 수: {int(cross_df['all_periods_detailed_fixed'].sum()) if not cross_df.empty else 0:,}")
    if not stable_df.empty:
        print("  - 기간 간 중심값 편차 최소 TOP 10:")
        for idx, row in enumerate(stable_df.itertuples(index=False), 1):
            print(
                f"    {idx}. {row.company} | std={row.cross_period_center_std:.5f} | "
                f"range={row.cross_period_center_range:.5f} | "
                f"centers={row.median_12m}/{row.median_6m}/{row.median_3m}/{row.median_1m}"
            )

    print("\n[4/4] 산출물 저장...")
    json_output = build_json_output(reference_date, df, period_frames, cross_df, exclude_company_tokens)
    json_path = output_dir / "detailed_fixed_bidder_map.json"
    json_path.write_text(json.dumps(json_output, ensure_ascii=False, indent=2), encoding="utf-8")

    xlsx_path = output_dir / "detailed_fixed_bidder_map.xlsx"
    with pd.ExcelWriter(xlsx_path, engine="openpyxl") as writer:
        for period_name, frame in period_frames.items():
            export_frame = frame.drop(columns=["position_list"], errors="ignore")
            export_frame.to_excel(writer, sheet_name=period_name, index=False)
        cross_df.to_excel(writer, sheet_name="cross_period", index=False)
        stable_df.to_excel(writer, sheet_name="stable_top20", index=False)
        top_stable_companies(cross_df, top_n=None).to_excel(writer, sheet_name="stable_all", index=False)

    md_path = output_dir / "detailed_fixed_bidder_map.md"
    write_markdown_report(md_path, data_path, reference_date, df, period_frames, cross_df, exclude_company_tokens)

    print(f"  - JSON: {json_path}")
    print(f"  - Excel: {xlsx_path}")
    print(f"  - Markdown: {md_path}")


if __name__ == "__main__":
    main()
