from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import math
import sys

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "cc-feature-implementer" / "src"
SCRIPT = SRC / "analyze_collision_left_space.py"


def _load_module():
    sys.path.insert(0, str(SRC))
    spec = spec_from_file_location("analyze_collision_left_space", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {SCRIPT}")
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_build_collision_frame_tracks_anchor_and_left_space() -> None:
    mod = _load_module()

    df = pd.DataFrame(
        [
            {"공고번호": "A", "순위": -1, "업체명": "탈락", "기초대비사정률": 99.500, "사정률": 99.600, "개찰일시": "2026-01-01"},
            {"공고번호": "A", "순위": 1, "업체명": "낙찰", "기초대비사정률": 99.510, "사정률": 99.600, "개찰일시": "2026-01-01"},
            {"공고번호": "A", "순위": 2, "업체명": "앵커", "기초대비사정률": 99.515, "사정률": 99.600, "개찰일시": "2026-01-01"},
            {"공고번호": "A", "순위": 3, "업체명": "후행", "기초대비사정률": 99.530, "사정률": 99.600, "개찰일시": "2026-01-01"},
        ]
    )

    all_complete, prepared = mod.prepare_complete_notice_rows(df)
    assert prepared["metadata"]["complete_notice_count"] == 1
    assert prepared["metadata"]["negative_rank_excluded_row_count"] == 1

    collision = mod.build_collision_frame(all_complete)
    winner = collision.loc[collision["업체명"] == "낙찰"].iloc[0]

    assert winner["right_anchor_company"] == "앵커"
    assert math.isclose(float(winner["right_anchor_gap"]), 0.005, rel_tol=1e-9)
    assert math.isclose(float(winner["left_space_gap_all"]), 0.010, rel_tol=1e-9)
    assert math.isclose(float(winner["left_space_advantage"]), 0.005, rel_tol=1e-9)


def test_summaries_reflect_left_space_advantage() -> None:
    mod = _load_module()

    collision = pd.DataFrame(
        [
            {"공고번호": "A", "순위": 1, "is_winner": True, "참여업체수": 4, "left_space_gap_all": 0.030, "right_anchor_gap": 0.010, "left_space_advantage": 0.020, "expected_win_probability": 0.25, "right_anchor_company": "X", "사정률": 99.6, "기초대비사정률": 99.51},
            {"공고번호": "A", "순위": 2, "is_winner": False, "참여업체수": 4, "left_space_gap_all": 0.010, "right_anchor_gap": 0.020, "left_space_advantage": -0.010, "expected_win_probability": 0.25, "right_anchor_company": "Y", "사정률": 99.6, "기초대비사정률": 99.52},
            {"공고번호": "B", "순위": 1, "is_winner": True, "참여업체수": 4, "left_space_gap_all": 0.040, "right_anchor_gap": 0.010, "left_space_advantage": 0.030, "expected_win_probability": 0.25, "right_anchor_company": "X", "사정률": 99.7, "기초대비사정률": 99.61},
            {"공고번호": "B", "순위": 2, "is_winner": False, "참여업체수": 4, "left_space_gap_all": 0.015, "right_anchor_gap": 0.030, "left_space_advantage": -0.015, "expected_win_probability": 0.25, "right_anchor_company": "Z", "사정률": 99.7, "기초대비사정률": 99.63},
            {"공고번호": "C", "순위": 2, "is_winner": False, "참여업체수": 4, "left_space_gap_all": 0.008, "right_anchor_gap": 0.025, "left_space_advantage": -0.017, "expected_win_probability": 0.25, "right_anchor_company": "Y", "사정률": 99.8, "기초대비사정률": 99.71},
            {"공고번호": "C", "순위": 3, "is_winner": False, "참여업체수": 4, "left_space_gap_all": 0.012, "right_anchor_gap": 0.035, "left_space_advantage": -0.023, "expected_win_probability": 0.25, "right_anchor_company": "Z", "사정률": 99.8, "기초대비사정률": 99.74},
        ]
    )

    left_summary = mod.summarize_left_space_tiers(collision)
    advantage_summary = mod.summarize_advantage_tiers(collision)

    wide_lift = float(left_summary.set_index("좌측여유구간").loc["넓음", "낙찰리프트"])
    low_adv_lift = float(advantage_summary.set_index("좌측우위구간").loc["낮음", "낙찰리프트"])
    high_adv_lift = float(advantage_summary.set_index("좌측우위구간").loc["높음", "낙찰리프트"])

    assert wide_lift > 1.0
    assert high_adv_lift > low_adv_lift


def test_apply_rate_window_filters_target_zone_only() -> None:
    mod = _load_module()

    collision = pd.DataFrame(
        [
            {"공고번호": "A", "순위": 1, "is_winner": True, "참여업체수": 4, "left_space_gap_all": 0.030, "right_anchor_gap": 0.010, "left_space_advantage": 0.020, "expected_win_probability": 0.25, "right_anchor_company": "X", "사정률": 99.6, "기초대비사정률": 99.510},
            {"공고번호": "B", "순위": 2, "is_winner": False, "참여업체수": 4, "left_space_gap_all": 0.010, "right_anchor_gap": 0.020, "left_space_advantage": -0.010, "expected_win_probability": 0.25, "right_anchor_company": "Y", "사정률": 99.6, "기초대비사정률": 100.120},
        ]
    )

    filtered, resolved_range = mod.apply_rate_window(collision, 99.5, 100.0)

    assert resolved_range == (99.5, 100.0)
    assert filtered["공고번호"].tolist() == ["A"]
