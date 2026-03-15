from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from pathlib import Path


def _command() -> list[str]:
    installed = shutil.which("cli-anything-nara-collector-desktop")
    if installed:
        return [installed]
    return [sys.executable, "-m", "cli_anything.nara_collector_desktop"]


def _env(tmp_path: Path) -> dict[str, str]:
    env = os.environ.copy()
    env["CLI_ANYTHING_NARA_COLLECTOR_DESKTOP_SESSION_DIR"] = str(tmp_path / ".session")
    env["NARA_WORKSPACE_ROOT"] = str(tmp_path / "nara-workspace")
    return env


def test_installed_command_paths_json(tmp_path: Path) -> None:
    completed = subprocess.run(
        [*_command(), "paths", "--json"],
        env=_env(tmp_path),
        text=True,
        capture_output=True,
        check=True,
    )
    payload = json.loads(completed.stdout)
    assert payload["paths"]["workspace_root"].endswith("nara-workspace")


def test_installed_command_persists_state_across_calls(tmp_path: Path) -> None:
    env = _env(tmp_path)
    subprocess.run([*_command(), "state", "set", "--region", "경북", "--json"], env=env, text=True, capture_output=True, check=True)
    completed = subprocess.run([*_command(), "state", "show", "--json"], env=env, text=True, capture_output=True, check=True)
    payload = json.loads(completed.stdout)
    assert payload["region"] == "경북"


def test_installed_command_analyze_dry_run_json(tmp_path: Path) -> None:
    completed = subprocess.run(
        [*_command(), "analyze", "--dry-run", "--json"],
        env=_env(tmp_path),
        text=True,
        capture_output=True,
        check=True,
    )
    payload = json.loads(completed.stdout)
    assert any("NaraCollector.Analytics.Cli.csproj" in arg for arg in payload["command"])
