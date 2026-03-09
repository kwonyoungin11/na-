"""Backwards-compatible wrapper for conditional-advantage payload composition."""

from __future__ import annotations

from typing import Any

from conditional_advantage_strategy import build_conditional_advantage_payload


def compose_conditional_advantage_summary(
    *,
    crowding_summary: dict[str, Any] | None,
    collision_summary: dict[str, Any] | None,
    shadow_strategy: dict[str, Any] | None,
    rate_window_rows: int = 0,
    anchor_rows: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    crowding_summary = crowding_summary or {}
    collision_summary = collision_summary or {}
    shadow_strategy = shadow_strategy or {}

    payload = build_conditional_advantage_payload(
        crowding_summary,
        collision_summary,
        competition_payload={},
        gap_payload={},
        shadow_payload=shadow_strategy,
        anchor_rows=anchor_rows,
    )

    crowded_lift = _as_float((crowding_summary.get("key_findings") or {}).get("crowded_band_lift"))
    anchor_count = len(anchor_rows or [])
    compression_level = _classify_backwards_compatible_compression(crowded_lift, rate_window_rows, anchor_count)
    payload.setdefault("system_view", {})
    payload["system_view"]["compression_level"] = compression_level
    payload["system_view"]["predictability"] = (
        "medium" if compression_level == "medium" else "low" if compression_level == "high" else "guarded"
    )

    top_views = crowding_summary.get("top_views") if isinstance(crowding_summary.get("top_views"), dict) else {}
    market_view = payload.setdefault("market_view", {})
    if not market_view.get("forbidden_zones"):
        market_view["forbidden_zones"] = _bands_to_zones(top_views.get("underperforming_bands") or [], decision="forbidden")
    if not market_view.get("boundary_zones"):
        market_view["boundary_zones"] = _bands_to_zones(top_views.get("crowded_bands") or [], decision="boundary")
    return payload


def _classify_backwards_compatible_compression(crowded_lift: float, rate_window_rows: int, anchor_count: int) -> str:
    if crowded_lift >= 1.75 or (crowded_lift >= 1.55 and anchor_count >= 3):
        return "high"
    if crowded_lift >= 1.25 or rate_window_rows >= 400 or anchor_count >= 1:
        return "medium"
    return "low"


def _as_float(value: Any) -> float:
    try:
        if value is None or value == "":
            return 0.0
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def _bands_to_zones(rows: list[dict[str, Any]], *, decision: str) -> list[dict[str, Any]]:
    zones: list[dict[str, Any]] = []
    for row in rows:
        try:
            band_start = float(row["band_start"])
            band_end = float(row["band_end"])
        except (KeyError, TypeError, ValueError):
            continue
        zones.append(
            {
                "decision": decision,
                "band": [band_start, band_end],
                "reason_codes": ["EXCESS_DENSITY"] if decision == "forbidden" else ["UNCERTAINTY_HIGH"],
            }
        )
    return zones
