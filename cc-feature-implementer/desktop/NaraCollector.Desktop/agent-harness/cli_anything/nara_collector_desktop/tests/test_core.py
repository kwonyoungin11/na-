from __future__ import annotations

import json
from pathlib import Path

from click.testing import CliRunner

from cli_anything.nara_collector_desktop.nara_collector_desktop_cli import cli


def _env(tmp_path: Path) -> dict[str, str]:
    workspace = tmp_path / "nara-workspace"
    return {
        "CLI_ANYTHING_NARA_COLLECTOR_DESKTOP_SESSION_DIR": str(tmp_path / ".session"),
        "NARA_WORKSPACE_ROOT": str(workspace),
    }


def test_paths_json_returns_workspace_and_projects(tmp_path: Path) -> None:
    runner = CliRunner()
    result = runner.invoke(cli, ["paths", "--json"], env=_env(tmp_path))
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert payload["paths"]["workspace_root"].endswith("nara-workspace")
    assert payload["paths"]["analytics_project"].endswith("NaraCollector.Analytics.Cli.csproj")


def test_state_set_undo_redo_roundtrip(tmp_path: Path) -> None:
    runner = CliRunner()
    env = _env(tmp_path)

    result = runner.invoke(cli, ["state", "set", "--region", "경북", "--period", "1m", "--json"], env=env)
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert payload["region"] == "경북"
    assert payload["period"] == "1m"

    result = runner.invoke(cli, ["state", "undo", "--json"], env=env)
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert payload["region"] == "전국"
    assert payload["period"] == "3m"

    result = runner.invoke(cli, ["state", "redo", "--json"], env=env)
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert payload["region"] == "경북"
    assert payload["period"] == "1m"


def test_collect_analyze_export_support_dry_run_json(tmp_path: Path) -> None:
    runner = CliRunner()
    env = _env(tmp_path)

    collect_result = runner.invoke(cli, ["collect", "--months", "6", "--dry-run", "--json"], env=env)
    assert collect_result.exit_code == 0, collect_result.output
    collect_payload = json.loads(collect_result.output)
    assert "/run_api_pipeline.sh" in " ".join(collect_payload["command"])

    analyze_result = runner.invoke(cli, ["analyze", "--dry-run", "--json"], env=env)
    assert analyze_result.exit_code == 0, analyze_result.output
    analyze_payload = json.loads(analyze_result.output)
    assert "NaraCollector.Analytics.Cli.csproj" in " ".join(analyze_payload["command"])

    export_result = runner.invoke(cli, ["export-chart", "--dry-run", "--json"], env=env)
    assert export_result.exit_code == 0, export_result.output
    export_payload = json.loads(export_result.output)
    assert "--export-chart" in export_payload["command"]


def test_status_json_contains_master_db_and_analysis_sections(tmp_path: Path) -> None:
    runner = CliRunner()
    result = runner.invoke(cli, ["status", "--json"], env=_env(tmp_path))
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert set(["paths", "session", "master", "db", "latest_collection", "analysis"]).issubset(payload.keys())
