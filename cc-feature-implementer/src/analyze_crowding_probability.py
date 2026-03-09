"""Crowding vs award probability analysis for Nara bid data."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd


REQUIRED_COLUMNS = {
    "공고번호",
    "순위",
    "업체명",
    "기초대비사정률",
    "사정률",
    "개찰일시",
}


@dataclass(frozen=True)
class AnalysisConfig:
    data_path: Path
    output_dir: Path
    bin_width: float = 0.001
    min_rate: float | None = None
    max_rate: float | None = None


def load_bid_data(path: Path) -> pd.DataFrame:
    df = pd.read_excel(path)
    missing = REQUIRED_COLUMNS - set(df.columns)
    if missing:
        raise ValueError(f"필수 컬럼 누락: {sorted(missing)}")

    working = df.copy()
    working["개찰일시"] = pd.to_datetime(working["개찰일시"], errors="coerce")
    working["기초대비사정률"] = pd.to_numeric(working["기초대비사정률"], errors="coerce")
    working["사정률"] = pd.to_numeric(working["사정률"], errors="coerce")
    working["순위"] = pd.to_numeric(working["순위"], errors="coerce")
    working = working.dropna(subset=["공고번호", "기초대비사정률", "사정률", "순위"])
    return working


def classify_tertile(series: pd.Series, low_label: str, mid_label: str, high_label: str) -> pd.Series:
    q1 = float(series.quantile(0.33))
    q2 = float(series.quantile(0.67))

    def _classify(value: float) -> str:
        if value <= q1:
            return low_label
        if value >= q2:
            return high_label
        return mid_label

    return series.apply(_classify)


def prepare_notice_level(df: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, Any]]:
    winner_counts = df.assign(is_winner=df["순위"].eq(1)).groupby("공고번호")["is_winner"].sum().rename("낙찰행수")
    notice_metrics = winner_counts.reset_index()
    complete_notices = notice_metrics.loc[notice_metrics["낙찰행수"] == 1, "공고번호"]

    filtered = df.loc[df["공고번호"].isin(complete_notices) & df["순위"].ge(1)].copy()
    notice_participants = filtered.groupby("공고번호").size()
    filtered["참여업체수"] = filtered["공고번호"].map(notice_participants).astype(int)
    filtered["기대낙찰확률"] = 1.0 / filtered["참여업체수"]
    filtered["is_winner"] = filtered["순위"].eq(1)

    winner_rows = filtered.loc[filtered["is_winner"]].copy()
    winner_rows["참여업체수구간"] = classify_tertile(winner_rows["참여업체수"], "적음", "중간", "많음")

    metadata = {
        "all_notice_count": int(df["공고번호"].nunique()),
        "complete_notice_count": int(len(complete_notices)),
        "excluded_notice_count": int(df["공고번호"].nunique() - len(complete_notices)),
        "all_row_count": int(len(df)),
        "complete_row_count": int(len(filtered)),
        "negative_rank_excluded_row_count": int(len(df.loc[df["공고번호"].isin(complete_notices)]) - len(filtered)),
    }
    return filtered, {"metadata": metadata, "winner_rows": winner_rows}


def add_rate_band(df: pd.DataFrame, bin_width: float, min_rate: float, max_rate: float) -> pd.DataFrame:
    working = df.loc[df["기초대비사정률"].between(min_rate, max_rate, inclusive="both")].copy()
    indices = np.floor((working["기초대비사정률"] - min_rate) / bin_width).astype(int)
    working["band_index"] = indices
    working["band_start"] = (min_rate + indices * bin_width).round(6)
    working["band_end"] = (working["band_start"] + bin_width).round(6)
    working["band_label"] = working["band_start"].map(lambda x: f"{x:.3f}~{x + bin_width:.3f}")
    return working


def resolve_rate_window(df: pd.DataFrame, config: AnalysisConfig) -> tuple[float, float]:
    observed_min = float(df["기초대비사정률"].min())
    observed_max = float(df["기초대비사정률"].max())
    min_rate = config.min_rate if config.min_rate is not None else float(np.floor(observed_min * 1000) / 1000)
    max_rate = config.max_rate if config.max_rate is not None else float(np.ceil(observed_max * 1000) / 1000)
    return min_rate, max_rate


def build_band_probability_table(df: pd.DataFrame) -> pd.DataFrame:
    grouped = df.groupby(["band_index", "band_start", "band_end", "band_label"], as_index=False).agg(
        bid_count=("공고번호", "size"),
        notice_count=("공고번호", "nunique"),
        winner_count=("is_winner", "sum"),
        expected_wins=("기대낙찰확률", "sum"),
        avg_notice_adjust_rate=("사정률", "mean"),
        median_notice_adjust_rate=("사정률", "median"),
        avg_bid_adjust_rate=("기초대비사정률", "mean"),
        median_bid_adjust_rate=("기초대비사정률", "median"),
    )
    grouped["actual_win_rate"] = grouped["winner_count"] / grouped["bid_count"]
    grouped["expected_win_rate"] = grouped["expected_wins"] / grouped["bid_count"]
    grouped["win_lift"] = np.where(grouped["expected_win_rate"] > 0, grouped["actual_win_rate"] / grouped["expected_win_rate"], np.nan)
    grouped["crowding_tier"] = classify_tertile(grouped["bid_count"], "적은구간", "중간구간", "많은구간")
    return grouped.sort_values(["band_start"]).reset_index(drop=True)


def build_crowding_summary(band_table: pd.DataFrame) -> pd.DataFrame:
    summary = band_table.groupby("crowding_tier", as_index=False).agg(
        구간수=("band_label", "size"),
        총투찰수=("bid_count", "sum"),
        실제낙찰수=("winner_count", "sum"),
        기대낙찰수=("expected_wins", "sum"),
        평균구간투찰수=("bid_count", "mean"),
        평균사정률=("avg_notice_adjust_rate", "mean"),
    )
    summary["실제낙찰확률"] = summary["실제낙찰수"] / summary["총투찰수"]
    summary["기대낙찰확률"] = summary["기대낙찰수"] / summary["총투찰수"]
    summary["낙찰리프트"] = np.where(summary["기대낙찰확률"] > 0, summary["실제낙찰확률"] / summary["기대낙찰확률"], np.nan)
    tier_order = {"적은구간": 0, "중간구간": 1, "많은구간": 2}
    summary = summary.sort_values(by="crowding_tier", key=lambda s: s.map(tier_order)).reset_index(drop=True)
    return summary


def build_notice_size_summary(filtered_df: pd.DataFrame, winner_rows: pd.DataFrame) -> pd.DataFrame:
    notice_base = winner_rows[["공고번호", "참여업체수", "참여업체수구간", "사정률", "기초대비사정률"]].rename(
        columns={"기초대비사정률": "낙찰기초대비사정률", "사정률": "공고사정률"}
    )
    summary = notice_base.groupby("참여업체수구간", as_index=False).agg(
        공고수=("공고번호", "nunique"),
        평균참여업체수=("참여업체수", "mean"),
        중앙참여업체수=("참여업체수", "median"),
        평균공고사정률=("공고사정률", "mean"),
        중앙공고사정률=("공고사정률", "median"),
        공고사정률표준편차=("공고사정률", "std"),
        평균낙찰기초대비사정률=("낙찰기초대비사정률", "mean"),
        중앙낙찰기초대비사정률=("낙찰기초대비사정률", "median"),
    )
    summary["기대낙찰확률"] = 1.0 / summary["평균참여업체수"]
    order = {"적음": 0, "중간": 1, "많음": 2}
    return summary.sort_values(by="참여업체수구간", key=lambda s: s.map(order)).reset_index(drop=True)


def build_top_band_views(band_table: pd.DataFrame) -> dict[str, list[dict[str, Any]]]:
    crowded = band_table.sort_values(["bid_count", "win_lift"], ascending=[False, False]).head(10)
    sparse = band_table.sort_values(["bid_count", "win_lift"], ascending=[True, False]).head(10)
    overperform = band_table.loc[band_table["winner_count"] > 0].sort_values(["win_lift", "winner_count"], ascending=[False, False]).head(10)
    underperform = band_table.loc[band_table["bid_count"] >= band_table["bid_count"].median()].sort_values(["win_lift", "bid_count"], ascending=[True, False]).head(10)
    return {
        "crowded_bands": crowded.to_dict(orient="records"),
        "sparse_bands": sparse.to_dict(orient="records"),
        "overperforming_bands": overperform.to_dict(orient="records"),
        "underperforming_bands": underperform.to_dict(orient="records"),
    }


def summarize_key_findings(metadata: dict[str, Any], crowding_summary: pd.DataFrame, notice_size_summary: pd.DataFrame) -> dict[str, Any]:
    crowded = crowding_summary.set_index("crowding_tier").to_dict(orient="index")
    notice = notice_size_summary.set_index("참여업체수구간").to_dict(orient="index")
    return {
        "complete_notice_count": metadata["complete_notice_count"],
        "excluded_notice_count": metadata["excluded_notice_count"],
        "crowded_band_lift": crowded.get("많은구간", {}).get("낙찰리프트"),
        "sparse_band_lift": crowded.get("적은구간", {}).get("낙찰리프트"),
        "crowded_notice_expected_win_rate": notice.get("많음", {}).get("기대낙찰확률"),
        "sparse_notice_expected_win_rate": notice.get("적음", {}).get("기대낙찰확률"),
        "crowded_notice_adjust_rate_mean": notice.get("많음", {}).get("평균공고사정률"),
        "sparse_notice_adjust_rate_mean": notice.get("적음", {}).get("평균공고사정률"),
    }


def df_to_markdown(df: pd.DataFrame) -> str:
    try:
        return df.to_markdown(index=False)
    except Exception:
        return df.to_string(index=False)


def write_markdown_report(
    config: AnalysisConfig,
    resolved_range: tuple[float, float],
    metadata: dict[str, Any],
    crowding_summary: pd.DataFrame,
    notice_size_summary: pd.DataFrame,
    top_views: dict[str, list[dict[str, Any]]],
) -> str:
    crowded = crowding_summary.set_index("crowding_tier")
    sparse_row = crowded.loc["적은구간"] if "적은구간" in crowded.index else None
    dense_row = crowded.loc["많은구간"] if "많은구간" in crowded.index else None

    notice_summary = notice_size_summary.set_index("참여업체수구간")
    low_notice = notice_summary.loc["적음"] if "적음" in notice_summary.index else None
    high_notice = notice_summary.loc["많음"] if "많음" in notice_summary.index else None

    lines = [
        "# 전체_통합_데이터 낙찰확률·기대확률·군집도 분석",
        "",
        f"- 입력 파일: `{config.data_path}`",
        f"- 분석 완료 공고 수: **{metadata['complete_notice_count']}건**",
        f"- 제외 공고 수(낙찰 1위 미확인): **{metadata['excluded_notice_count']}건**",
        f"- 분석 행 수(유효 순위만): **{metadata['complete_row_count']:,}행**",
        f"- 제외된 음수순위 행 수: **{metadata['negative_rank_excluded_row_count']:,}행**",
        f"- 분석 사정률 범위: **{resolved_range[0]:.3f} ~ {resolved_range[1]:.3f}**",
        f"- band 폭: **{config.bin_width:.3f}**",
        "",
        "## 핵심 해석",
    ]

    if dense_row is not None and sparse_row is not None:
        lines.extend([
            f"- **사람이 많은 구간**의 실제 낙찰확률은 `{dense_row['실제낙찰확률']:.4f}`이고 기대낙찰확률은 `{dense_row['기대낙찰확률']:.4f}`입니다. 리프트는 **{dense_row['낙찰리프트']:.3f}배**입니다.",
            f"- **사람이 적은 구간**의 실제 낙찰확률은 `{sparse_row['실제낙찰확률']:.4f}`이고 기대낙찰확률은 `{sparse_row['기대낙찰확률']:.4f}`입니다. 리프트는 **{sparse_row['낙찰리프트']:.3f}배**입니다.",
        ])
    if high_notice is not None and low_notice is not None:
        lines.extend([
            f"- **참여업체가 많은 공고**의 기대낙찰확률(1/n)은 평균 **{high_notice['기대낙찰확률']:.4f}** 입니다.",
            f"- **참여업체가 적은 공고**의 기대낙찰확률(1/n)은 평균 **{low_notice['기대낙찰확률']:.4f}** 입니다.",
            f"- 공고 사정률 평균은 참여업체가 많은 공고 `{high_notice['평균공고사정률']:.5f}`, 적은 공고 `{low_notice['평균공고사정률']:.5f}` 입니다.",
        ])

    lines.extend([
        "",
        "## 구간 혼잡도별 요약",
        df_to_markdown(crowding_summary),
        "",
        "## 공고 참여업체 수별 요약",
        df_to_markdown(notice_size_summary),
        "",
        "## 가장 붐비는 구간 Top 10",
        df_to_markdown(pd.DataFrame(top_views["crowded_bands"])[["band_label", "bid_count", "winner_count", "actual_win_rate", "expected_win_rate", "win_lift"]]),
        "",
        "## 가장 한산한 구간 Top 10",
        df_to_markdown(pd.DataFrame(top_views["sparse_bands"])[["band_label", "bid_count", "winner_count", "actual_win_rate", "expected_win_rate", "win_lift"]]),
        "",
        "## 기대보다 낙찰이 많이 나온 구간 Top 10",
        df_to_markdown(pd.DataFrame(top_views["overperforming_bands"])[["band_label", "bid_count", "winner_count", "actual_win_rate", "expected_win_rate", "win_lift"]]),
        "",
        "## 기대보다 덜 나온 붐비는 구간 Top 10",
        df_to_markdown(pd.DataFrame(top_views["underperforming_bands"])[["band_label", "bid_count", "winner_count", "actual_win_rate", "expected_win_rate", "win_lift"]]),
    ])
    return "\n".join(lines) + "\n"


def run_analysis(config: AnalysisConfig) -> dict[str, Any]:
    raw_df = load_bid_data(config.data_path)
    prepared_df, prepared_info = prepare_notice_level(raw_df)
    resolved_range = resolve_rate_window(prepared_df, config)
    band_df = add_rate_band(prepared_df, config.bin_width, *resolved_range)
    band_table = build_band_probability_table(band_df)
    crowding_summary = build_crowding_summary(band_table)
    notice_size_summary = build_notice_size_summary(prepared_df, prepared_info["winner_rows"])
    top_views = build_top_band_views(band_table)
    key_findings = summarize_key_findings(prepared_info["metadata"], crowding_summary, notice_size_summary)
    return {
        "metadata": prepared_info["metadata"],
        "band_table": band_table,
        "crowding_summary": crowding_summary,
        "notice_size_summary": notice_size_summary,
        "top_views": top_views,
        "key_findings": key_findings,
        "resolved_range": resolved_range,
    }


def save_analysis(result: dict[str, Any], config: AnalysisConfig) -> None:
    output_dir = config.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    band_table: pd.DataFrame = result["band_table"]
    crowding_summary: pd.DataFrame = result["crowding_summary"]
    notice_size_summary: pd.DataFrame = result["notice_size_summary"]
    metadata = result["metadata"]
    top_views = result["top_views"]
    key_findings = result["key_findings"]
    resolved_range = result["resolved_range"]

    band_table.to_csv(output_dir / "band_probability_table.csv", index=False, encoding="utf-8-sig")
    crowding_summary.to_csv(output_dir / "crowding_summary.csv", index=False, encoding="utf-8-sig")
    notice_size_summary.to_csv(output_dir / "notice_size_summary.csv", index=False, encoding="utf-8-sig")

    payload = {
        "metadata": metadata,
        "resolved_range": result["resolved_range"],
        "key_findings": key_findings,
        "top_views": top_views,
    }
    (output_dir / "summary.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    report = write_markdown_report(config, resolved_range, metadata, crowding_summary, notice_size_summary, top_views)
    (output_dir / "report.md").write_text(report, encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="낙찰확률·기대확률·군집도 분석")
    parser.add_argument("--data", default="/mnt/a/c/전체_통합_데이터.xlsx", help="입력 Excel 경로")
    parser.add_argument("--output-dir", default="/mnt/a/c/console-workspace/output/crowding_probability_analysis", help="출력 디렉터리")
    parser.add_argument("--bin-width", type=float, default=0.001, help="기초대비사정률 band 폭")
    parser.add_argument("--min-rate", type=float, default=None, help="분석 최소 사정률 (기본: 데이터 최소값)")
    parser.add_argument("--max-rate", type=float, default=None, help="분석 최대 사정률 (기본: 데이터 최대값)")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    config = AnalysisConfig(
        data_path=Path(args.data),
        output_dir=Path(args.output_dir),
        bin_width=args.bin_width,
        min_rate=args.min_rate,
        max_rate=args.max_rate,
    )
    result = run_analysis(config)
    save_analysis(result, config)
    print(json.dumps(result["key_findings"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
