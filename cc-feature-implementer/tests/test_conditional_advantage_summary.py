from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from conditional_advantage_summary import compose_conditional_advantage_summary


def test_compose_conditional_advantage_summary_builds_skill_views() -> None:
    summary = compose_conditional_advantage_summary(
        crowding_summary={
            "key_findings": {
                "crowded_band_lift": 1.5,
                "sparse_band_lift": 2.1,
            },
            "top_views": {
                "crowded_bands": [
                    {"band_start": 99.742, "band_end": 99.743, "bid_count": 32, "win_lift": 0.91},
                    {"band_start": 99.743, "band_end": 99.744, "bid_count": 31, "win_lift": 0.95},
                ],
                "underperforming_bands": [
                    {"band_start": 99.742, "band_end": 99.743, "bid_count": 32, "win_lift": 0.91},
                ],
                "overperforming_bands": [
                    {"band_start": 99.736, "band_end": 99.737, "bid_count": 11, "win_lift": 1.34},
                ],
                "sparse_bands": [
                    {"band_start": 99.731, "band_end": 99.732, "bid_count": 4, "win_lift": 1.12},
                ],
            },
        },
        collision_summary={
            "key_findings": {
                "wide_left_space_lift": 3.2,
                "high_left_advantage_lift": 4.1,
                "best_matrix_cell": {
                    "우측앵커거리구간": "가까움",
                    "좌측여유구간": "넓음",
                },
            },
        },
        shadow_strategy={
            "recommendations": [
                {
                    "target_company": "화랑문화재연구원",
                    "recommended_rate": 99.742,
                    "band_min": 99.739,
                    "band_max": 99.746,
                    "gap_score": 71.8,
                    "crowd_avoidance_score": 95.4,
                    "evidence_score": 61.35,
                }
            ]
        },
        rate_window_rows=640,
        anchor_rows=[
            {
                "CompanyName": "화랑문화재연구원",
                "평균좌측여유폭": 0.008,
                "평균우측앵커간격": 0.002,
            }
        ],
    )

    assert summary["system_view"]["compression_level"] in {"medium", "high"}
    assert summary["system_view"]["predictability"] in {"medium", "low"}
    assert "forbidden_zones" in summary["market_view"]
    assert "boundary_zones" in summary["market_view"]
    assert summary["relation_view"]["primary_anchor"]["company"] == "화랑문화재연구원"
    assert summary["summary"]["candidate_band_count"] == 1
    candidate = summary["decision_view"]["candidate_bands"][0]
    assert candidate["anchor"] == "화랑문화재연구원"
    assert candidate["band"] == [99.739, 99.746]
    assert candidate["uncertainty"] in {"medium", "high"}
