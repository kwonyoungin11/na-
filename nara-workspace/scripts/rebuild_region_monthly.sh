#!/usr/bin/env bash
set -euo pipefail

master='/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx'
out='/mnt/a/c/nara-workspace/output'
log='/mnt/a/c/nara-workspace/logs/collection_history.jsonl'

for m in "$@"; do
  args=(python3 -u /mnt/a/c/.agents/skills/API/scripts/run_master_sync_pipeline.py --start-month "$m" --end-month "$m" --master-path "$master" --output-dir "$out" --collection-log "$log" --key-source-env /mnt/a/c/.env)
  echo "[RUN] ${args[*]}"
  "${args[@]}"
done
