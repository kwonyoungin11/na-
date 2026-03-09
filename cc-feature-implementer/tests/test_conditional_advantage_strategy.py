from __future__ import annotations

from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "cc-feature-implementer" / "src"
SCRIPT = SRC / "conditional_advantage_strategy.py"


def _load_module():
    sys.path.insert(0, str(SRC))
    spec = spec_from_file_location("conditional_advantage_strategy", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {SCRIPT}")
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_build_conditional_advantage_payload_classifies_zones_and_candidates() -> None:
    mod = _load_module()

    crowding_summary = {
        "key_findings": {
            "crowded_band_lift": 1.42,
            "sparse_band_lift": 1.11,
        }
    }
    collision_summary = {
        "key_findings": {
            "wide_left_space_lift": 2.8,
        }
    }
    competition_profile = {
        "zones": [
            {
                "zone_start": 99.736,
                "zone_end": 99.737,
                "density_score": 32.0,
                "overheat_score": 24.0,
                "nearest_fixed_company": "앵커A",
            },
            {
                "zone_start": 99.748,
                "zone_end": 99.749,
                "density_score": 81.0,
                "overheat_score": 74.0,
                "nearest_fixed_company": "앵커B",
            },
        ]
    }
    gap_analysis = {
        "gaps": [
            {
                "zone_start": 99.736,
                "nearest_right_company": "앵커A",
                "nearest_right_distance": 0.002,
                "nearest_left_distance": 0.006,
                "density_score": 32.0,
                "overheat_score": 24.0,
                "opportunity_score": 68.0,
            },
            {
                "zone_start": 99.748,
                "nearest_right_company": "앵커B",
                "nearest_right_distance": 0.001,
                "nearest_left_distance": 0.0008,
                "density_score": 81.0,
                "overheat_score": 74.0,
                "opportunity_score": 21.0,
            },
        ]
    }
    shadow_strategy = {
        "summary": {"fixed_bidders": 4},
        "recommendations": [
            {
                "priority": 1,
                "source_company": "소스A",
                "target_company": "앵커A",
                "anchor_company": "앵커A",
                "recommended_rate": 99.7365,
                "band_min": 99.736,
                "band_max": 99.739,
                "left_gap": 0.006,
                "left_boundary_distance": 0.2365,
                "nearest_right_company": "앵커A",
                "nearest_right_distance": 0.002,
                "density_penalty": 32.0,
                "overheat_penalty": 24.0,
                "gap_score": 68.0,
                "priority_score": 74.0,
            }
        ],
    }
    anchors = [
        {
            "right_anchor_company": "앵커A",
            "표본수": 18,
            "평균좌측여유폭": 0.006,
            "평균우측앵커간격": 0.002,
            "낙찰리프트": 2.1,
        }
    ]

    payload = mod.build_conditional_advantage_payload(
        crowding_summary,
        collision_summary,
        competition_profile,
        gap_analysis,
        shadow_strategy,
        anchors,
    )

    assert payload["system_view"]["compression_level"] == "high"
    assert payload["summary"]["candidate_band_count"] == 1
    assert payload["summary"]["forbidden_zone_count"] == 1
    candidate = payload["decision_view"]["candidate_bands"][0]
    assert candidate["anchor"] == "앵커A"
    assert candidate["target_company"] == "앵커A"
    assert candidate["source_company"] == "소스A"
    assert candidate["left_space_width"] == 0.006
    assert candidate["left_boundary_distance"] == 0.2365
    assert "LEFT_SPACE_WIDE" in candidate["why"]
    forbidden = payload["market_view"]["forbidden_zones"][0]
    assert forbidden["band"] == [99.748, 99.749]
    assert "EXCESS_DENSITY" in forbidden["reason_codes"]


def test_build_conditional_advantage_payload_respects_configured_decision_rules() -> None:
    mod = _load_module()

    competition_profile = {
        "zones": [
            {
                "zone_start": 99.748,
                "zone_end": 99.749,
                "density_score": 81.0,
                "overheat_score": 74.0,
                "nearest_fixed_company": "앵커B",
            }
        ]
    }
    gap_analysis = {
        "gaps": [
            {
                "zone_start": 99.748,
                "nearest_right_company": "앵커B",
                "nearest_right_distance": 0.001,
                "nearest_left_distance": 0.0008,
                "density_score": 81.0,
                "overheat_score": 74.0,
                "opportunity_score": 21.0,
            }
        ]
    }

    payload = mod.build_conditional_advantage_payload(
        {},
        {},
        competition_profile,
        gap_analysis,
        {"summary": {"fixed_bidders": 1}, "recommendations": []},
        decision_rules={
            "forbidden_density_score": 90.0,
            "forbidden_overheat_score": 90.0,
        },
    )

    assert payload["summary"]["forbidden_zone_count"] == 0
    assert payload["summary"]["boundary_zone_count"] == 1
