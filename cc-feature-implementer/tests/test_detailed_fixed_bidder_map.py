from __future__ import annotations

import importlib.util
from pathlib import Path
import sys

import pandas as pd


SCRIPT_PATH = Path("/mnt/a/c/.agents/skills/fixed-bidder-map/scripts/detailed_map.py")


def _load_module():
    spec = importlib.util.spec_from_file_location("detailed_fixed_bidder_map", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_apply_company_exclusions_removes_requested_variants() -> None:
    mod = _load_module()
    df = pd.DataFrame(
        [
            {"업체명정규화": "동국문화재연구원", "기초대비사정률": 99.61},
            {"업체명정규화": "재동국문화재연구원", "기초대비사정률": 99.62},
            {"업체명정규화": "금오문화재연구원", "기초대비사정률": 99.87},
        ]
    )

    filtered = mod.apply_company_exclusions(df, ["동국문화재연구원"])

    assert filtered["업체명정규화"].tolist() == ["금오문화재연구원"]


def test_cross_period_summary_uses_median_based_stability_fields() -> None:
    mod = _load_module()

    period_frames = {
        "12m": pd.DataFrame([{"company": "가", "sample_count": 10, "median_rate": 99.61, "std_rate": 0.01, "legacy_fixed_candidate": True, "detailed_fixed_candidate": True, "micro_fixed_candidate": False}]),
        "6m": pd.DataFrame([{"company": "가", "sample_count": 8, "median_rate": 99.62, "std_rate": 0.02, "legacy_fixed_candidate": True, "detailed_fixed_candidate": True, "micro_fixed_candidate": False}]),
        "3m": pd.DataFrame([{"company": "가", "sample_count": 6, "median_rate": 99.60, "std_rate": 0.03, "legacy_fixed_candidate": False, "detailed_fixed_candidate": True, "micro_fixed_candidate": False}]),
        "1m": pd.DataFrame([{"company": "가", "sample_count": 5, "median_rate": 99.61, "std_rate": 0.01, "legacy_fixed_candidate": False, "detailed_fixed_candidate": False, "micro_fixed_candidate": False}]),
    }

    summary = mod.build_cross_period_summary(period_frames)

    row = summary.iloc[0]
    assert row["company"] == "가"
    assert bool(row["all_periods_present"]) is True
    assert "cross_period_center_mean" not in summary.columns
    assert row["cross_period_center_std"] is not None
    assert row["cross_period_center_mad"] is not None
