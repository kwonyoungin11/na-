from __future__ import annotations

from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from tempfile import TemporaryDirectory
import json
import sys

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
SHADOW_SCRIPT = ROOT / ".agents" / "skills" / "shadow-strategy" / "scripts" / "analyze.py"
LOGIC_SCRIPT = ROOT / ".agents" / "skills" / "logic" / "scripts" / "analyze.py"
PROFILE_LOADER = ROOT / ".agents" / "skills" / "shadow-strategy" / "modules" / "profile_loader.py"
MODULE_DIR = ROOT / ".agents" / "skills" / "shadow-strategy" / "modules"
COMPANY_DAY_CLUSTER = ROOT / "cc-feature-implementer" / "src" / "company_day_cluster.py"


def _load_module(module_name: str, path: Path):
    spec = spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {path}")
    module = module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def _build_dataset() -> pd.DataFrame:
    rows: list[dict] = []
    base_date = pd.Timestamp("2025-01-01")

    for idx in range(60):
        notice = f"N{idx:04d}"
        dt = base_date + pd.Timedelta(days=idx * 3)
        rows.extend(
            [
                {
                    "공고번호": notice,
                    "업체명": "고정연구원A",
                    "기초대비사정률": 99.630 + ((idx % 3) * 0.0002),
                    "투찰일시": dt,
                    "순위": 2,
                    "지역제한": "전국",
                },
                {
                    "공고번호": notice,
                    "업체명": "보조연구원B",
                    "기초대비사정률": 99.615 + ((idx % 4) * 0.0003),
                    "투찰일시": dt,
                    "순위": 3,
                    "지역제한": "전국",
                },
                {
                    "공고번호": notice,
                    "업체명": "동국문화재연구원",
                    "기초대비사정률": 99.622 + ((idx % 2) * 0.0002),
                    "투찰일시": dt,
                    "순위": 1,
                    "지역제한": "전국",
                },
            ]
        )

    recent_base = pd.Timestamp("2025-06-20")
    for idx in range(15):
        notice = f"HOT{idx:03d}"
        dt = recent_base + pd.Timedelta(days=idx)
        for newcomer in range(4):
            rows.append(
                {
                    "공고번호": notice,
                    "업체명": f"신규업체{idx}_{newcomer}",
                    "기초대비사정률": 99.700 + (newcomer * 0.0002),
                    "투찰일시": dt,
                    "순위": newcomer + 2,
                    "지역제한": "전국",
                }
            )
        rows.append(
            {
                "공고번호": notice,
                "업체명": "고정연구원A",
                "기초대비사정률": 99.631,
                "투찰일시": dt,
                "순위": 5,
                "지역제한": "전국",
            }
        )

    return pd.DataFrame(rows)


def test_competition_profile_marks_recent_crowding_hotter_than_fixed_band() -> None:
    sys.path.insert(0, str(MODULE_DIR))
    fixed_mod = _load_module("fixed_bidder_mod", MODULE_DIR / "fixed_bidder.py")
    competition_mod = _load_module("competition_profile_mod", MODULE_DIR / "competition_profile.py")

    df = _build_dataset()
    fixed_bidders = fixed_mod.FixedBidderDetector(df, min_bids=5, concentration_threshold=40).detect()
    profiles = competition_mod.CompetitionProfileAnalyzer(df[df["업체명"] != "동국문화재연구원"], fixed_bidders).analyze()

    profile_630 = next(row for row in profiles if abs(row["zone_start"] - 99.630) < 0.0005)
    profile_700 = next(row for row in profiles if abs(row["zone_start"] - 99.700) < 0.0005)

    assert profile_700["overheat_score"] > profile_630["overheat_score"]
    assert profile_700["density_score"] > profile_630["density_score"]


def test_shadow_strategy_analyzer_excludes_my_company_from_market_view_and_writes_competition_outputs() -> None:
    mod = _load_module("shadow_strategy_analyze_mod", SHADOW_SCRIPT)
    df = _build_dataset()

    with TemporaryDirectory() as tmp_dir:
        tmp = Path(tmp_dir)
        data_path = tmp / "input.xlsx"
        out_dir = tmp / "shadow"
        df.to_excel(data_path, index=False)

        analyzer = mod.ShadowStrategyAnalyzer(data_path=str(data_path), output_dir=str(out_dir), my_company="동국문화재연구원")
        analyzer.run(period="1y", min_rate=99.5, max_rate=100.0)
        analyzer.save_results("all")

        assert analyzer.recommendations
        top = analyzer.recommendations[0]
        assert top["source_company"] != "동국문화재연구원"
        assert top["target_company"] == top["anchor_company"]
        assert analyzer.summary["self_rows_excluded"] == analyzer.summary["self_rows_matched"]
        assert analyzer.summary["self_rows_matched"] > 0
        assert analyzer.summary["self_company_policy"] == "excluded_from_market_structure"
        assert "expected_prob" not in top
        assert top["recommended_rate"] < top["fixed_position"]

        competition_payload = json.loads((out_dir / "competition_profile.json").read_text(encoding="utf-8"))
        probability_payload = json.loads((out_dir / "probability_table.json").read_text(encoding="utf-8"))
        shadow_payload = json.loads((out_dir / "shadow_strategy.json").read_text(encoding="utf-8"))
        assert competition_payload["zones"]
        assert probability_payload["deprecated"] is True
        assert shadow_payload["conditional_advantage"]["system_view"]["compression_level"] in {"low", "medium", "high"}
        assert shadow_payload["conditional_advantage"]["market_view"]["forbidden_zones"]
        assert shadow_payload["conditional_advantage"]["decision_view"]["candidate_bands"]
        top_candidate = shadow_payload["conditional_advantage"]["decision_view"]["candidate_bands"][0]
        assert top_candidate["anchor"]
        assert top_candidate["target_company"] == top_candidate["anchor"]
        assert "LEFT_OF_COLLISION_ANCHOR" in top_candidate["reason_codes"]
        assert (out_dir / "recommendation.md").exists()


def test_shadow_strategy_uses_profile_defaults_when_cli_values_missing() -> None:
    profile_loader_mod = _load_module("profile_loader_mod", PROFILE_LOADER)
    shadow_mod = _load_module("shadow_strategy_profile_mod", SHADOW_SCRIPT)
    cluster_mod = _load_module("company_day_cluster_profile_mod", COMPANY_DAY_CLUSTER)
    df = _build_dataset()

    with TemporaryDirectory() as tmp_dir:
        tmp = Path(tmp_dir)
        data_path = tmp / "input.xlsx"
        profile_path = tmp / "analysis_profile.json"
        df.to_excel(data_path, index=False)
        profile_path.write_text(
            json.dumps(
                {
                    "cli_defaults": {"my_company": "고정연구원A", "shadow_period": "1y"},
                    "rate_range": {"min": 99.5, "max": 100.0},
                },
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )

        profile, _ = profile_loader_mod.load_analysis_profile(profile_path)
        analyzer = shadow_mod.ShadowStrategyAnalyzer(data_path=str(data_path), output_dir=str(tmp / "shadow"), profile=profile)
        assert analyzer.my_company == "고정연구원A"

        cutoff = pd.Timestamp.now() - pd.Timedelta(days=365)
        filtered = df[pd.to_datetime(df["투찰일시"]) >= cutoff].copy()
        collapsed, _ = cluster_mod.collapse_same_company_day_clusters(
            filtered,
            company_col="업체명",
            rate_col="기초대비사정률",
            primary_date_col="개찰일시",
            fallback_date_col="투찰일시",
            rate_tolerance=0.003,
            min_cluster_size=2,
        )
        expected_matched = int(collapsed[collapsed["업체명"] == "고정연구원A"].shape[0])
        analyzer.run()

        assert analyzer.summary["period"] == "1y"
        assert analyzer.summary["self_rows_excluded"] == expected_matched
        assert analyzer.summary["self_rows_matched"] == expected_matched
        assert analyzer.summary["self_company_policy"] == "excluded_from_market_structure"


def test_shadow_strategy_collapses_same_company_same_day_nearby_rows_before_fixed_bidder_detection() -> None:
    mod = _load_module("shadow_strategy_same_day_mod", SHADOW_SCRIPT)
    rows = []
    for idx, rate in enumerate([99.6310, 99.6314, 99.6316, 99.6318, 99.6320, 99.6322], start=1):
        rows.append(
            {
                "공고번호": f"S{idx}",
                "업체명": "가람문화재연구원",
                "기초대비사정률": rate,
                "개찰일시": pd.Timestamp("2026-03-01 10:00") + pd.Timedelta(minutes=idx),
                "투찰일시": pd.Timestamp("2026-03-01 09:00") + pd.Timedelta(minutes=idx),
                "순위": 2,
                "지역제한": "전국",
            }
        )
    for idx, day in enumerate(["2026-03-02", "2026-03-03"], start=1):
        rows.append(
            {
                "공고번호": f"T{idx}",
                "업체명": "가람문화재연구원",
                "기초대비사정률": 99.6315 + (idx * 0.0001),
                "개찰일시": pd.Timestamp(f"{day} 10:00"),
                "투찰일시": pd.Timestamp(f"{day} 09:00"),
                "순위": 2,
                "지역제한": "전국",
            }
        )
    for idx, day in enumerate(["2026-03-01", "2026-03-02", "2026-03-03"], start=1):
        rows.append(
            {
                "공고번호": f"D{idx}",
                "업체명": "동국문화재연구원",
                "기초대비사정률": 99.6200 + (idx * 0.0001),
                "개찰일시": pd.Timestamp(f"{day} 10:30"),
                "투찰일시": pd.Timestamp(f"{day} 09:30"),
                "순위": 1,
                "지역제한": "전국",
            }
        )
    df = pd.DataFrame(rows)

    with TemporaryDirectory() as tmp_dir:
        tmp = Path(tmp_dir)
        data_path = tmp / "input.xlsx"
        out_dir = tmp / "shadow"
        profile = {
            "rate_range": {"min": 99.5, "max": 100.0},
            "same_day_collapse": {"enabled": True, "rate_tolerance": 0.003, "min_cluster_size": 2},
            "fixed_bidder": {"min_bids": 3, "concentration_threshold": 40.0},
        }
        df.to_excel(data_path, index=False)

        analyzer = mod.ShadowStrategyAnalyzer(
            data_path=str(data_path),
            output_dir=str(out_dir),
            my_company="동국문화재연구원",
            profile=profile,
        )
        analyzer.run(period="all", min_rate=99.5, max_rate=100.0)

        assert analyzer.summary["records"] == 3
        assert analyzer.summary["self_rows_excluded"] == 3
        fixed = next(item for item in analyzer.fixed_bidders if item["company"] == "가람문화재연구원")
        assert fixed["total_bids"] == 3


def test_logic_pipeline_skips_downstream_steps_when_region_has_no_rows(monkeypatch) -> None:
    mod = _load_module("logic_analyze_mod", LOGIC_SCRIPT)
    df = _build_dataset().copy()
    df["지역제한"] = "경북"

    with TemporaryDirectory() as tmp_dir:
        tmp = Path(tmp_dir)
        source_file = tmp / "source.xlsx"
        analysis_dir = tmp / "analysis"
        output_dir = tmp / "output"
        chart_dir = output_dir / "charts"
        shadow_dir = analysis_dir / "shadow_대구"
        source_file.parent.mkdir(parents=True, exist_ok=True)
        df.to_excel(source_file, index=False)
        shadow_dir.mkdir(parents=True, exist_ok=True)
        chart_dir.mkdir(parents=True, exist_ok=True)
        (shadow_dir / "old.json").write_text("{}", encoding="utf-8")
        (chart_dir / "대구_시각화_0.01.png").write_text("old", encoding="utf-8")
        (chart_dir / "대구_시각화_0.001.png").write_text("old", encoding="utf-8")

        calls: list[list[str]] = []
        monkeypatch.setattr(mod, "ANALYSIS_DIR", analysis_dir)
        monkeypatch.setattr(mod, "OUTPUT_DIR", output_dir)
        monkeypatch.setattr(mod, "CHART_DIR", chart_dir)
        monkeypatch.setattr(mod, "run", lambda cmd: calls.append(cmd))
        monkeypatch.setattr(
            sys,
            "argv",
            [
                "analyze.py",
                "--skip-api",
                "--source-file",
                str(source_file),
                "--region",
                "대구",
                "--period",
                "1y",
            ],
        )

        mod.main()

        assert calls == []
        assert (analysis_dir / "input_대구.xlsx").exists()
        status_payload = json.loads((shadow_dir / "pipeline_status.json").read_text(encoding="utf-8"))
        assert status_payload["status"] == "skipped_empty_input"
        assert not (shadow_dir / "old.json").exists()
        assert not (chart_dir / "대구_시각화_0.01.png").exists()
        assert not (chart_dir / "대구_시각화_0.001.png").exists()


def test_logic_pipeline_runs_market_analyses_before_shadow_and_viz(monkeypatch) -> None:
    mod = _load_module("logic_analyze_market_mod", LOGIC_SCRIPT)
    df = _build_dataset().copy()
    df["사정률"] = 99.8
    df["개찰일시"] = df["투찰일시"]

    with TemporaryDirectory() as tmp_dir:
        tmp = Path(tmp_dir)
        source_file = tmp / "source.xlsx"
        analysis_dir = tmp / "analysis"
        output_dir = tmp / "output"
        chart_dir = output_dir / "charts"
        source_file.parent.mkdir(parents=True, exist_ok=True)
        df.to_excel(source_file, index=False)

        calls: list[list[str]] = []
        monkeypatch.setattr(mod, "ANALYSIS_DIR", analysis_dir)
        monkeypatch.setattr(mod, "OUTPUT_DIR", output_dir)
        monkeypatch.setattr(mod, "CHART_DIR", chart_dir)
        monkeypatch.setattr(mod, "run", lambda cmd: calls.append(cmd))
        monkeypatch.setattr(
            sys,
            "argv",
            [
                "analyze.py",
                "--skip-api",
                "--source-file",
                str(source_file),
                "--region",
                "전국",
                "--period",
                "1y",
                "--min-rate",
                "99.5",
                "--max-rate",
                "100.0",
            ],
        )

        mod.main()

        assert len(calls) == 12
        assert str(mod.FIXED_BIDDER_DETAILED) in calls[0]
        assert str(mod.CROWDING) in calls[1]
        assert str(mod.COLLISION) in calls[2]
        assert str(mod.SHADOW) in calls[3]
        assert all(str(mod.VIZ) in call for call in calls[4:])
        status_payload = json.loads((analysis_dir / "shadow_전국" / "pipeline_status.json").read_text(encoding="utf-8"))
        assert status_payload["status"] == "completed"
        assert status_payload["rate_window"]["rows"] > 0
        assert status_payload["outputs"]["crowding"].endswith("crowding_전국")
        assert status_payload["outputs"]["collision"].endswith("collision_전국")
        assert "conditional_advantage" in status_payload
