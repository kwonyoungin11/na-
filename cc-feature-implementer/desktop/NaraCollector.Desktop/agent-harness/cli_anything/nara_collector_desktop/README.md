# cli_anything.nara_collector_desktop

Stateful Click CLI harness for the WPF app.

## Commands

- `cli-anything-nara-collector-desktop paths`
- `cli-anything-nara-collector-desktop status`
- `cli-anything-nara-collector-desktop state show`
- `cli-anything-nara-collector-desktop state set --region 경북 --period 1m`
- `cli-anything-nara-collector-desktop collect --months 6`
- `cli-anything-nara-collector-desktop analyze`
- `cli-anything-nara-collector-desktop export-chart --output ./chart.png`
- `cli-anything-nara-collector-desktop launch`

## REPL

Run without a subcommand:

```bash
cli-anything-nara-collector-desktop
```

Then enter commands such as:

```text
paths --json
state set --region 전국 --period 3m
analyze --dry-run
export-chart --dry-run
quit
```
