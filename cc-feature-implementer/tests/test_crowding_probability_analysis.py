from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import math
import sys

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "cc-feature-implementer" / "src"
SCRIPT = SRC / "analyze_crowding_probability.py"


def _load_module():
    sys.path.insert(0, str(SRC))
    spec = spec_from_file_location("analyze_crowding_probability", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {SCRIPT}")
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_band_probability_uses_notice_aware_expected_wins() -> None:
    mod = _load_module()

    df = pd.DataFrame(
        [
            {"공고번호": "A", "순위": 1, "업체명": "가", "기초대비사정률": 99.5001, "사정률": 99.6001, "개찰일시": "2026-01-01"},
            {"공고번호": "A", "순위": 2, "업체명": "나", "기초대비사정률": 99.5011, "사정률": 99.6001, "개찰일시": "2026-01-01"},
            {"공고번호": "B", "순위": 2, "업체명": "다", "기초대비사정률": 99.5002, "사정률": 99.6002, "개찰일시": "2026-01-02"},
            {"공고번호": "B", "순위": 1, "업체명": "라", "기초대비사정률": 99.5007, "사정률": 99.6002, "개찰일시": "2026-01-02"},
            {"공고번호": "B", "순위": 3, "업체명": "마", "기초대비사정률": 99.5012, "사정률": 99.6002, "개찰일시": "2026-01-02"},
            {"공고번호": "B", "순위": 4, "업체명": "바", "기초대비사정률": 99.5021, "사정률": 99.6002, "개찰일시": "2026-01-02"},
        ]
    )

    prepared_df, prepared_info = mod.prepare_notice_level(df)
    assert prepared_info["metadata"]["complete_notice_count"] == 2

    band_df = mod.add_rate_band(prepared_df, 0.001, 99.5, 100.0)
    band_table = mod.build_band_probability_table(band_df)

    first_band = band_table.loc[band_table["band_label"] == "99.500~99.501"].iloc[0]
    assert int(first_band["bid_count"]) == 3
    assert int(first_band["winner_count"]) == 2
    assert math.isclose(float(first_band["expected_wins"]), 1.0, rel_tol=1e-9)
    assert math.isclose(float(first_band["actual_win_rate"]), 2 / 3, rel_tol=1e-9)
    assert math.isclose(float(first_band["expected_win_rate"]), 1 / 3, rel_tol=1e-9)
    assert math.isclose(float(first_band["win_lift"]), 2.0, rel_tol=1e-9)


def test_prepare_notice_level_excludes_notices_without_single_winner() -> None:
    mod = _load_module()

    df = pd.DataFrame(
        [
            {"공고번호": "A", "순위": 1, "업체명": "가", "기초대비사정률": 99.5001, "사정률": 99.6001, "개찰일시": "2026-01-01"},
            {"공고번호": "A", "순위": 2, "업체명": "나", "기초대비사정률": 99.5011, "사정률": 99.6001, "개찰일시": "2026-01-01"},
            {"공고번호": "C", "순위": 2, "업체명": "다", "기초대비사정률": 99.5021, "사정률": 99.6003, "개찰일시": "2026-01-03"},
            {"공고번호": "C", "순위": 3, "업체명": "라", "기초대비사정률": 99.5031, "사정률": 99.6003, "개찰일시": "2026-01-03"},
        ]
    )

    prepared_df, prepared_info = mod.prepare_notice_level(df)

    assert prepared_info["metadata"]["all_notice_count"] == 2
    assert prepared_info["metadata"]["complete_notice_count"] == 1
    assert prepared_info["metadata"]["excluded_notice_count"] == 1
    assert prepared_df["공고번호"].nunique() == 1
    assert set(prepared_df["공고번호"]) == {"A"}


def test_prepare_notice_level_overwrites_existing_participant_count_column() -> None:
    mod = _load_module()

    df = pd.DataFrame(
        [
            {"공고번호": "A", "순위": 1, "업체명": "가", "기초대비사정률": 99.5001, "사정률": 99.6001, "개찰일시": "2026-01-01", "참여업체수": None},
            {"공고번호": "A", "순위": 2, "업체명": "나", "기초대비사정률": 99.5011, "사정률": 99.6001, "개찰일시": "2026-01-01", "참여업체수": None},
            {"공고번호": "B", "순위": 1, "업체명": "다", "기초대비사정률": 99.5021, "사정률": 99.6002, "개찰일시": "2026-01-02", "참여업체수": 99},
            {"공고번호": "B", "순위": 2, "업체명": "라", "기초대비사정률": 99.5031, "사정률": 99.6002, "개찰일시": "2026-01-02", "참여업체수": 99},
            {"공고번호": "B", "순위": 3, "업체명": "마", "기초대비사정률": 99.5041, "사정률": 99.6002, "개찰일시": "2026-01-02", "참여업체수": 99},
        ]
    )

    prepared_df, _ = mod.prepare_notice_level(df)

    a_counts = prepared_df.loc[prepared_df["공고번호"] == "A", "참여업체수"].unique().tolist()
    b_counts = prepared_df.loc[prepared_df["공고번호"] == "B", "참여업체수"].unique().tolist()
    assert a_counts == [2]
    assert b_counts == [3]
