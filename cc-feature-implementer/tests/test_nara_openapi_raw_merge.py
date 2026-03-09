"""Tests for API raw JSONL month merge behavior."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys


def _load_module():
    script_path = Path("/mnt/a/c/.agents/skills/API/scripts/run_nara_openapi_pipeline.py")
    spec = importlib.util.spec_from_file_location("nara_openapi_pipeline", script_path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_merge_jsonl_rows_preserves_existing_rows_when_incoming_is_empty(tmp_path: Path):
    module = _load_module()
    path = tmp_path / "2026-03" / "bid_notice_raw.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)
    existing_row = {
        "bidNtceNo": "R26BK01366957",
        "bidNtceOrd": "000",
        "bidNtceNm": "철도 신봉리 42번지 국비지원(소규모) 정밀발굴조사 용역",
        "_src_group": "servc",
    }
    path.write_text(json.dumps(existing_row, ensure_ascii=False) + "\n", encoding="utf-8")

    merged_count, added_count = module._merge_jsonl_rows(path, [])

    rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    assert merged_count == 1
    assert added_count == 0
    assert rows == [existing_row]


def test_merge_jsonl_rows_appends_new_unique_rows(tmp_path: Path):
    module = _load_module()
    path = tmp_path / "2026-03" / "bid_notice_raw.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)
    existing_row = {
        "bidNtceNo": "R26BK01366957",
        "bidNtceOrd": "000",
        "bidNtceNm": "철도 신봉리 42번지 국비지원(소규모) 정밀발굴조사 용역",
        "_src_group": "servc",
    }
    new_row = {
        "bidNtceNo": "R26BK01364852",
        "bidNtceOrd": "000",
        "bidNtceNm": "(긴급)대구 경상감영지 보호구역 시굴·정밀발굴조사 용역",
        "_src_group": "servc",
    }
    path.write_text(json.dumps(existing_row, ensure_ascii=False) + "\n", encoding="utf-8")

    merged_count, added_count = module._merge_jsonl_rows(path, [existing_row, new_row])

    rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    assert merged_count == 2
    assert added_count == 1
    assert rows == [existing_row, new_row]
