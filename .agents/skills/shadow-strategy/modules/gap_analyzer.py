#!/usr/bin/env python3
"""공백 지속성 분석 모듈.

최근 붐비는 자리나 낙찰 다발 구간이 아니라,
고정업체 인접 구간 중에서도 혼잡도와 신규유입이 낮은 공백을 찾는다.
"""

from __future__ import annotations

import pandas as pd


class GapAnalyzer:
    """경쟁 밀도 기반 공백 지속성 분석기."""

    def __init__(
        self,
        competition_profiles: list[dict],
        fixed_bidders: list[dict],
        *,
        zone_size: float = 0.001,
        min_rate: float = 99.5,
        max_rate: float = 100.0,
        max_anchor_gap: float = 0.020,
        profile: dict | None = None,
    ) -> None:
        rate_profile = (profile or {}).get("rate_range", {})
        gap_profile = (profile or {}).get("gap", {})
        self.competition_profiles = sorted(competition_profiles, key=lambda x: x.get("zone_start", 0))
        self.fixed_bidders = fixed_bidders
        self.zone_size = float(rate_profile.get("zone_size", zone_size))
        self.min_rate = float(rate_profile.get("min", min_rate))
        self.max_rate = float(rate_profile.get("max", max_rate))
        self.max_anchor_gap = float(gap_profile.get("max_anchor_gap", max_anchor_gap))
        self.results: list[dict] = []
        self.left_anchor_discount = float(gap_profile.get("left_anchor_discount", 0.70))
        self.persistence_weights = gap_profile.get("persistence_weights", {"density": 0.65, "overheat": 0.35})
        self.opportunity_weights = gap_profile.get("opportunity_weights", {"persistence": 0.55, "anchor": 0.35, "calm": 0.10})
        self.no_anchor_multiplier = float(gap_profile.get("no_anchor_multiplier", 0.40))

    def _find_nearest_fixed(self, rate: float, *, direction: str) -> dict | None:
        candidates = []
        for bidder in self.fixed_bidders:
            fixed_rate = float(bidder.get("main_zone_avg", 0))
            distance = fixed_rate - rate
            if direction == "right" and distance <= 0:
                continue
            if direction == "left" and distance >= 0:
                continue
            abs_distance = abs(distance)
            if abs_distance > self.max_anchor_gap:
                continue
            candidates.append(
                {
                    "company": bidder.get("company", ""),
                    "rate": fixed_rate,
                    "distance": round(abs_distance, 5),
                    "fixed_score": float(bidder.get("fixed_score", bidder.get("concentration", 0))),
                }
            )
        if not candidates:
            return None
        return sorted(candidates, key=lambda x: (x["distance"], -x["fixed_score"]))[0]

    def analyze(self) -> list[dict]:
        results: list[dict] = []
        for profile in self.competition_profiles:
            zone_start = float(profile["zone_start"])
            zone_end = float(profile["zone_end"])
            zone_center = float(profile.get("zone_center", zone_start + (self.zone_size / 2)))
            density_score = float(profile.get("density_score", 0))
            overheat_score = float(profile.get("overheat_score", 0))
            newcomer_ratio = float(profile.get("newcomer_ratio", 0))

            right_anchor = self._find_nearest_fixed(zone_center, direction="right")
            left_anchor = self._find_nearest_fixed(zone_center, direction="left")

            anchor_score = 0.0
            if right_anchor:
                proximity = max(0.0, 1.0 - (right_anchor["distance"] / self.max_anchor_gap))
                anchor_score = max(anchor_score, right_anchor["fixed_score"] * proximity)
            if left_anchor:
                proximity = max(0.0, 1.0 - (left_anchor["distance"] / self.max_anchor_gap))
                anchor_score = max(anchor_score, left_anchor["fixed_score"] * proximity * self.left_anchor_discount)

            persistence_score = max(
                0.0,
                min(
                    100.0,
                    100.0
                    - (density_score * float(self.persistence_weights.get("density", 0.65)))
                    - (overheat_score * float(self.persistence_weights.get("overheat", 0.35))),
                ),
            )
            calm_score = max(0.0, min(100.0, 100.0 - (newcomer_ratio * 100.0)))
            opportunity_score = (
                persistence_score * float(self.opportunity_weights.get("persistence", 0.55))
                + anchor_score * float(self.opportunity_weights.get("anchor", 0.35))
                + calm_score * float(self.opportunity_weights.get("calm", 0.10))
            )
            if not right_anchor and not left_anchor:
                opportunity_score *= self.no_anchor_multiplier

            results.append(
                {
                    "zone": profile["zone"],
                    "zone_start": round(zone_start, 5),
                    "zone_end": round(zone_end, 5),
                    "zone_center": round(zone_center, 5),
                    "density_score": round(density_score, 2),
                    "overheat_score": round(overheat_score, 2),
                    "persistence_score": round(persistence_score, 2),
                    "anchor_score": round(anchor_score, 2),
                    "opportunity_score": round(max(0.0, min(100.0, opportunity_score)), 2),
                    "nearest_right_company": right_anchor["company"] if right_anchor else "",
                    "nearest_right_distance": right_anchor["distance"] if right_anchor else None,
                    "nearest_left_company": left_anchor["company"] if left_anchor else "",
                    "nearest_left_distance": left_anchor["distance"] if left_anchor else None,
                    "has_left_anchor": left_anchor is not None,
                    "left_boundary_distance": round(max(0.0, zone_center - self.min_rate), 5),
                }
            )

        self.results = sorted(results, key=lambda x: x["zone_start"])
        print(f"[GapAnalyzer] 공백 지속성 분석 완료: {len(self.results)}개 구간")
        return self.results

    def get_results(self) -> list[dict]:
        return self.results

    def to_dataframe(self) -> pd.DataFrame:
        return pd.DataFrame(self.results)

    def get_best_opportunities(self, top_n: int = 5) -> list[dict]:
        return sorted(self.results, key=lambda x: x.get("opportunity_score", 0), reverse=True)[:top_n]

    def find_gap_at_position(self, target_rate: float) -> dict | None:
        for row in self.results:
            if row["zone_start"] <= target_rate < row["zone_end"]:
                return row
        return None
