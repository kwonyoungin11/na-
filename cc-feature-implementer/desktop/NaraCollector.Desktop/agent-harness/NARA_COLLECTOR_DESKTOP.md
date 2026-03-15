# NaraCollector.Desktop CLI-Anything Harness

- target software: `NaraCollector.Desktop`
- source path: `/mnt/a/c/cc-feature-implementer/desktop/NaraCollector.Desktop`
- harness path: `agent-harness/`

## Exposed backend mappings

- GUI launch
  - `A:\c\nara-workspace\scripts\launch_nara_current.ps1`
- API collection
  - `/mnt/a/c/nara-workspace/scripts/run_api_pipeline.sh`
- analysis pipeline
  - `../NaraCollector.Analytics.Cli/NaraCollector.Analytics.Cli.csproj`
- chart export
  - desktop executable `동국문화재연구원_낙찰분석프로그램.exe --export-chart ...`
- workspace inspection
  - `A:\c\nara-workspace` / `/mnt/a/c/nara-workspace`

## Stateful session model

The harness stores session state in JSON and supports undo/redo for:

- workspace root
- source file
- output root
- region
- period
- min/max rate
- chart precision
- company filter
- last generated status/chart path
- last launch target

## Main commands

- `paths`
- `status`
- `state show|set|undo|redo|reset`
- `launch`
- `collect`
- `analyze`
- `export-chart`

## Notes

- Real backend is preferred. This harness wraps existing scripts / executables rather than reimplementing business logic.
- `--dry-run` is available on execution commands so workflows can be validated safely.
