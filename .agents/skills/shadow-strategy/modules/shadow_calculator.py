#!/usr/bin/env python3
"""경쟁업체 전용 추천 계산 모듈.

낙찰 빈도/확률을 사용하지 않고 고정성, 공백 지속성, 과열 회피만으로
추천 위치를 계산한다.
"""

from __future__ import annotations

from typing import Iterable

import pandas as pd


class ShadowCalculator:
    """경쟁업체 행동 기반 가림위치 계산기."""

    def __init__(
        self,
        fixed_bidders: list[dict],
        trends: list[dict],
        gaps: list[dict],
        competition_profiles: list[dict],
        my_company: str | None = None,
        profile: dict | None = None,
    ):
        rate_profile = (profile or {}).get("rate_range", {})
        shadow_profile = (profile or {}).get("shadow", {})
        self.fixed_bidders = fixed_bidders
        self.trends = {t["company"]: t for t in trends} if trends else {}
        self.gaps = gaps
        self.competition_profiles = competition_profiles
        self.results: list[dict] = []
        self.min_rate = float(rate_profile.get("min", 99.5))
        self.max_rate = float(rate_profile.get("max", 100.0))
        self.shadow_profile = shadow_profile
        self.decision_rules = (profile or {}).get("decision_rules", {})
        self.my_company = str(my_company or "").strip()

    @staticmethod
    def _normalize_company_text(value: str) -> str:
        return str(value or "").replace(" ", "").strip()

    def _is_my_company(self, company: str) -> bool:
        if not self.my_company:
            return False
        return self._normalize_company_text(company) == self._normalize_company_text(self.my_company)

    def _find_gap(self, target_rate: float) -> dict | None:
        for gap in self.gaps:
            if gap["zone_start"] <= target_rate < gap["zone_end"]:
                return gap
        return None

    def _clamp_rate(self, value: float) -> float:
        return round(max(self.min_rate, min(self.max_rate, value)), 5)

    def _build_reason_list(
        self,
        bidder: dict,
        trend_info: dict,
        gap_info: dict,
        fixed_score: float,
        movement_score: float,
        volatility_penalty: float,
        left_space_width: float | None,
        left_boundary_distance: float,
    ) -> list[str]:
        reasons: list[str] = []
        if fixed_score >= 75:
            reasons.append(f"고정성 높음({fixed_score:.1f})")
        if gap_info.get("persistence_score", 0) >= 70:
            reasons.append(f"공백 지속성 양호({gap_info['persistence_score']:.1f})")
        if gap_info.get("density_score", 100) <= 35:
            reasons.append(f"혼잡도 낮음({gap_info['density_score']:.1f})")
        if gap_info.get("overheat_score", 100) <= 30:
            reasons.append(f"신규유입 낮음({gap_info['overheat_score']:.1f})")
        if trend_info.get("direction") == "하향조정":
            reasons.append("고정업체 최근 하향 이동")
        elif trend_info.get("direction") == "유지":
            reasons.append("고정업체 위치 유지")
        if volatility_penalty >= 45:
            reasons.append(f"변동성 주의({volatility_penalty:.1f})")
        if gap_info.get("nearest_right_company"):
            reasons.append(f"우측 고정업체 {gap_info['nearest_right_company']}")
        if left_space_width is None and left_boundary_distance > 0:
            reasons.append(f"좌측 실제앵커 없음(경계 {left_boundary_distance:.5f})")
        if movement_score <= 50 and trend_info.get("direction") == "상향조정":
            reasons.append("우측 이동 중이라 보수적 접근 필요")
        return reasons[:4] or ["경쟁업체 공백 회피 기준 기본 추천"]

    @staticmethod
    def _build_reason_codes(
        *,
        has_anchor: bool,
        anchor_gap_left: float,
        left_space_width: float | None,
        left_boundary_distance: float,
        density_penalty: float,
        overheat_penalty: float,
        volatility_penalty: float,
        decision_rules: dict,
    ) -> list[str]:
        codes: list[str] = []
        min_left_space_width = float(decision_rules.get("min_left_space_width", 0.002))
        boundary_clear_min_distance = float(decision_rules.get("boundary_clear_min_distance", min_left_space_width))
        wide_left_space_anchor_multiplier = float(decision_rules.get("wide_left_space_anchor_multiplier", 1.05))
        narrow_left_space_anchor_multiplier = float(decision_rules.get("narrow_left_space_anchor_multiplier", 0.75))
        narrow_left_space_min_width = float(decision_rules.get("narrow_left_space_min_width", 0.001))
        forbidden_density_score = float(decision_rules.get("forbidden_density_score", 65.0))
        forbidden_overheat_score = float(decision_rules.get("forbidden_overheat_score", 60.0))
        less_obvious_density_score = float(decision_rules.get("less_obvious_density_score", 35.0))
        low_newcomer_overheat_score = float(decision_rules.get("low_newcomer_overheat_score", 35.0))
        uncertainty_high_volatility_penalty = float(decision_rules.get("uncertainty_high_volatility_penalty", 45.0))
        if has_anchor:
            codes.append("LEFT_OF_COLLISION_ANCHOR")
        if left_space_width is None:
            codes.append("LEFT_SPACE_UNVERIFIED")
            if left_boundary_distance >= boundary_clear_min_distance:
                codes.append("LEFT_BOUNDARY_CLEAR")
        elif left_space_width >= max(anchor_gap_left * wide_left_space_anchor_multiplier, min_left_space_width):
            codes.append("LEFT_SPACE_WIDE")
        elif left_space_width <= max(anchor_gap_left * narrow_left_space_anchor_multiplier, narrow_left_space_min_width):
            codes.append("LEFT_SPACE_TOO_NARROW")
        if density_penalty >= forbidden_density_score:
            codes.append("EXCESS_DENSITY")
        elif density_penalty <= less_obvious_density_score:
            codes.append("LESS_OBVIOUS")
        if overheat_penalty >= forbidden_overheat_score:
            codes.append("OVERHEAT")
        elif overheat_penalty <= low_newcomer_overheat_score:
            codes.append("CALM_BAND")
        if volatility_penalty >= uncertainty_high_volatility_penalty:
            codes.append("UNCERTAINTY_HIGH")
        return codes

    @staticmethod
    def _classify_left_space(left_space_width: float | None, anchor_gap_left: float, left_boundary_distance: float, decision_rules: dict) -> str:
        min_left_space_width = float(decision_rules.get("min_left_space_width", 0.002))
        wide_left_space_anchor_multiplier = float(decision_rules.get("wide_left_space_anchor_multiplier", 1.2))
        narrow_left_space_anchor_multiplier = float(decision_rules.get("narrow_left_space_anchor_multiplier", 0.75))
        if left_space_width is None:
            return "좌측앵커없음" if left_boundary_distance > 0 else "미확인"
        if left_space_width >= max(anchor_gap_left * wide_left_space_anchor_multiplier, min_left_space_width * 2.0):
            return "넓음"
        if left_space_width >= max(anchor_gap_left * narrow_left_space_anchor_multiplier, min_left_space_width):
            return "중간"
        return "좁음"

    @staticmethod
    def _classify_uncertainty(density_penalty: float, overheat_penalty: float, volatility_penalty: float, decision_rules: dict) -> str:
        if (
            density_penalty >= float(decision_rules.get("uncertainty_high_density_score", 60.0))
            or overheat_penalty >= float(decision_rules.get("uncertainty_high_overheat_score", 60.0))
            or volatility_penalty >= float(decision_rules.get("uncertainty_high_volatility_penalty", 45.0))
        ):
            return "high"
        if (
            density_penalty >= float(decision_rules.get("uncertainty_medium_density_score", 45.0))
            or overheat_penalty >= float(decision_rules.get("uncertainty_medium_overheat_score", 45.0))
            or volatility_penalty >= float(decision_rules.get("uncertainty_medium_volatility_penalty", 25.0))
        ):
            return "medium"
        return "low"

    def calculate(self) -> list[dict]:
        results: list[dict] = []
        for bidder in self.fixed_bidders:
            company = bidder["company"]
            if self._is_my_company(company):
                continue
            trend_info = self.trends.get(company, {})
            base_position = trend_info.get("recent_median") or trend_info.get("recent_avg") or bidder.get("main_zone_avg")
            if base_position is None:
                continue
            base_position = float(base_position)

            direction = trend_info.get("direction", "알수없음")
            velocity = abs(float(trend_info.get("velocity") or 0.0))
            recent_std = float(trend_info.get("recent_std") or bidder.get("std_rate") or 0.0)

            offset = float(self.shadow_profile.get("base_offset", 0.001)) + min(
                float(self.shadow_profile.get("offset_std_cap", 0.002)),
                recent_std * float(self.shadow_profile.get("offset_std_multiplier", 0.60)),
            )
            if direction == "하향조정":
                offset += min(
                    float(self.shadow_profile.get("downward_velocity_cap", 0.002)),
                    velocity * float(self.shadow_profile.get("downward_velocity_multiplier", 12.0)),
                )
            elif direction == "상향조정":
                offset = max(
                    float(self.shadow_profile.get("min_offset", 0.001)),
                    offset * float(self.shadow_profile.get("upward_offset_multiplier", 0.80)),
                )

            candidate_rate = self._clamp_rate(base_position - offset)
            band_half = max(
                float(self.shadow_profile.get("band_min_half", 0.001)),
                min(
                    float(self.shadow_profile.get("band_max_half", 0.004)),
                    recent_std * float(self.shadow_profile.get("band_std_multiplier", 1.50))
                    + float(self.shadow_profile.get("band_base_half", 0.001)),
                ),
            )
            band_min = self._clamp_rate(candidate_rate - band_half)
            band_max = self._clamp_rate(candidate_rate + band_half)
            gap_info = self._find_gap(candidate_rate)
            if not gap_info:
                continue

            fixed_score = float(bidder.get("fixed_score", bidder.get("concentration", 0)))
            movement_score = self.shadow_profile.get(
                "movement_scores",
                {"하향조정": 85.0, "유지": 70.0, "상향조정": 55.0, "알수없음": 50.0},
            ).get(direction, 50.0)
            volatility_penalty = min(100.0, (recent_std / float(self.shadow_profile.get("volatility_reference", 0.015))) * 100.0)
            density_penalty = float(gap_info.get("density_score", 0.0))
            overheat_penalty = float(gap_info.get("overheat_score", 0.0))
            gap_score = float(gap_info.get("opportunity_score", 0.0))
            anchor_score = float(gap_info.get("anchor_score", 0.0))

            positive_weights = self.shadow_profile.get("positive_weights", {"fixed": 0.35, "gap": 0.35, "anchor": 0.15, "movement": 0.15})
            negative_weights = self.shadow_profile.get("negative_weights", {"density": 0.45, "overheat": 0.35, "volatility": 0.20})
            positive = (
                fixed_score * float(positive_weights.get("fixed", 0.35))
                + gap_score * float(positive_weights.get("gap", 0.35))
                + anchor_score * float(positive_weights.get("anchor", 0.15))
                + movement_score * float(positive_weights.get("movement", 0.15))
            )
            negative = (
                density_penalty * float(negative_weights.get("density", 0.45))
                + overheat_penalty * float(negative_weights.get("overheat", 0.35))
                + volatility_penalty * float(negative_weights.get("volatility", 0.20))
            )
            evidence_score = max(
                0.0,
                min(100.0, positive - negative + float(self.shadow_profile.get("evidence_bonus", 30.0))),
            )
            if evidence_score < float(self.shadow_profile.get("minimum_priority_score", 35.0)):
                continue

            fixed_position = round(float(bidder.get("main_zone_avg", base_position)), 5)
            anchor_company = str(gap_info.get("nearest_right_company") or "").strip()
            target_company = anchor_company or company
            source_company = company
            anchor_gap_left = round(
                float(gap_info.get("nearest_right_distance") or max(0.0, fixed_position - candidate_rate)),
                5,
            )
            raw_left_space_width = gap_info.get("nearest_left_distance")
            left_space_width = round(float(raw_left_space_width), 5) if raw_left_space_width is not None else None
            left_boundary_distance = round(
                float(gap_info.get("left_boundary_distance") or max(0.0, candidate_rate - self.min_rate)),
                5,
            )
            left_space_label = self._classify_left_space(left_space_width, anchor_gap_left, left_boundary_distance, self.decision_rules)
            uncertainty = self._classify_uncertainty(density_penalty, overheat_penalty, volatility_penalty, self.decision_rules)
            reason_codes = self._build_reason_codes(
                has_anchor=bool(anchor_company),
                anchor_gap_left=anchor_gap_left,
                left_space_width=left_space_width,
                left_boundary_distance=left_boundary_distance,
                density_penalty=density_penalty,
                overheat_penalty=overheat_penalty,
                volatility_penalty=volatility_penalty,
                decision_rules=self.decision_rules,
            )
            reasons = self._build_reason_list(
                bidder,
                trend_info,
                gap_info,
                fixed_score,
                movement_score,
                volatility_penalty,
                left_space_width,
                left_boundary_distance,
            )

            results.append(
                {
                    "source_company": source_company,
                    "target_company": target_company,
                    "anchor_company": anchor_company,
                    "fixed_zone": bidder.get("main_zone", ""),
                    "fixed_position": fixed_position,
                    "current_trend": direction,
                    "recommended_rate": round(candidate_rate, 5),
                    "shadow_position": round(candidate_rate, 5),
                    "band_min": round(band_min, 5),
                    "band_max": round(band_max, 5),
                    "left_gap": left_space_width,
                    "anchor_gap_left": anchor_gap_left,
                    "left_space_width": left_space_width,
                    "left_boundary_distance": left_boundary_distance,
                    "left_space_source": "left_anchor" if left_space_width is not None else "boundary",
                    "left_space_label": left_space_label,
                    "fixed_score": round(fixed_score, 2),
                    "gap_score": round(gap_score, 2),
                    "anchor_score": round(anchor_score, 2),
                    "movement_score": round(movement_score, 2),
                    "density_penalty": round(density_penalty, 2),
                    "overheat_penalty": round(overheat_penalty, 2),
                    "volatility_penalty": round(volatility_penalty, 2),
                    "priority_score": round(evidence_score, 2),
                    "uncertainty": uncertainty,
                    "reason": ", ".join(reasons),
                    "reasons": reasons,
                    "reason_codes": reason_codes,
                    "nearest_right_company": anchor_company,
                    "nearest_right_distance": gap_info.get("nearest_right_distance"),
                    "supporting_gap_zone": gap_info.get("zone", ""),
                    "supporting_gap_zone_start": gap_info.get("zone_start"),
                    "supporting_gap_zone_end": gap_info.get("zone_end"),
                }
            )

        results.sort(key=lambda x: (x["priority_score"], x["fixed_score"], -x["density_penalty"]), reverse=True)

        deduped: list[dict] = []
        used_targets = set()
        for row in results:
            target_company = row["target_company"]
            rounded_rate = round(row["recommended_rate"], int(self.shadow_profile.get("dedupe_precision", 3)))
            key = (target_company, rounded_rate)
            if key in used_targets:
                continue
            used_targets.add(key)
            deduped.append(row)

        for index, row in enumerate(deduped, start=1):
            row["priority"] = index

        self.results = deduped
        print(f"[ShadowCalculator] 경쟁업체 기반 추천 계산 완료: {len(self.results)}개")
        return self.results

    def get_results(self) -> list[dict]:
        return self.results

    def to_dataframe(self) -> pd.DataFrame:
        return pd.DataFrame(
            [
                {
                    "우선순위": r.get("priority"),
                    "목표업체": r.get("target_company"),
                    "소스업체": r.get("source_company"),
                    "고정구간": r.get("fixed_zone"),
                    "추천위치": r.get("recommended_rate"),
                    "권장구간시작": r.get("band_min"),
                    "권장구간끝": r.get("band_max"),
                    "고정성점수": r.get("fixed_score"),
                    "공백점수": r.get("gap_score"),
                    "앵커점수": r.get("anchor_score"),
                    "이동점수": r.get("movement_score"),
                    "혼잡패널티": r.get("density_penalty"),
                    "과열패널티": r.get("overheat_penalty"),
                    "변동성패널티": r.get("volatility_penalty"),
                    "종합점수": r.get("priority_score"),
                    "추천이유": r.get("reason"),
                }
                for r in self.results
            ]
        )

    def get_top_recommendations(self, top_n: int = 10) -> list[dict]:
        return self.results[:top_n]
