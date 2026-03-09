#!/usr/bin/env python3
"""고정업체 탐지 모듈.

낙찰 빈도 대신 업체가 특정 구간에 얼마나 꾸준히 고정적으로 남아 있는지를
최근/중기 윈도우까지 함께 본다.
"""

from __future__ import annotations

from collections import defaultdict
from datetime import datetime, timedelta

import numpy as np
import pandas as pd


class FixedBidderDetector:
    """고정 업체 탐지기 (기초대비사정률 기준)."""

    def __init__(
        self,
        df: pd.DataFrame,
        *,
        zone_size: float = 0.001,
        min_bids: int = 5,
        concentration_threshold: float = 40.0,
        profile: dict | None = None,
    ) -> None:
        fixed_profile = (profile or {}).get("fixed_bidder", {})
        rate_profile = (profile or {}).get("rate_range", {})
        self.df = df.copy()
        self.zone_size = float(rate_profile.get("zone_size", zone_size))
        self.min_bids = int(fixed_profile.get("min_bids", min_bids))
        self.concentration_threshold = float(fixed_profile.get("concentration_threshold", concentration_threshold))
        self.results: list[dict] = []
        self.min_rate = float(rate_profile.get("min", 99.5))
        self.max_rate = float(rate_profile.get("max", 100.0))
        self.window_days = {
            "recent": int(fixed_profile.get("window_days", {}).get("recent", 30)),
            "short": int(fixed_profile.get("window_days", {}).get("short", 90)),
            "medium": int(fixed_profile.get("window_days", {}).get("medium", 180)),
        }
        self.consistency_weights = fixed_profile.get(
            "consistency_weights",
            {"concentration": 0.25, "medium": 0.25, "short": 0.25, "recent": 0.25},
        )
        self.score_weights = fixed_profile.get(
            "score_weights",
            {"concentration": 0.45, "consistency": 0.35, "recency_presence": 0.2, "volatility_penalty": 0.15},
        )
        self.recency_multiplier = float(fixed_profile.get("recency_multiplier", 2.0))
        self.volatility_reference = float(fixed_profile.get("volatility_reference", 0.02))

    def _get_zone_index(self, rate: float) -> int | None:
        if pd.isna(rate) or rate < self.min_rate or rate > self.max_rate:
            return None
        zone_idx = int(np.floor(((float(rate) - self.min_rate) / self.zone_size) + 1e-12))
        max_index = int(round((self.max_rate - self.min_rate) / self.zone_size)) - 1
        return zone_idx if 0 <= zone_idx <= max_index else None

    def _get_zone(self, rate: float) -> str | None:
        zone_idx = self._get_zone_index(rate)
        if zone_idx is None:
            return None
        zone_start = self.min_rate + zone_idx * self.zone_size
        zone_end = zone_start + self.zone_size
        return f"{zone_start:.3f}~{zone_end:.3f}%"

    def _collect_window_share(self, company_df: pd.DataFrame, anchor_time: datetime, days: int, main_idx: int) -> tuple[float, int]:
        if "투찰일시" not in company_df.columns:
            return 0.0, 0
        cutoff = anchor_time - timedelta(days=days)
        window = company_df[company_df["투찰일시"] >= cutoff]
        if window.empty:
            return 0.0, 0
        bins = window["기초대비사정률"].map(self._get_zone_index).dropna().astype(int)
        if bins.empty:
            return 0.0, 0
        aligned = bins.apply(lambda idx: abs(idx - main_idx) <= 1)
        return float(aligned.mean() * 100.0), int(len(bins))

    def detect(self) -> list[dict]:
        rate_col = "기초대비사정률"
        company_col = "업체명"
        date_col = "투찰일시"

        if rate_col not in self.df.columns or company_col not in self.df.columns:
            raise ValueError(f"필수 컬럼 없음: {rate_col}, {company_col}")

        work = self.df.copy()
        work[rate_col] = pd.to_numeric(work[rate_col], errors="coerce")
        work = work[(work[rate_col] >= self.min_rate) & (work[rate_col] <= self.max_rate)].copy()
        if date_col in work.columns:
            work[date_col] = pd.to_datetime(work[date_col], errors="coerce")
            anchor_time = work[date_col].dropna().max()
            if pd.isna(anchor_time):
                anchor_time = datetime.now()
        else:
            anchor_time = datetime.now()

        self.results = []
        for company, company_df in work.groupby(company_col):
            bids = company_df[rate_col].dropna().astype(float)
            total_bids = len(bids)
            if total_bids < self.min_bids:
                continue

            zone_indices = bids.map(self._get_zone_index).dropna().astype(int)
            if zone_indices.empty:
                continue
            zone_counts = zone_indices.value_counts().sort_index().to_dict()
            main_idx = max(zone_counts, key=zone_counts.get)
            zone_bids = int(zone_counts[main_idx])
            concentration = float((zone_bids / total_bids) * 100.0)
            if concentration < self.concentration_threshold:
                continue

            aligned_mask = zone_indices.apply(lambda idx: abs(idx - main_idx) <= 1)
            aligned_rates = bids.loc[aligned_mask[aligned_mask].index]
            main_zone_avg = float(np.median(aligned_rates)) if len(aligned_rates) else float(np.median(bids))
            avg_rate = float(np.mean(bids))
            std_rate = float(np.std(bids)) if total_bids > 1 else 0.0

            share_30d, count_30d = self._collect_window_share(company_df, anchor_time, self.window_days["recent"], main_idx)
            share_90d, count_90d = self._collect_window_share(company_df, anchor_time, self.window_days["short"], main_idx)
            share_180d, count_180d = self._collect_window_share(company_df, anchor_time, self.window_days["medium"], main_idx)

            weighted_consistency = np.average(
                [concentration, share_180d or concentration, share_90d or concentration, share_30d or concentration],
                weights=[
                    float(self.consistency_weights.get("concentration", 0.25)),
                    float(self.consistency_weights.get("medium", 0.25)),
                    float(self.consistency_weights.get("short", 0.25)),
                    float(self.consistency_weights.get("recent", 0.25)),
                ],
            )
            recency_presence = min(100.0, ((count_90d / max(total_bids, 1)) * 100.0) * self.recency_multiplier)
            volatility_penalty = min(100.0, (std_rate / self.volatility_reference) * 100.0)
            fixed_score = max(
                0.0,
                min(
                    100.0,
                    (concentration * float(self.score_weights.get("concentration", 0.45)))
                    + (weighted_consistency * float(self.score_weights.get("consistency", 0.35)))
                    + (recency_presence * float(self.score_weights.get("recency_presence", 0.20)))
                    - (volatility_penalty * float(self.score_weights.get("volatility_penalty", 0.15))),
                ),
            )

            main_zone = self._get_zone(main_zone_avg)
            self.results.append(
                {
                    "company": str(company),
                    "main_zone": main_zone,
                    "main_zone_avg": round(main_zone_avg, 5),
                    "concentration": float(round(concentration, 2)),
                    "window_consistency": float(round(weighted_consistency, 2)),
                    "recent_share_30d": float(round(share_30d, 2)),
                    "recent_share_90d": float(round(share_90d, 2)),
                    "recent_share_180d": float(round(share_180d, 2)),
                    "recent_count_30d": count_30d,
                    "recent_count_90d": count_90d,
                    "recent_count_180d": count_180d,
                    "fixed_score": float(round(fixed_score, 2)),
                    "total_bids": total_bids,
                    "zone_bids": zone_bids,
                    "avg_rate": float(round(avg_rate, 5)),
                    "std_rate": float(round(std_rate, 5)),
                    "zone_distribution": {str(idx): int(count) for idx, count in zone_counts.items()},
                }
            )

        self.results.sort(key=lambda x: (x["fixed_score"], x["concentration"], x["total_bids"]), reverse=True)
        print(f"[FixedBidder] 고정업체 탐지: {len(self.results)}개")
        return self.results

    def get_results(self) -> list[dict]:
        return self.results

    def to_dataframe(self) -> pd.DataFrame:
        if not self.results:
            return pd.DataFrame()
        return pd.DataFrame(
            [
                {
                    "업체명": r["company"],
                    "주_투찰구간": r["main_zone"],
                    "구간_중앙값": r["main_zone_avg"],
                    "집중도(%)": r["concentration"],
                    "윈도우일관성(%)": r["window_consistency"],
                    "고정성점수": r["fixed_score"],
                    "총_투찰횟수": r["total_bids"],
                    "구간_투찰횟수": r["zone_bids"],
                    "전체_평균": r["avg_rate"],
                    "표준편차": r["std_rate"],
                }
                for r in self.results
            ]
        )
