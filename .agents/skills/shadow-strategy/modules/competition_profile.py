#!/usr/bin/env python3
"""경쟁 밀도/과열도 분석 모듈.

낙찰 빈도나 1위 비율이 아니라 실제 경쟁업체의 최근/중기 분포만 사용해
구간별 혼잡도, 신규 유입, 고정업체 인접도를 계산한다.
"""

from __future__ import annotations

from collections import defaultdict
from datetime import datetime, timedelta
from typing import Iterable

import numpy as np
import pandas as pd


class CompetitionProfileAnalyzer:
    """경쟁 밀도 및 과열도 프로파일 분석기."""

    def __init__(
        self,
        df: pd.DataFrame,
        fixed_bidders: list[dict] | None = None,
        *,
        zone_size: float = 0.001,
        min_rate: float = 99.5,
        max_rate: float = 100.0,
        profile: dict | None = None,
    ) -> None:
        rate_profile = (profile or {}).get("rate_range", {})
        competition_profile = (profile or {}).get("competition", {})
        self.df = df.copy()
        self.fixed_bidders = fixed_bidders or []
        self.zone_size = float(rate_profile.get("zone_size", zone_size))
        self.min_rate = float(rate_profile.get("min", min_rate))
        self.max_rate = float(rate_profile.get("max", max_rate))
        self.num_zones = int(round((self.max_rate - self.min_rate) / self.zone_size))
        self.results: list[dict] = []
        self.window_days = {
            "recent": int(competition_profile.get("window_days", {}).get("recent", 30)),
            "short": int(competition_profile.get("window_days", {}).get("short", 90)),
            "medium": int(competition_profile.get("window_days", {}).get("medium", 180)),
            "historical_recent": int(competition_profile.get("window_days", {}).get("historical_recent", 30)),
            "historical_lookback": int(competition_profile.get("window_days", {}).get("historical_lookback", 180)),
        }
        self.weighted_count_weights = competition_profile.get(
            "weighted_count_weights",
            {"recent": 0.5, "short": 0.3, "medium": 0.2},
        )
        self.density_weights = competition_profile.get("density_weights", {"bid_count": 0.6, "company_count": 0.4})
        self.overheat_weights = competition_profile.get("overheat_weights", {"growth": 0.55, "newcomer_ratio": 0.45})
        self.fixed_anchor_search_zone_count = int(competition_profile.get("fixed_anchor_search_zone_count", 20))
        self.fixed_anchor_max_gap = float(competition_profile.get("fixed_anchor_max_gap", 0.02))

    def _clamp_zone(self, rate: float) -> int | None:
        if pd.isna(rate) or rate < self.min_rate or rate > self.max_rate:
            return None
        idx = int(np.floor(((float(rate) - self.min_rate) / self.zone_size) + 1e-12))
        return idx if 0 <= idx < self.num_zones else None

    def _zone_rate(self, idx: int) -> float:
        return round(self.min_rate + (idx * self.zone_size), 5)

    @staticmethod
    def _safe_normalize(values: np.ndarray) -> np.ndarray:
        if values.size == 0:
            return values
        max_value = float(np.max(values))
        if max_value <= 0:
            return np.zeros_like(values, dtype=float)
        return values / max_value

    def _window_frame(self, anchor_time: datetime, days: int) -> pd.DataFrame:
        if "투찰일시" not in self.df.columns:
            return self.df.copy()
        cutoff = anchor_time - timedelta(days=days)
        return self.df[self.df["투찰일시"] >= cutoff].copy()

    def _historical_frame(self, anchor_time: datetime, recent_days: int, lookback_days: int) -> pd.DataFrame:
        if "투찰일시" not in self.df.columns:
            return self.df.iloc[0:0].copy()
        recent_cutoff = anchor_time - timedelta(days=recent_days)
        long_cutoff = anchor_time - timedelta(days=lookback_days)
        return self.df[(self.df["투찰일시"] < recent_cutoff) & (self.df["투찰일시"] >= long_cutoff)].copy()

    def _collect_counts(self, frame: pd.DataFrame) -> tuple[np.ndarray, list[set[str]]]:
        counts = np.zeros(self.num_zones, dtype=float)
        company_sets = [set() for _ in range(self.num_zones)]
        if frame.empty:
            return counts, company_sets

        for row in frame[["업체명", "기초대비사정률"]].itertuples(index=False):
            zone_idx = self._clamp_zone(float(row[1]))
            if zone_idx is None:
                continue
            counts[zone_idx] += 1
            company = str(row[0] or "").strip()
            if company:
                company_sets[zone_idx].add(company)
        return counts, company_sets

    def _build_fixed_anchor_map(self) -> list[list[dict]]:
        anchors = [[] for _ in range(self.num_zones)]
        for bidder in self.fixed_bidders:
            rate = bidder.get("main_zone_avg")
            zone_idx = self._clamp_zone(float(rate)) if rate is not None else None
            if zone_idx is None:
                continue
            for offset in range(-self.fixed_anchor_search_zone_count, self.fixed_anchor_search_zone_count + 1):
                target_idx = zone_idx + offset
                if not (0 <= target_idx < self.num_zones):
                    continue
                distance = abs(offset) * self.zone_size
                anchors[target_idx].append(
                    {
                        "company": bidder.get("company", ""),
                        "rate": float(rate),
                        "distance": round(distance, 5),
                        "fixed_score": float(bidder.get("fixed_score", bidder.get("concentration", 0))),
                    }
                )
        return anchors

    def analyze(self) -> list[dict]:
        if self.df.empty:
            self.results = []
            return self.results

        work = self.df.copy()
        work["기초대비사정률"] = pd.to_numeric(work["기초대비사정률"], errors="coerce")
        work = work[(work["기초대비사정률"] >= self.min_rate) & (work["기초대비사정률"] <= self.max_rate)].copy()
        if work.empty:
            self.results = []
            return self.results

        if "투찰일시" in work.columns:
            work["투찰일시"] = pd.to_datetime(work["투찰일시"], errors="coerce")
            anchor_time = work["투찰일시"].dropna().max()
            if pd.isna(anchor_time):
                anchor_time = datetime.now()
        else:
            anchor_time = datetime.now()
        self.df = work

        recent_frame = self._window_frame(anchor_time, self.window_days["recent"])
        short_frame = self._window_frame(anchor_time, self.window_days["short"])
        medium_frame = self._window_frame(anchor_time, self.window_days["medium"])
        historical_frame = self._historical_frame(
            anchor_time,
            recent_days=self.window_days["historical_recent"],
            lookback_days=self.window_days["historical_lookback"],
        )

        recent_counts, recent_companies = self._collect_counts(recent_frame)
        short_counts, short_companies = self._collect_counts(short_frame)
        medium_counts, medium_companies = self._collect_counts(medium_frame)
        historical_counts, historical_companies = self._collect_counts(historical_frame)

        recent_unique = np.array([len(x) for x in recent_companies], dtype=float)
        short_unique = np.array([len(x) for x in short_companies], dtype=float)
        medium_unique = np.array([len(x) for x in medium_companies], dtype=float)

        weighted_count = (
            recent_counts * float(self.weighted_count_weights.get("recent", 0.50))
            + short_counts * float(self.weighted_count_weights.get("short", 0.30))
            + medium_counts * float(self.weighted_count_weights.get("medium", 0.20))
        )
        weighted_unique = (
            recent_unique * float(self.weighted_count_weights.get("recent", 0.50))
            + short_unique * float(self.weighted_count_weights.get("short", 0.30))
            + medium_unique * float(self.weighted_count_weights.get("medium", 0.20))
        )

        normalized_recent = recent_counts
        normalized_historical = historical_counts * (
            float(self.window_days["historical_recent"]) / max(float(self.window_days["historical_lookback"] - self.window_days["historical_recent"]), 1.0)
        )
        growth_raw = np.maximum(0.0, normalized_recent - normalized_historical)
        newcomer_count = np.array(
            [len(recent_companies[i] - historical_companies[i]) for i in range(self.num_zones)],
            dtype=float,
        )
        newcomer_ratio = np.divide(
            newcomer_count,
            np.maximum(recent_unique, 1.0),
            out=np.zeros_like(newcomer_count, dtype=float),
            where=np.maximum(recent_unique, 1.0) > 0,
        )

        count_score = self._safe_normalize(weighted_count)
        unique_score = self._safe_normalize(weighted_unique)
        growth_score = self._safe_normalize(growth_raw)
        density_score = (
            (count_score * float(self.density_weights.get("bid_count", 0.60)))
            + (unique_score * float(self.density_weights.get("company_count", 0.40)))
        ) * 100.0
        overheat_score = np.clip(
            (
                growth_score * float(self.overheat_weights.get("growth", 0.55))
                + newcomer_ratio * float(self.overheat_weights.get("newcomer_ratio", 0.45))
            )
            * 100.0,
            0.0,
            100.0,
        )

        anchor_map = self._build_fixed_anchor_map()
        results: list[dict] = []
        for zone_idx in range(self.num_zones):
            zone_start = self._zone_rate(zone_idx)
            zone_end = round(zone_start + self.zone_size, 5)
            anchors = sorted(anchor_map[zone_idx], key=lambda x: (x["distance"], -x["fixed_score"]))
            nearest_anchor = anchors[0] if anchors else None
            fixed_neighbor_score = 0.0
            if nearest_anchor:
                proximity = max(0.0, 1.0 - (nearest_anchor["distance"] / self.fixed_anchor_max_gap))
                fixed_neighbor_score = min(100.0, nearest_anchor["fixed_score"] * proximity)

            results.append(
                {
                    "zone": f"{zone_start:.3f}~{zone_end:.3f}%",
                    "zone_start": zone_start,
                    "zone_end": zone_end,
                    "zone_center": round(zone_start + (self.zone_size / 2.0), 5),
                    "recent_bid_count": int(recent_counts[zone_idx]),
                    "recent_company_count": int(recent_unique[zone_idx]),
                    "short_bid_count": int(short_counts[zone_idx]),
                    "medium_bid_count": int(medium_counts[zone_idx]),
                    "density_score": round(float(density_score[zone_idx]), 2),
                    "overheat_score": round(float(overheat_score[zone_idx]), 2),
                    "growth_score": round(float(growth_score[zone_idx] * 100.0), 2),
                    "newcomer_count": int(newcomer_count[zone_idx]),
                    "newcomer_ratio": round(float(newcomer_ratio[zone_idx]), 4),
                    "fixed_neighbor_score": round(float(fixed_neighbor_score), 2),
                    "nearest_fixed_company": nearest_anchor["company"] if nearest_anchor else "",
                    "nearest_fixed_distance": nearest_anchor["distance"] if nearest_anchor else None,
                    "nearest_fixed_rate": nearest_anchor["rate"] if nearest_anchor else None,
                }
            )

        self.results = results
        print(f"[CompetitionProfile] 경쟁 프로파일 생성: {len(self.results)}개 구간")
        return self.results

    def to_dataframe(self) -> pd.DataFrame:
        return pd.DataFrame(self.results)
