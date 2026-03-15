# TEST PLAN

## Unit coverage (`test_core.py`)

1. `paths --json` returns resolved workspace/app/master/backend paths.
2. `state set` updates persisted state.
3. `state undo` / `state redo` restore previous snapshots.
4. `collect --dry-run --json` emits the real backend command without executing it.
5. `analyze --dry-run --json` emits the analytics CLI command using session defaults.
6. `export-chart --dry-run --json` emits the desktop export command.
7. `status --json` returns machine-readable workspace summary.

## E2E coverage (`test_full_e2e.py`)

1. Installed command `cli-anything-nara-collector-desktop paths --json` works.
2. Installed command persists state across subprocess calls.
3. Installed command supports dry-run analysis/export commands.

## Validation commands

```bash
cd /mnt/a/c/cc-feature-implementer/desktop/NaraCollector.Desktop/agent-harness
python3 -m pytest cli_anything/nara_collector_desktop/tests -q
python3 -m pip install -e .
cli-anything-nara-collector-desktop paths --json
cli-anything-nara-collector-desktop analyze --dry-run --json
cli-anything-nara-collector-desktop export-chart --dry-run --json
```
