"""같은 업체의 동일일자 근접 다건을 대표 1건으로 축약하는 유틸리티."""

from __future__ import annotations

from typing import Any

import pandas as pd


DEFAULT_COMPANY_DAY_RATE_TOLERANCE = 0.003
DEFAULT_MIN_CLUSTER_SIZE = 2


def collapse_same_company_day_clusters(
    df: pd.DataFrame,
    *,
    company_col: str = "업체명",
    rate_col: str = "기초대비사정률",
    primary_date_col: str = "개찰일시",
    fallback_date_col: str | None = "투찰일시",
    rate_tolerance: float = DEFAULT_COMPANY_DAY_RATE_TOLERANCE,
    min_cluster_size: int = DEFAULT_MIN_CLUSTER_SIZE,
) -> tuple[pd.DataFrame, dict[str, Any]]:
    """같은 업체가 같은 날 낸 근접 구간 다건을 1건으로 축약한다.

    - 그룹 기준: 업체명 + 날짜
    - 같은 날짜 안에서도 사정률이 멀리 떨어진 묶음은 별도 cluster로 유지
    - 대표 사정률은 cluster 중앙값(median)

    Returns:
        (축약된 데이터프레임, 통계)
    """

    if df.empty:
        return df.copy(), {
            "input_rows": 0,
            "output_rows": 0,
            "reduced_rows": 0,
            "collapsed_cluster_count": 0,
            "rate_tolerance": float(rate_tolerance),
            "min_cluster_size": int(min_cluster_size),
        }

    if company_col not in df.columns or rate_col not in df.columns:
        return df.copy(), {
            "input_rows": int(len(df)),
            "output_rows": int(len(df)),
            "reduced_rows": 0,
            "collapsed_cluster_count": 0,
            "rate_tolerance": float(rate_tolerance),
            "min_cluster_size": int(min_cluster_size),
        }

    work = df.copy()
    work["_cluster_order"] = range(len(work))
    work["_cluster_company"] = work[company_col].astype(str).str.strip()
    work["_cluster_rate"] = pd.to_numeric(work[rate_col], errors="coerce")

    primary_dt = pd.to_datetime(work[primary_date_col], errors="coerce") if primary_date_col in work.columns else pd.Series(pd.NaT, index=work.index)
    if fallback_date_col and fallback_date_col in work.columns:
        fallback_dt = pd.to_datetime(work[fallback_date_col], errors="coerce")
        work["_cluster_dt"] = primary_dt.fillna(fallback_dt)
    else:
        work["_cluster_dt"] = primary_dt
    work["_cluster_day"] = work["_cluster_dt"].dt.floor("D")

    valid_mask = (
        work["_cluster_company"].ne("")
        & work["_cluster_rate"].notna()
        & work["_cluster_day"].notna()
    )

    retained_rows: list[pd.Series] = []
    collapsed_cluster_count = 0

    def _append_single(row: pd.Series) -> None:
        current = row.copy()
        current["동일일자묶음수"] = 1
        current["동일일자묶음범위"] = 0.0
        retained_rows.append(current)

    def _append_cluster(cluster: pd.DataFrame) -> None:
        nonlocal collapsed_cluster_count
        if len(cluster) < max(1, int(min_cluster_size)):
            for _, row in cluster.sort_values("_cluster_order").iterrows():
                _append_single(row)
            return

        representative = cluster.sort_values("_cluster_order").iloc[0].copy()
        representative[rate_col] = float(cluster["_cluster_rate"].median())
        representative["동일일자묶음수"] = int(len(cluster))
        representative["동일일자묶음범위"] = float(cluster["_cluster_rate"].max() - cluster["_cluster_rate"].min())
        retained_rows.append(representative)
        collapsed_cluster_count += 1

    valid_rows = work[valid_mask].copy()
    for _, group in valid_rows.groupby(["_cluster_company", "_cluster_day"], sort=False):
        ordered = group.sort_values(["_cluster_rate", "_cluster_order"])
        current_indices: list[Any] = []
        current_last_rate: float | None = None

        for index, row in ordered.iterrows():
            rate = float(row["_cluster_rate"])
            if not current_indices:
                current_indices = [index]
                current_last_rate = rate
                continue

            if current_last_rate is not None and (rate - current_last_rate) <= float(rate_tolerance):
                current_indices.append(index)
                current_last_rate = rate
                continue

            _append_cluster(ordered.loc[current_indices])
            current_indices = [index]
            current_last_rate = rate

        if current_indices:
            _append_cluster(ordered.loc[current_indices])

    for _, row in work[~valid_mask].sort_values("_cluster_order").iterrows():
        _append_single(row)

    result = pd.DataFrame(retained_rows).sort_values("_cluster_order").drop(columns=["_cluster_company", "_cluster_rate", "_cluster_dt", "_cluster_day", "_cluster_order"], errors="ignore")
    stats = {
        "input_rows": int(len(df)),
        "output_rows": int(len(result)),
        "reduced_rows": int(len(df) - len(result)),
        "collapsed_cluster_count": int(collapsed_cluster_count),
        "rate_tolerance": float(rate_tolerance),
        "min_cluster_size": int(min_cluster_size),
    }
    return result.reset_index(drop=True), stats
