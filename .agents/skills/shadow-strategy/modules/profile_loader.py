#!/usr/bin/env python3
"""Analysis profile loader for competition-only strategy."""

from __future__ import annotations

import json
from copy import deepcopy
from pathlib import Path
from typing import Any


MODULE_DIR = Path(__file__).resolve().parent
SKILL_DIR = MODULE_DIR.parent
DEFAULT_ANALYSIS_PROFILE_PATH = SKILL_DIR / "analysis_profile.json"
DEFAULT_API_RULE_PROFILE_PATH = SKILL_DIR.parent / "API" / "rule_profile.json"


def _deep_merge(base: dict[str, Any], override: dict[str, Any]) -> dict[str, Any]:
    merged = deepcopy(base)
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(merged.get(key), dict):
            merged[key] = _deep_merge(merged[key], value)
        else:
            merged[key] = value
    return merged


DEFAULT_ANALYSIS_PROFILE: dict[str, Any] = {
    "profile_name": "competition-only-analysis-default",
    "rate_range": {"min": 99.5, "max": 100.0, "zone_size": 0.001},
    "same_day_collapse": {
        "enabled": True,
        "rate_tolerance": 0.003,
        "min_cluster_size": 2,
        "primary_date_column": "개찰일시",
        "fallback_date_column": "투찰일시",
    },
    "period_days": {"all": 0, "1m": 30, "3m": 90, "6m": 180, "1y": 365, "2y": 730},
    "cli_defaults": {
        "shadow_period": "2y",
        "pipeline_period": "6m",
        "pipeline_region": "전국",
        "output_format": "all",
        "my_company": "동국문화재연구원",
    },
    "fixed_bidder": {
        "min_bids": 5,
        "concentration_threshold": 40.0,
        "window_days": {"recent": 30, "short": 90, "medium": 180},
        "consistency_weights": {"concentration": 0.25, "medium": 0.25, "short": 0.25, "recent": 0.25},
        "score_weights": {"concentration": 0.45, "consistency": 0.35, "recency_presence": 0.2, "volatility_penalty": 0.15},
        "recency_multiplier": 2.0,
        "volatility_reference": 0.02,
    },
    "trend": {"direction_threshold": 0.006, "recent_sample_size": 8},
    "competition": {
        "window_days": {"recent": 30, "short": 90, "medium": 180, "historical_recent": 30, "historical_lookback": 180},
        "weighted_count_weights": {"recent": 0.5, "short": 0.3, "medium": 0.2},
        "density_weights": {"bid_count": 0.6, "company_count": 0.4},
        "overheat_weights": {"growth": 0.55, "newcomer_ratio": 0.45},
        "fixed_anchor_search_zone_count": 20,
        "fixed_anchor_max_gap": 0.02,
    },
    "gap": {
        "max_anchor_gap": 0.02,
        "left_anchor_discount": 0.7,
        "persistence_weights": {"density": 0.65, "overheat": 0.35},
        "opportunity_weights": {"persistence": 0.55, "anchor": 0.35, "calm": 0.1},
        "no_anchor_multiplier": 0.4,
    },
    "shadow": {
        "base_offset": 0.001,
        "offset_std_multiplier": 0.6,
        "offset_std_cap": 0.002,
        "downward_velocity_multiplier": 12.0,
        "downward_velocity_cap": 0.002,
        "upward_offset_multiplier": 0.8,
        "min_offset": 0.001,
        "band_std_multiplier": 1.5,
        "band_base_half": 0.001,
        "band_min_half": 0.001,
        "band_max_half": 0.004,
        "volatility_reference": 0.015,
        "movement_scores": {"하향조정": 85.0, "유지": 70.0, "상향조정": 55.0, "알수없음": 50.0},
        "positive_weights": {"fixed": 0.35, "gap": 0.35, "anchor": 0.15, "movement": 0.15},
        "negative_weights": {"density": 0.45, "overheat": 0.35, "volatility": 0.2},
        "evidence_bonus": 30.0,
        "minimum_priority_score": 35.0,
        "dedupe_precision": 3,
    },
    "decision_rules": {
        "forbidden_density_score": 65.0,
        "forbidden_overheat_score": 60.0,
        "candidate_max_density_score": 45.0,
        "candidate_max_overheat_score": 45.0,
        "candidate_min_opportunity_score": 55.0,
        "min_left_space_width": 0.002,
        "boundary_clear_min_distance": 0.002,
        "wide_left_space_anchor_multiplier": 1.05,
        "narrow_left_space_anchor_multiplier": 0.75,
        "narrow_left_space_min_width": 0.001,
        "calm_band_max_density_score": 45.0,
        "calm_band_max_overheat_score": 45.0,
        "low_newcomer_overheat_score": 35.0,
        "less_obvious_density_score": 35.0,
        "uncertainty_high_density_score": 60.0,
        "uncertainty_high_overheat_score": 60.0,
        "uncertainty_medium_density_score": 45.0,
        "uncertainty_medium_overheat_score": 45.0,
        "uncertainty_min_opportunity_score": 45.0,
        "uncertainty_high_volatility_penalty": 45.0,
        "uncertainty_medium_volatility_penalty": 25.0,
    },
    "desktop": {
        "fixed_windows": {
            "all": {"min_bids": 6, "concentration_threshold": 0.52},
            "1y": {"min_bids": 5, "concentration_threshold": 0.53},
            "6m": {"min_bids": 4, "concentration_threshold": 0.54},
            "3m": {"min_bids": 3, "concentration_threshold": 0.55},
        },
        "minimum_support_notices": 3,
        "minimum_award_notices": 1,
        "nearby_radius_rate": 0.01,
        "band_threshold_min": 18.0,
        "band_threshold_ratio": 0.82,
        "max_band_expand_steps": 4,
        "evidence_weights": {"shadow": 0.45, "gap": 0.35, "competition": 0.20},
        "fallback_weights": {"gap": 0.55, "competition": 0.30, "support": 0.15},
    },
}


def _load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def load_analysis_profile(profile_path: str | Path | None = None) -> tuple[dict[str, Any], Path]:
    path = Path(profile_path) if profile_path else DEFAULT_ANALYSIS_PROFILE_PATH
    return _deep_merge(DEFAULT_ANALYSIS_PROFILE, _load_json(path)), path


def load_api_rule_profile(profile_path: str | Path | None = None) -> tuple[dict[str, Any], Path]:
    path = Path(profile_path) if profile_path else DEFAULT_API_RULE_PROFILE_PATH
    return _load_json(path), path
