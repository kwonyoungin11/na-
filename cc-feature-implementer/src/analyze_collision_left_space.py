"""Analyze collision anchors and left-space effects in Nara bidding data."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd

from analyze_crowding_probability import load_bid_data


@dataclass(frozen=True)
class CollisionAnalysisConfig:
    data_path: Path
    output_dir: Path
    min_anchor_samples: int = 20
    min_rate: float | None = None
    max_rate: float | None = None


def prepare_complete_notice_rows(df: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, Any]]:
    winner_counts = df.assign(is_winner=df["순위"].eq(1)).groupby("공고번호")["is_winner"].sum()
    complete_notice_ids = winner_counts.loc[winner_counts.eq(1)].index
    all_complete = df.loc[df["공고번호"].isin(complete_notice_ids)].copy()
    valid_rows = all_complete.loc[all_complete["순위"].ge(1)].copy()

    metadata = {
        "all_notice_count": int(df["공고번호"].nunique()),
        "complete_notice_count": int(len(complete_notice_ids)),
        "excluded_notice_count": int(df["공고번호"].nunique() - len(complete_notice_ids)),
        "all_complete_row_count": int(len(all_complete)),
        "valid_rank_row_count": int(len(valid_rows)),
        "negative_rank_excluded_row_count": int(len(all_complete) - len(valid_rows)),
    }
    return all_complete, {"metadata": metadata, "complete_notice_ids": set(complete_notice_ids), "valid_rows": valid_rows}


def build_collision_frame(all_complete_rows: pd.DataFrame) -> pd.DataFrame:
    rows: list[dict[str, Any]] = []

    for notice_id, group in all_complete_rows.groupby("공고번호"):
        sorted_all = group.sort_values("기초대비사정률").reset_index(drop=True)
        sorted_all = sorted_all.reset_index().rename(columns={"index": "all_position"})
        valid = sorted_all.loc[sorted_all["순위"].ge(1)].copy().reset_index(drop=True)
        if valid.empty:
            continue

        for idx, row in valid.iterrows():
            all_position = int(row["all_position"])
            previous_all = sorted_all.iloc[all_position - 1] if all_position > 0 else None
            next_valid = valid.iloc[idx + 1] if idx + 1 < len(valid) else None
            previous_valid = valid.iloc[idx - 1] if idx > 0 else None

            left_space_gap_all = np.nan if previous_all is None else float(row["기초대비사정률"] - previous_all["기초대비사정률"])
            right_anchor_gap = np.nan if next_valid is None else float(next_valid["기초대비사정률"] - row["기초대비사정률"])
            left_gap_valid = np.nan if previous_valid is None else float(row["기초대비사정률"] - previous_valid["기초대비사정률"])

            rows.append(
                {
                    "공고번호": notice_id,
                    "업체명": row["업체명"],
                    "순위": int(row["순위"]),
                    "is_winner": bool(row["순위"] == 1),
                    "참여업체수": int(len(valid)),
                    "기초대비사정률": float(row["기초대비사정률"]),
                    "사정률": float(row["사정률"]),
                    "right_anchor_company": None if next_valid is None else next_valid["업체명"],
                    "right_anchor_gap": right_anchor_gap,
                    "left_gap_valid": left_gap_valid,
                    "left_space_gap_all": left_space_gap_all,
                    "left_prev_rank": None if previous_all is None else int(previous_all["순위"]),
                    "left_prev_company": None if previous_all is None else previous_all["업체명"],
                    "left_space_advantage": np.nan if np.isnan(left_space_gap_all) or np.isnan(right_anchor_gap) else left_space_gap_all - right_anchor_gap,
                    "left_space_ratio": np.nan if np.isnan(left_space_gap_all) or np.isnan(right_anchor_gap) else left_space_gap_all / (right_anchor_gap + 1e-9),
                    "expected_win_probability": 1.0 / len(valid),
                }
            )

    return pd.DataFrame(rows)


def apply_rate_window(collision_df: pd.DataFrame, min_rate: float | None, max_rate: float | None) -> tuple[pd.DataFrame, tuple[float, float]]:
    if collision_df.empty:
        return collision_df.copy(), (float("nan"), float("nan"))

    observed_min = float(collision_df["기초대비사정률"].min())
    observed_max = float(collision_df["기초대비사정률"].max())
    resolved_min = observed_min if min_rate is None else float(min_rate)
    resolved_max = observed_max if max_rate is None else float(max_rate)

    filtered = collision_df.loc[
        collision_df["기초대비사정률"].between(resolved_min, resolved_max, inclusive="both")
    ].copy()
    return filtered, (resolved_min, resolved_max)


def summarize_winner_profile(collision_df: pd.DataFrame) -> pd.DataFrame:
    summary = collision_df.groupby("is_winner", as_index=False).agg(
        행수=("공고번호", "size"),
        평균참여업체수=("참여업체수", "mean"),
        평균우측앵커간격=("right_anchor_gap", "mean"),
        중앙우측앵커간격=("right_anchor_gap", "median"),
        평균좌측여유폭=("left_space_gap_all", "mean"),
        중앙좌측여유폭=("left_space_gap_all", "median"),
        평균좌측우위차이=("left_space_advantage", "mean"),
        중앙좌측우위차이=("left_space_advantage", "median"),
    )
    summary["구분"] = summary["is_winner"].map({True: "낙찰", False: "비낙찰"})
    return summary[["구분", "행수", "평균참여업체수", "평균우측앵커간격", "중앙우측앵커간격", "평균좌측여유폭", "중앙좌측여유폭", "평균좌측우위차이", "중앙좌측우위차이"]]


def _aggregate_lift(group: pd.DataFrame) -> pd.Series:
    actual = float(group["is_winner"].mean())
    expected = float(group["expected_win_probability"].mean())
    return pd.Series(
        {
            "표본수": int(len(group)),
            "실제낙찰확률": actual,
            "기대낙찰확률": expected,
            "낙찰리프트": np.nan if expected == 0 else actual / expected,
            "평균순위": float(group["순위"].mean()),
            "평균좌측여유폭": float(group["left_space_gap_all"].mean()),
            "평균우측앵커간격": float(group["right_anchor_gap"].mean()),
            "평균좌측우위차이": float(group["left_space_advantage"].mean()),
        }
    )


AGGREGATE_COLUMNS = [
    "is_winner",
    "expected_win_probability",
    "순위",
    "left_space_gap_all",
    "right_anchor_gap",
    "left_space_advantage",
]


def summarize_left_space_tiers(collision_df: pd.DataFrame) -> pd.DataFrame:
    working = collision_df.dropna(subset=["left_space_gap_all", "right_anchor_gap"]).copy()
    working["좌측여유구간"] = pd.qcut(working["left_space_gap_all"], 3, labels=["좁음", "중간", "넓음"])
    summary = working.groupby("좌측여유구간", observed=False)[AGGREGATE_COLUMNS].apply(_aggregate_lift).reset_index()
    order = {"좁음": 0, "중간": 1, "넓음": 2}
    return summary.sort_values(by="좌측여유구간", key=lambda s: s.map(order)).reset_index(drop=True)


def summarize_anchor_left_matrix(collision_df: pd.DataFrame) -> pd.DataFrame:
    working = collision_df.dropna(subset=["left_space_gap_all", "right_anchor_gap"]).copy()
    working["좌측여유구간"] = pd.qcut(working["left_space_gap_all"], 3, labels=["좁음", "중간", "넓음"])
    working["우측앵커거리구간"] = pd.qcut(working["right_anchor_gap"], 3, labels=["가까움", "중간", "멀음"])
    matrix = working.groupby(["우측앵커거리구간", "좌측여유구간"], observed=False)[AGGREGATE_COLUMNS].apply(_aggregate_lift).reset_index()
    right_order = {"가까움": 0, "중간": 1, "멀음": 2}
    left_order = {"좁음": 0, "중간": 1, "넓음": 2}
    matrix = matrix.sort_values(
        by=["우측앵커거리구간", "좌측여유구간"],
        key=lambda s: s.map(right_order) if s.name == "우측앵커거리구간" else s.map(left_order),
    ).reset_index(drop=True)
    return matrix


def summarize_advantage_tiers(collision_df: pd.DataFrame) -> pd.DataFrame:
    working = collision_df.dropna(subset=["left_space_advantage"]).copy()
    working["좌측우위구간"] = pd.qcut(working["left_space_advantage"], 3, labels=["낮음", "중간", "높음"])
    summary = working.groupby("좌측우위구간", observed=False)[AGGREGATE_COLUMNS].apply(_aggregate_lift).reset_index()
    order = {"낮음": 0, "중간": 1, "높음": 2}
    return summary.sort_values(by="좌측우위구간", key=lambda s: s.map(order)).reset_index(drop=True)


def summarize_anchor_companies(collision_df: pd.DataFrame, min_samples: int) -> pd.DataFrame:
    working = collision_df.dropna(subset=["right_anchor_company", "right_anchor_gap"]).copy()
    summary = working.groupby("right_anchor_company", as_index=False).agg(
        표본수=("공고번호", "size"),
        실제낙찰확률=("is_winner", "mean"),
        기대낙찰확률=("expected_win_probability", "mean"),
        평균좌측여유폭=("left_space_gap_all", "mean"),
        평균우측앵커간격=("right_anchor_gap", "mean"),
        평균좌측우위차이=("left_space_advantage", "mean"),
    )
    summary = summary.loc[summary["표본수"] >= min_samples].copy()
    summary["낙찰리프트"] = np.where(summary["기대낙찰확률"] > 0, summary["실제낙찰확률"] / summary["기대낙찰확률"], np.nan)
    summary = summary.sort_values(["표본수", "낙찰리프트"], ascending=[False, False]).reset_index(drop=True)
    return summary


def summarize_key_findings(
    metadata: dict[str, Any],
    left_space_summary: pd.DataFrame,
    matrix_summary: pd.DataFrame,
    advantage_summary: pd.DataFrame,
) -> dict[str, Any]:
    left_map = left_space_summary.set_index("좌측여유구간").to_dict(orient="index")
    advantage_map = advantage_summary.set_index("좌측우위구간").to_dict(orient="index")
    best_cell = matrix_summary.sort_values(["낙찰리프트", "표본수"], ascending=[False, False]).iloc[0].to_dict()
    return {
        "complete_notice_count": metadata["complete_notice_count"],
        "valid_rank_row_count": metadata["valid_rank_row_count"],
        "wide_left_space_lift": left_map.get("넓음", {}).get("낙찰리프트"),
        "narrow_left_space_lift": left_map.get("좁음", {}).get("낙찰리프트"),
        "high_left_advantage_lift": advantage_map.get("높음", {}).get("낙찰리프트"),
        "low_left_advantage_lift": advantage_map.get("낮음", {}).get("낙찰리프트"),
        "best_matrix_cell": best_cell,
    }


def df_to_markdown(df: pd.DataFrame) -> str:
    try:
        return df.to_markdown(index=False)
    except Exception:
        return df.to_string(index=False)


def write_report(
    config: CollisionAnalysisConfig,
    metadata: dict[str, Any],
    winner_profile: pd.DataFrame,
    left_space_summary: pd.DataFrame,
    matrix_summary: pd.DataFrame,
    advantage_summary: pd.DataFrame,
    anchor_summary: pd.DataFrame,
) -> str:
    wide_left = left_space_summary.set_index("좌측여유구간").loc["넓음"] if "넓음" in set(left_space_summary["좌측여유구간"]) else None
    narrow_left = left_space_summary.set_index("좌측여유구간").loc["좁음"] if "좁음" in set(left_space_summary["좌측여유구간"]) else None
    high_adv = advantage_summary.set_index("좌측우위구간").loc["높음"] if "높음" in set(advantage_summary["좌측우위구간"]) else None
    best_cell = matrix_summary.sort_values(["낙찰리프트", "표본수"], ascending=[False, False]).iloc[0]

    lines = [
        "# 충돌앵커-좌측여유 분석",
        "",
        f"- 입력 파일: `{config.data_path}`",
        f"- 완전공고 수: **{metadata['complete_notice_count']}건**",
        f"- 유효 순위 행 수: **{metadata['valid_rank_row_count']:,}행**",
        f"- 제외된 음수순위 행 수: **{metadata['negative_rank_excluded_row_count']:,}행**",
        "",
        "## 핵심 해석",
    ]

    if wide_left is not None and narrow_left is not None:
        lines.append(
            f"- **좌측여유가 넓은 구간**의 실제 낙찰확률은 `{wide_left['실제낙찰확률']:.4f}`, 기대확률은 `{wide_left['기대낙찰확률']:.4f}`, 리프트는 **{wide_left['낙찰리프트']:.3f}배**입니다."
        )
        lines.append(
            f"- **좌측여유가 좁은 구간**의 실제 낙찰확률은 `{narrow_left['실제낙찰확률']:.4f}`, 기대확률은 `{narrow_left['기대낙찰확률']:.4f}`, 리프트는 **{narrow_left['낙찰리프트']:.3f}배**입니다."
        )
    if high_adv is not None:
        lines.append(
            f"- **좌측우위차이(좌측여유 - 우측앵커간격)가 높은 구간**의 낙찰 리프트는 **{high_adv['낙찰리프트']:.3f}배**입니다."
        )
    lines.append(
        f"- 가장 강한 조합은 **우측앵커거리 {best_cell['우측앵커거리구간']} + 좌측여유 {best_cell['좌측여유구간']}** 이며 리프트는 **{best_cell['낙찰리프트']:.3f}배**입니다."
    )
    lines.extend([
        "",
        "## 낙찰/비낙찰 기본 프로필",
        df_to_markdown(winner_profile),
        "",
        "## 좌측여유 구간별 요약",
        df_to_markdown(left_space_summary),
        "",
        "## 우측앵커거리 × 좌측여유 매트릭스",
        df_to_markdown(matrix_summary),
        "",
        "## 좌측우위차이(좌측여유-우측앵커거리) 구간별 요약",
        df_to_markdown(advantage_summary),
        "",
        f"## 반복 충돌앵커 상위 {min(15, len(anchor_summary))}개",
        df_to_markdown(anchor_summary.head(15)),
    ])
    return "\n".join(lines) + "\n"


def run_analysis(config: CollisionAnalysisConfig) -> dict[str, Any]:
    raw_df = load_bid_data(config.data_path)
    all_complete_rows, prepared = prepare_complete_notice_rows(raw_df)
    collision_df = build_collision_frame(all_complete_rows)
    collision_df, resolved_range = apply_rate_window(collision_df, config.min_rate, config.max_rate)
    winner_profile = summarize_winner_profile(collision_df)
    left_space_summary = summarize_left_space_tiers(collision_df)
    matrix_summary = summarize_anchor_left_matrix(collision_df)
    advantage_summary = summarize_advantage_tiers(collision_df)
    anchor_summary = summarize_anchor_companies(collision_df, config.min_anchor_samples)
    key_findings = summarize_key_findings(prepared["metadata"], left_space_summary, matrix_summary, advantage_summary)
    return {
        "metadata": prepared["metadata"],
        "collision_frame": collision_df,
        "winner_profile": winner_profile,
        "left_space_summary": left_space_summary,
        "matrix_summary": matrix_summary,
        "advantage_summary": advantage_summary,
        "anchor_summary": anchor_summary,
        "key_findings": key_findings,
        "resolved_range": resolved_range,
    }


def save_analysis(result: dict[str, Any], config: CollisionAnalysisConfig) -> None:
    config.output_dir.mkdir(parents=True, exist_ok=True)

    collision_frame: pd.DataFrame = result["collision_frame"]
    winner_profile: pd.DataFrame = result["winner_profile"]
    left_space_summary: pd.DataFrame = result["left_space_summary"]
    matrix_summary: pd.DataFrame = result["matrix_summary"]
    advantage_summary: pd.DataFrame = result["advantage_summary"]
    anchor_summary: pd.DataFrame = result["anchor_summary"]

    collision_frame.to_csv(config.output_dir / "collision_frame.csv", index=False, encoding="utf-8-sig")
    winner_profile.to_csv(config.output_dir / "winner_profile.csv", index=False, encoding="utf-8-sig")
    left_space_summary.to_csv(config.output_dir / "left_space_summary.csv", index=False, encoding="utf-8-sig")
    matrix_summary.to_csv(config.output_dir / "anchor_left_matrix.csv", index=False, encoding="utf-8-sig")
    advantage_summary.to_csv(config.output_dir / "left_advantage_summary.csv", index=False, encoding="utf-8-sig")
    anchor_summary.to_csv(config.output_dir / "anchor_company_summary.csv", index=False, encoding="utf-8-sig")

    summary_payload = {
        "metadata": result["metadata"],
        "resolved_range": result["resolved_range"],
        "key_findings": result["key_findings"],
    }
    (config.output_dir / "summary.json").write_text(json.dumps(summary_payload, ensure_ascii=False, indent=2), encoding="utf-8")
    report = write_report(
        config,
        result["metadata"],
        winner_profile,
        left_space_summary,
        matrix_summary,
        advantage_summary,
        anchor_summary,
    )
    (config.output_dir / "report.md").write_text(report, encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="충돌앵커-좌측여유 분석")
    parser.add_argument("--data", default="/mnt/a/c/전체_통합_데이터.xlsx", help="입력 Excel 경로")
    parser.add_argument("--output-dir", default="/mnt/a/c/console-workspace/output/collision_left_space_analysis", help="출력 디렉터리")
    parser.add_argument("--min-anchor-samples", type=int, default=20, help="앵커업체 최소 표본수")
    parser.add_argument("--min-rate", type=float, default=None, help="분석 최소 사정률 (기본: 데이터 최소값)")
    parser.add_argument("--max-rate", type=float, default=None, help="분석 최대 사정률 (기본: 데이터 최대값)")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    config = CollisionAnalysisConfig(
        data_path=Path(args.data),
        output_dir=Path(args.output_dir),
        min_anchor_samples=args.min_anchor_samples,
        min_rate=args.min_rate,
        max_rate=args.max_rate,
    )
    result = run_analysis(config)
    save_analysis(result, config)
    print(json.dumps(result["key_findings"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
