from __future__ import annotations

import os
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent
DEFAULT_WORKSPACE_ROOT = PROJECT_ROOT / "nara-workspace"


def workspace_root() -> Path:
    raw = os.environ.get("NARA_WORKSPACE_ROOT", "").strip()
    if raw:
        return Path(raw).expanduser()
    return DEFAULT_WORKSPACE_ROOT


def source_data_dir() -> Path:
    return workspace_root() / "source-data"


def first_stage_root() -> Path:
    return workspace_root() / "first-stage"


def first_stage_dir(name: str = "default") -> Path:
    return first_stage_root() / name


def master_dir() -> Path:
    return workspace_root() / "master"


def master_path() -> Path:
    return master_dir() / "전체_통합_데이터.xlsx"


def daegu_gyeongbuk_master_path() -> Path:
    return master_dir() / "대구경북통합데이터.xlsx"


def output_dir() -> Path:
    return workspace_root() / "output"


def clean_output_dir() -> Path:
    return output_dir() / "clean"


def raw_output_dir() -> Path:
    return output_dir() / "raw"


def charts_dir() -> Path:
    return output_dir() / "charts"


def logs_dir() -> Path:
    return workspace_root() / "logs"


def collection_log_path() -> Path:
    return logs_dir() / "collection_history.jsonl"


def analysis_dir() -> Path:
    return workspace_root() / "analysis"


def reports_dir() -> Path:
    return workspace_root() / "reports"


def scripts_dir() -> Path:
    return workspace_root() / "scripts"


def app_dir() -> Path:
    return workspace_root() / "app"


def app_api_output_dir() -> Path:
    return app_dir() / "api-skill-output"


def default_env_candidates() -> list[Path]:
    return [
        workspace_root() / ".env",
        PROJECT_ROOT / ".env",
        PROJECT_ROOT / "공공데이터포털api" / ".env",
    ]


def default_env_path() -> Path:
    for candidate in default_env_candidates():
        if candidate.exists():
            return candidate
    return default_env_candidates()[0]


def ensure_workspace_dirs() -> dict[str, Path]:
    paths = {
        "workspace": workspace_root(),
        "source_data": source_data_dir(),
        "first_stage_root": first_stage_root(),
        "master": master_dir(),
        "output": output_dir(),
        "clean_output": clean_output_dir(),
        "raw_output": raw_output_dir(),
        "charts": charts_dir(),
        "logs": logs_dir(),
        "analysis": analysis_dir(),
        "reports": reports_dir(),
        "scripts": scripts_dir(),
        "app": app_dir(),
        "app_api_output": app_api_output_dir(),
    }
    for path in paths.values():
        path.mkdir(parents=True, exist_ok=True)
    return paths
