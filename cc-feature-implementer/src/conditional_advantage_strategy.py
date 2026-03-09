"""Build conditional-advantage views for multi-reserve-price analysis outputs."""

from __future__ import annotations

import csv
import math
from pathlib import Path
from typing import Any


DEFAULT_DECISION_RULES: dict[str, float] = {
    "forbidden_density_score": 65.0,
    "forbidden_overheat_score": 60.0,
    "candidate_max_density_score": 45.0,
    "candidate_max_overheat_score": 45.0,
    "candidate_min_opportunity_score": 55.0,
    "min_left_space_width": 0.002,
    "boundary_clear_min_distance": 0.002,
    "wide_left_space_anchor_multiplier": 1.05,
    "calm_band_max_density_score": 45.0,
    "calm_band_max_overheat_score": 45.0,
    "low_newcomer_overheat_score": 35.0,
    "less_obvious_density_score": 35.0,
    "uncertainty_high_density_score": 60.0,
    "uncertainty_high_overheat_score": 60.0,
    "uncertainty_medium_density_score": 45.0,
    "uncertainty_medium_overheat_score": 45.0,
    "uncertainty_min_opportunity_score": 45.0,
}


def load_anchor_summary(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def build_conditional_advantage_payload(
    crowding_summary_payload: dict[str, Any],
    collision_summary_payload: dict[str, Any],
    competition_payload: dict[str, Any],
    gap_payload: dict[str, Any],
    shadow_payload: dict[str, Any],
    anchor_rows: list[dict[str, Any]] | None = None,
    decision_rules: dict[str, Any] | None = None,
) -> dict[str, Any]:
    rules = _resolve_decision_rules(decision_rules)
    competition_zones = list(competition_payload.get("zones") or [])
    gap_rows = list(gap_payload.get("gaps") or [])
    recommendations = list(shadow_payload.get("recommendations") or [])
    anchors = list(anchor_rows or [])

    gap_by_zone = {
        _round_rate(row.get("zone_start")): row
        for row in gap_rows
        if _round_rate(row.get("zone_start")) is not None
    }

    forbidden_zones: list[dict[str, Any]] = []
    boundary_zones: list[dict[str, Any]] = []

    for zone in competition_zones:
        zone_start = _round_rate(zone.get("zone_start"))
        if zone_start is None:
            continue
        gap = gap_by_zone.get(zone_start, {})
        decision = _build_zone_decision(zone, gap, rules)
        if decision["decision"] == "forbidden":
            forbidden_zones.append(decision)
        elif decision["decision"] == "boundary":
            boundary_zones.append(decision)

    forbidden_zones.sort(
        key=lambda row: (
            -_as_float(row.get("density_score")),
            -_as_float(row.get("overheat_score")),
            row.get("band", [0.0])[0],
        )
    )
    boundary_zones.sort(
        key=lambda row: (
            _decision_priority(row),
            row.get("band", [0.0])[0],
        )
    )

    candidate_bands = [
        _build_candidate_band(
            row,
            gap_by_zone.get(_round_rate(row.get("supporting_gap_zone_start")) or _round_rate(row.get("band_min"))),
            rules,
        )
        for row in recommendations
    ]
    candidate_bands = [row for row in candidate_bands if row]
    candidate_bands.sort(key=lambda row: (row["priority"], row["band"][0]))

    if not candidate_bands:
        candidate_bands = [
            _zone_to_candidate_band(row, index + 1)
            for index, row in enumerate(boundary_zones)
            if row.get("anchor")
            and _as_float(row.get("left_space_width")) >= _as_float(row.get("anchor_gap_left"))
        ][:5]

    crowded_lift = _nested_float(crowding_summary_payload, "key_findings", "crowded_band_lift")
    wide_left_lift = _nested_float(collision_summary_payload, "key_findings", "wide_left_space_lift")
    fixed_bidder_count = int((shadow_payload.get("summary") or {}).get("fixed_bidders") or 0)
    compression_level = _classify_compression(crowded_lift, fixed_bidder_count)
    predictability = "low" if compression_level == "high" else "medium" if compression_level == "medium" else "guarded"

    primary_anchor = _build_primary_anchor(candidate_bands, anchors)
    top_candidate = candidate_bands[0] if candidate_bands else None

    payload = {
        "system_view": {
            "compression_level": compression_level,
            "predictability": predictability,
            "crowded_band_lift": crowded_lift,
            "wide_left_space_lift": wide_left_lift,
            "left_space_requirement": top_candidate.get("left_space_width") if top_candidate else None,
        },
        "market_view": {
            "forbidden_zones": forbidden_zones[:12],
            "boundary_zones": boundary_zones[:12],
        },
        "relation_view": {
            "primary_anchor": primary_anchor,
            "collision_anchors": [_normalize_anchor_row(row) for row in anchors[:5]],
        },
        "decision_view": {
            "candidate_bands": candidate_bands[:10],
        },
    }
    payload["summary"] = {
        "forbidden_zone_count": len(forbidden_zones),
        "boundary_zone_count": len(boundary_zones),
        "candidate_band_count": len(candidate_bands),
        "primary_anchor": primary_anchor.get("company", ""),
        "top_candidate": top_candidate,
    }
    return payload
def _build_zone_decision(zone: dict[str, Any], gap: dict[str, Any] | None, rules: dict[str, float]) -> dict[str, Any]:
    gap = gap or {}
    density = _as_float(zone.get("density_score"))
    overheat = _as_float(zone.get("overheat_score"))
    opportunity = _as_float(gap.get("opportunity_score"))
    anchor = str(gap.get("nearest_right_company") or zone.get("nearest_fixed_company") or "").strip()
    anchor_gap = _round_rate(gap.get("nearest_right_distance"))
    left_space_width = _round_rate(gap.get("nearest_left_distance"))
    left_boundary_distance = _round_rate(gap.get("left_boundary_distance"))
    reason_codes: list[str] = []

    if density >= rules["forbidden_density_score"]:
        reason_codes.append("EXCESS_DENSITY")
    if overheat >= rules["forbidden_overheat_score"]:
        reason_codes.append("OVERHEAT")
    if anchor:
        reason_codes.append("LEFT_OF_COLLISION_ANCHOR")
    if _is_left_space_wide(left_space_width, anchor_gap, rules):
        reason_codes.append("LEFT_SPACE_WIDE")
    elif left_space_width is None:
        reason_codes.append("LEFT_SPACE_UNVERIFIED")
        if _as_float(left_boundary_distance) >= rules["boundary_clear_min_distance"]:
            reason_codes.append("LEFT_BOUNDARY_CLEAR")
    else:
        reason_codes.append("LEFT_SPACE_TOO_NARROW")
    if density <= rules["calm_band_max_density_score"] and overheat <= rules["calm_band_max_overheat_score"]:
        reason_codes.append("CALM_BAND")
    if overheat <= rules["low_newcomer_overheat_score"]:
        reason_codes.append("LOW_NEWCOMER_RATIO")

    if density >= rules["forbidden_density_score"] or overheat >= rules["forbidden_overheat_score"]:
        decision = "forbidden"
    elif (
        anchor
        and _is_left_space_wide(left_space_width, anchor_gap, rules)
        and opportunity >= rules["candidate_min_opportunity_score"]
        and density <= rules["candidate_max_density_score"]
        and overheat <= rules["candidate_max_overheat_score"]
    ):
        decision = "candidate"
        if "LESS_OBVIOUS" not in reason_codes:
            reason_codes.append("LESS_OBVIOUS")
    else:
        decision = "boundary"
        if _uncertainty_level(anchor, left_space_width, anchor_gap, density, overheat, opportunity, rules) == "high":
            reason_codes.append("UNCERTAINTY_HIGH")

    return {
        "decision": decision,
        "band": [_round_rate(zone.get("zone_start")), _round_rate(zone.get("zone_end"))],
        "anchor": anchor,
        "anchor_gap_left": anchor_gap,
        "left_space_width": left_space_width,
        "left_boundary_distance": left_boundary_distance,
        "left_space_source": "left_anchor" if left_space_width is not None else "boundary",
        "density_score": density,
        "overheat_score": overheat,
        "opportunity_score": opportunity,
        "reason_codes": reason_codes,
    }


def _build_candidate_band(
    recommendation: dict[str, Any],
    gap: dict[str, Any] | None = None,
    rules: dict[str, float] | None = None,
) -> dict[str, Any] | None:
    rules = rules or DEFAULT_DECISION_RULES
    band_min = _round_rate(recommendation.get("band_min"))
    band_max = _round_rate(recommendation.get("band_max"))
    if band_min is None or band_max is None:
        return None

    gap = gap or {}
    anchor = str(
        recommendation.get("anchor_company")
        or recommendation.get("nearest_right_company")
        or gap.get("nearest_right_company")
        or recommendation.get("target_company")
        or recommendation.get("source_company")
        or ""
    ).strip()
    anchor_gap = _round_rate(
        recommendation.get("anchor_gap_left")
        or recommendation.get("nearest_right_distance")
        or gap.get("nearest_right_distance")
    )
    left_space_width = _round_rate(
        recommendation.get("left_space_width")
        or recommendation.get("true_left_space_width")
        or recommendation.get("left_gap")
        or gap.get("nearest_left_distance")
    )
    left_boundary_distance = _round_rate(recommendation.get("left_boundary_distance") or gap.get("left_boundary_distance"))
    density = _as_float(recommendation.get("density_penalty") or gap.get("density_score"))
    overheat = _as_float(recommendation.get("overheat_penalty") or gap.get("overheat_score"))
    opportunity = _as_float(recommendation.get("gap_score") or gap.get("opportunity_score"))
    uncertainty = _uncertainty_level(anchor, left_space_width, anchor_gap, density, overheat, opportunity, rules)

    why: list[str] = []
    if anchor:
        why.append("LEFT_OF_COLLISION_ANCHOR")
    if _is_left_space_wide(left_space_width, anchor_gap, rules):
        why.append("LEFT_SPACE_WIDE")
    elif left_space_width is None:
        why.append("LEFT_SPACE_UNVERIFIED")
        if _as_float(left_boundary_distance) >= rules["boundary_clear_min_distance"]:
            why.append("LEFT_BOUNDARY_CLEAR")
    else:
        why.append("LEFT_SPACE_TOO_NARROW")
    if density <= rules["calm_band_max_density_score"] and overheat <= rules["calm_band_max_overheat_score"]:
        why.append("CALM_BAND")
    if overheat <= rules["low_newcomer_overheat_score"]:
        why.append("LOW_NEWCOMER_RATIO")
    if density <= rules["less_obvious_density_score"]:
        why.append("LESS_OBVIOUS")
    if uncertainty == "high":
        why.append("UNCERTAINTY_HIGH")

    return {
        "band": [band_min, band_max],
        "anchor": anchor,
        "anchor_gap_left": anchor_gap,
        "left_space_width": left_space_width,
        "left_boundary_distance": left_boundary_distance,
        "left_space_source": "left_anchor" if left_space_width is not None else "boundary",
        "priority": int(recommendation.get("priority") or 0),
        "uncertainty": uncertainty,
        "recommended_rate": _round_rate(recommendation.get("recommended_rate")),
        "priority_score": _as_float(recommendation.get("priority_score")),
        "source_company": str(recommendation.get("source_company") or "").strip(),
        "target_company": str(recommendation.get("target_company") or "").strip(),
        "reason_codes": list(why),
        "why": why,
    }


def _zone_to_candidate_band(zone: dict[str, Any], priority: int) -> dict[str, Any]:
    return {
        "band": list(zone.get("band") or []),
        "anchor": zone.get("anchor", ""),
        "anchor_gap_left": zone.get("anchor_gap_left"),
        "left_space_width": zone.get("left_space_width"),
        "left_boundary_distance": zone.get("left_boundary_distance"),
        "left_space_source": zone.get("left_space_source"),
        "priority": priority,
        "uncertainty": "high",
        "recommended_rate": None,
        "priority_score": _as_float(zone.get("opportunity_score")),
        "reason_codes": list(zone.get("reason_codes") or []),
        "why": list(zone.get("reason_codes") or []),
    }


def _build_primary_anchor(candidate_bands: list[dict[str, Any]], anchors: list[dict[str, Any]]) -> dict[str, Any]:
    if candidate_bands and candidate_bands[0].get("anchor"):
        return {
            "company": candidate_bands[0]["anchor"],
            "anchor_gap_left": candidate_bands[0].get("anchor_gap_left"),
            "left_space_width": candidate_bands[0].get("left_space_width"),
        }

    if anchors:
        row = _normalize_anchor_row(anchors[0])
        return {
            "company": row.get("company", ""),
            "sample_count": row.get("sample_count"),
            "left_space_width": row.get("average_left_space_gap"),
            "anchor_gap_left": row.get("average_right_anchor_gap"),
        }

    return {"company": "", "anchor_gap_left": None, "left_space_width": None}


def _normalize_anchor_row(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "company": str(row.get("right_anchor_company") or row.get("CompanyName") or "").strip(),
        "sample_count": int(float(row.get("표본수") or row.get("SampleCount") or 0)),
        "average_left_space_gap": _round_rate(row.get("평균좌측여유폭") or row.get("AverageLeftSpaceGap")),
        "average_right_anchor_gap": _round_rate(row.get("평균우측앵커간격") or row.get("AverageRightAnchorGap")),
        "win_lift": _as_float(row.get("낙찰리프트") or row.get("WinLift")),
    }


def _decision_priority(row: dict[str, Any]) -> tuple[float, float, float]:
    return (
        -_as_float(row.get("opportunity_score")),
        -_as_float(row.get("left_space_width")),
        _as_float(row.get("density_score")),
    )


def _classify_compression(crowded_lift: float | None, fixed_bidder_count: int) -> str:
    crowded = crowded_lift or 0.0
    if crowded >= 1.35 or fixed_bidder_count >= 5:
        return "high"
    if crowded >= 1.10 or fixed_bidder_count >= 3:
        return "medium"
    return "low"


def _uncertainty_level(
    anchor: str,
    left_space_width: float | None,
    anchor_gap: float | None,
    density: float,
    overheat: float,
    opportunity: float,
    rules: dict[str, float],
) -> str:
    if (
        not anchor
        or density >= rules["uncertainty_high_density_score"]
        or overheat >= rules["uncertainty_high_overheat_score"]
        or opportunity < rules["uncertainty_min_opportunity_score"]
    ):
        return "high"
    if left_space_width is None or anchor_gap is None:
        return "high"
    if (
        left_space_width < max(anchor_gap, rules["min_left_space_width"])
        or density >= rules["uncertainty_medium_density_score"]
        or overheat >= rules["uncertainty_medium_overheat_score"]
    ):
        return "medium"
    return "low"


def _is_left_space_wide(left_space_width: float | None, anchor_gap: float | None, rules: dict[str, float]) -> bool:
    if left_space_width is None:
        return False
    if anchor_gap is None:
        return left_space_width >= rules["min_left_space_width"]
    return left_space_width >= max(anchor_gap * rules["wide_left_space_anchor_multiplier"], rules["min_left_space_width"])


def _resolve_decision_rules(decision_rules: dict[str, Any] | None) -> dict[str, float]:
    resolved = dict(DEFAULT_DECISION_RULES)
    for key, default_value in DEFAULT_DECISION_RULES.items():
        parsed = _maybe_float((decision_rules or {}).get(key))
        resolved[key] = parsed if parsed is not None else default_value
    return resolved


def _nested_float(payload: dict[str, Any], parent: str, child: str) -> float | None:
    node = payload.get(parent)
    if not isinstance(node, dict):
        return None
    return _round_rate(node.get(child), digits=4)


def _round_rate(value: Any, digits: int = 5) -> float | None:
    parsed = _maybe_float(value)
    if parsed is None:
        return None
    return round(parsed, digits)


def _as_float(value: Any) -> float:
    parsed = _maybe_float(value)
    return parsed if parsed is not None else 0.0


def _maybe_float(value: Any) -> float | None:
    if value is None or value == "":
        return None
    try:
        parsed = float(value)
    except (TypeError, ValueError):
        return None
    if math.isnan(parsed) or math.isinf(parsed):
        return None
    return parsed
