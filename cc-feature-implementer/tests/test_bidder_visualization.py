from __future__ import annotations

import importlib.util
import json
from pathlib import Path

import pandas as pd


def _load_visualize_module():
    script_path = (
        Path(__file__).resolve().parents[2]
        / ".agents"
        / "skills"
        / "bidder-visualization"
        / "scripts"
        / "visualize.py"
    )
    spec = importlib.util.spec_from_file_location("bidder_visualize", script_path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def test_load_color_map_supports_nested_color_objects(tmp_path: Path):
    module = _load_visualize_module()
    color_map_path = tmp_path / "bidder_colors.json"
    color_map_path.write_text(
        json.dumps(
            {
                "동국문화재연구원": {"id": 1, "color": "#FF0000"},
                "가람문화재연구원": "#00FF00",
            },
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )

    loaded = module.load_color_map(color_map_path)

    assert loaded["동국문화재연구원"] == "#FF0000"
    assert loaded["가람문화재연구원"] == "#00FF00"


def test_filter_visualization_rows_keeps_my_company_and_applies_period_filter():
    module = _load_visualize_module()
    df = pd.DataFrame(
        {
            "업체명": [
                "동국문화재연구원",
                "한국문화재연구원",
                "효림문화재연구원",
                "제주문화재연구원",
            ],
            "개찰일시": [
                "2026-03-01 10:00",
                "2026-03-01 10:00",
                "2026-02-20 10:00",
                "2025-10-01 10:00",
            ],
        }
    )
    df["개찰dt"] = pd.to_datetime(df["개찰일시"])

    filtered = module.filter_visualization_rows(df, period="1m")

    remaining = filtered["업체명정규화"].tolist()
    assert "동국문화재연구원" in remaining
    assert all("한국문화재연구원" not in name for name in remaining)
    assert all("효림" not in name for name in remaining)
    assert "제주문화재연구원" not in remaining


def test_resolve_default_cell_px_keeps_001_chart_large():
    module = _load_visualize_module()

    assert module.resolve_default_cell_px(0.01, 120) == 64.0
    assert module.resolve_default_cell_px(0.001, 10) == 64.0
    assert module.resolve_default_cell_px(0.001, 50) == 40.0
    assert module.resolve_default_cell_px(0.001, 120) == 32.0


def test_collapse_visualization_rows_merges_same_company_same_day_nearby_points():
    module = _load_visualize_module()
    df = pd.DataFrame(
        {
            "공고번호": ["N1", "N2", "N3", "N4"],
            "업체명": ["금오문화재연구원", "금오문화재연구원", "금오문화재연구원", "다른연구원"],
            "순위": [1, 2, 3, 1],
            "기초대비사정률": [99.82367, 99.82369, 99.82392, 99.70012],
            "개찰일시": [
                "2025-02-11 10:00",
                "2025-02-11 11:00",
                "2025-02-11 12:00",
                "2025-02-11 10:00",
            ],
        }
    )
    df["개찰dt"] = pd.to_datetime(df["개찰일시"])

    collapsed, stats = module.collapse_visualization_rows(df)

    assert stats["input_rows"] == 4
    assert stats["output_rows"] == 2
    assert round(float(collapsed.iloc[0]["기초대비사정률"]), 5) == 99.82369
    assert int(collapsed.iloc[0]["동일일자묶음수"]) == 3
