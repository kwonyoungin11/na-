"""Load existing monthly raw jsonl and run preprocess_to_master + master merge."""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pandas as pd

PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import default_env_path, master_path, output_dir

SCRIPT_ROOT = Path(__file__).resolve().parent


def load_module():
    import importlib.util

    path = SCRIPT_ROOT / "run_nara_openapi_pipeline.py"
    spec = importlib.util.spec_from_file_location("nara_openapi_pipeline", path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)  # type: ignore[attr-defined]
    return mod


def load_jsonl(path: Path) -> list[dict]:
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            rows.append(json.loads(line))
    return rows


def main() -> int:
    raw_root = output_dir() / "raw"
    openg_rows: list[dict] = []
    notice_rows: list[dict] = []
    for month_dir in sorted(raw_root.iterdir()):
        if not month_dir.is_dir():
            continue
        openg_rows.extend(load_jsonl(month_dir / "openg_result_raw.jsonl"))
        notice_rows.extend(load_jsonl(month_dir / "bid_notice_raw.jsonl"))
        print(f"[raw] {month_dir.name} openg={len(load_jsonl(month_dir / 'openg_result_raw.jsonl'))} notice={len(load_jsonl(month_dir / 'bid_notice_raw.jsonl'))}")

    print(f"[raw] total openg={len(openg_rows)} notice={len(notice_rows)}")
    mod = load_module()
    key = mod.load_service_key(default_env_path())
    session = mod.build_session()
    openg_df = pd.DataFrame(openg_rows)
    notice_df = pd.DataFrame(notice_rows)
    clean_df, stats = mod.preprocess_to_master(
        openg_df,
        notice_df,
        session=session,
        service_key=key,
        num_of_rows=100,
        delay_sec=0.03,
        allowed_regions=["전국", "경북"],
        min_participants=6,
        target_company_terms=["문화재연구원"],
        exclude_retired_company=True,
    )
    print("[preprocess]", json.dumps(stats, ensure_ascii=False))
    out_stats = mod.save_outputs(clean_df, output_dir())
    print("[save]", out_stats)

    import run_master_sync_pipeline as sync

    master = Path(master_path())
    existing = pd.read_excel(master, engine="openpyxl") if master.exists() else None
    merged = sync._merge_master(existing, clean_df)
    master.parent.mkdir(parents=True, exist_ok=True)
    merged.to_excel(master, index=False)
    print(f"[master] rows={len(merged)} path={master}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
