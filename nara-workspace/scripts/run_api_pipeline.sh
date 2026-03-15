#!/usr/bin/env bash
set -euo pipefail

ROOT="/mnt/a/c/nara-workspace"

mkdir -p "$ROOT/master" "$ROOT/output" "$ROOT/logs" "$ROOT/analysis" "$ROOT/reports" "$ROOT/scripts" "$ROOT/app"

python3 /mnt/a/c/.agents/skills/API/scripts/run_master_sync_pipeline.py \
  --master-path "$ROOT/master/전체_통합_데이터.xlsx" \
  --output-dir "$ROOT/output" \
  --collection-log "$ROOT/logs/collection_history.jsonl" \
  --key-source-env /mnt/a/c/.env \
  "$@"
