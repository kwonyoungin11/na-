from __future__ import annotations

import json
import shlex
from pathlib import Path
from typing import Any

import click

from cli_anything.nara_collector_desktop import __version__
from cli_anything.nara_collector_desktop.core.session import SessionManager
from cli_anything.nara_collector_desktop.utils.nara_collector_desktop_backend import (
    CommandResult,
    SessionSnapshot,
    build_analyze_command,
    build_collect_command,
    build_export_chart_command,
    build_launch_command,
    collect_status_payload,
    ensure_workspace_dirs,
    resolve_paths,
    run_command,
)


def emit(payload: Any, as_json: bool) -> None:
    if as_json:
        click.echo(json.dumps(payload, ensure_ascii=False, indent=2))
        return

    if isinstance(payload, dict):
        for key, value in payload.items():
            click.echo(f"{key}: {value}")
        return

    click.echo(payload)


def result_payload(result: CommandResult, **extra: Any) -> dict[str, Any]:
    payload = result.to_dict()
    payload.update(extra)
    return payload


@click.group(invoke_without_command=True)
@click.version_option(__version__)
@click.pass_context
def cli(ctx: click.Context) -> None:
    """CLI-Anything harness for NaraCollector.Desktop.

    Run without a subcommand to enter REPL mode.
    """
    ctx.ensure_object(dict)
    ctx.obj.setdefault("session", SessionManager())
    if ctx.invoked_subcommand is None:
        _run_repl(ctx)


@cli.command()
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def paths(ctx: click.Context, as_json: bool) -> None:
    session = ctx.obj["session"].current()
    resolved = resolve_paths(session.workspace_root)
    ensure_workspace_dirs(resolved)
    emit({"paths": resolved.to_dict(), "session": session.to_dict()}, as_json)


@cli.command()
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def status(ctx: click.Context, as_json: bool) -> None:
    session = ctx.obj["session"].current()
    emit(collect_status_payload(session), as_json)


@cli.group()
def state() -> None:
    """Inspect and mutate persisted session state."""


@state.command("show")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def state_show(ctx: click.Context, as_json: bool) -> None:
    emit(ctx.obj["session"].current().to_dict(), as_json)


@state.command("set")
@click.option("--workspace-root", type=click.Path(file_okay=False, dir_okay=True, path_type=Path))
@click.option("--source-file", type=click.Path(dir_okay=False, path_type=Path))
@click.option("--output-root", type=click.Path(file_okay=False, dir_okay=True, path_type=Path))
@click.option("--region", type=click.Choice(["전국", "대구", "경북", "전체"], case_sensitive=False))
@click.option("--period", type=click.Choice(["1m", "3m", "6m", "1y", "2y", "all"], case_sensitive=False))
@click.option("--min-rate", type=float)
@click.option("--max-rate", type=float)
@click.option("--precision", type=click.Choice(["0.01", "0.001"], case_sensitive=True))
@click.option("--company")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def state_set(
    ctx: click.Context,
    workspace_root: Path | None,
    source_file: Path | None,
    output_root: Path | None,
    region: str | None,
    period: str | None,
    min_rate: float | None,
    max_rate: float | None,
    precision: str | None,
    company: str | None,
    as_json: bool,
) -> None:
    session = ctx.obj["session"]
    snapshot = session.update(
        workspace_root=str(workspace_root) if workspace_root else None,
        source_file=str(source_file) if source_file else None,
        output_root=str(output_root) if output_root else None,
        region=region,
        period=period,
        min_rate=min_rate,
        max_rate=max_rate,
        precision=precision,
        company=company,
    )
    emit(snapshot.to_dict(), as_json)


@state.command("undo")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def state_undo(ctx: click.Context, as_json: bool) -> None:
    emit(ctx.obj["session"].undo().to_dict(), as_json)


@state.command("redo")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def state_redo(ctx: click.Context, as_json: bool) -> None:
    emit(ctx.obj["session"].redo().to_dict(), as_json)


@state.command("reset")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def state_reset(ctx: click.Context, as_json: bool) -> None:
    emit(ctx.obj["session"].reset().to_dict(), as_json)


@cli.command()
@click.option("--dry-run", is_flag=True, help="Print the backend command without executing it")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def launch(ctx: click.Context, dry_run: bool, as_json: bool) -> None:
    manager: SessionManager = ctx.obj["session"]
    snapshot = manager.current()
    paths = resolve_paths(snapshot.workspace_root)
    ensure_workspace_dirs(paths)
    command = build_launch_command(paths)
    result = run_command(command, dry_run=dry_run)
    updated = SessionSnapshot(**{**snapshot.to_dict(), "last_launch_target": str(paths.desktop_executable or paths.launch_script)})
    manager.save_current(updated, push_undo=False)
    emit(result_payload(result, launch_target=updated.last_launch_target), as_json)


@cli.command()
@click.option("--start-month")
@click.option("--end-month")
@click.option("--months", type=int)
@click.option("--years", type=int)
@click.option("--keywords")
@click.option("--dry-run", is_flag=True, help="Print the backend command without executing it")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def collect(
    ctx: click.Context,
    start_month: str | None,
    end_month: str | None,
    months: int | None,
    years: int | None,
    keywords: str | None,
    dry_run: bool,
    as_json: bool,
) -> None:
    session = ctx.obj["session"].current()
    paths = resolve_paths(session.workspace_root)
    ensure_workspace_dirs(paths)
    command = build_collect_command(
        paths,
        start_month=start_month,
        end_month=end_month,
        months=months,
        years=years,
        keywords=keywords,
    )
    result = run_command(command, cwd=paths.repo_root, dry_run=dry_run)
    payload = result_payload(result, latest_collection=collect_status_payload(session)["latest_collection"])
    emit(payload, as_json)


@cli.command()
@click.option("--source-file", type=click.Path(dir_okay=False, path_type=Path))
@click.option("--output-root", type=click.Path(file_okay=False, dir_okay=True, path_type=Path))
@click.option("--region", type=click.Choice(["전국", "대구", "경북", "전체"], case_sensitive=False))
@click.option("--period", type=click.Choice(["1m", "3m", "6m", "1y", "2y", "all"], case_sensitive=False))
@click.option("--min-rate", type=float)
@click.option("--max-rate", type=float)
@click.option("--dry-run", is_flag=True, help="Print the backend command without executing it")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def analyze(
    ctx: click.Context,
    source_file: Path | None,
    output_root: Path | None,
    region: str | None,
    period: str | None,
    min_rate: float | None,
    max_rate: float | None,
    dry_run: bool,
    as_json: bool,
) -> None:
    manager: SessionManager = ctx.obj["session"]
    base = manager.current()
    snapshot = SessionSnapshot(**{
        **base.to_dict(),
        "source_file": str(source_file) if source_file else base.source_file,
        "output_root": str(output_root) if output_root else base.output_root,
        "region": region or base.region,
        "period": period or base.period,
        "min_rate": min_rate if min_rate is not None else base.min_rate,
        "max_rate": max_rate if max_rate is not None else base.max_rate,
    }).normalized()
    paths = resolve_paths(snapshot.workspace_root)
    ensure_workspace_dirs(paths)
    command = build_analyze_command(snapshot, paths)
    result = run_command(command, cwd=paths.repo_root, dry_run=dry_run)
    last_line = next((line.strip() for line in reversed(result.stdout.splitlines()) if line.strip()), "")
    if last_line:
        snapshot = SessionSnapshot(**{**snapshot.to_dict(), "last_status_path": last_line})
        manager.save_current(snapshot, push_undo=False)
    emit(result_payload(result, status_path=snapshot.last_status_path), as_json)


@cli.command("export-chart")
@click.option("--output", "output_path", type=click.Path(dir_okay=False, path_type=Path))
@click.option("--input", "input_path", type=click.Path(dir_okay=False, path_type=Path))
@click.option("--company")
@click.option("--precision", type=click.Choice(["0.01", "0.001"], case_sensitive=True))
@click.option("--min-rate", type=float)
@click.option("--max-rate", type=float)
@click.option("--width", type=int, default=3600, show_default=True)
@click.option("--height", type=int, default=2200, show_default=True)
@click.option("--dry-run", is_flag=True, help="Print the backend command without executing it")
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON")
@click.pass_context
def export_chart(
    ctx: click.Context,
    output_path: Path | None,
    input_path: Path | None,
    company: str | None,
    precision: str | None,
    min_rate: float | None,
    max_rate: float | None,
    width: int,
    height: int,
    dry_run: bool,
    as_json: bool,
) -> None:
    manager: SessionManager = ctx.obj["session"]
    base = manager.current()
    snapshot = SessionSnapshot(**{
        **base.to_dict(),
        "source_file": str(input_path) if input_path else base.source_file,
        "company": company or base.company,
        "precision": precision or base.precision,
        "min_rate": min_rate if min_rate is not None else base.min_rate,
        "max_rate": max_rate if max_rate is not None else base.max_rate,
    }).normalized()
    paths = resolve_paths(snapshot.workspace_root)
    ensure_workspace_dirs(paths)
    command = build_export_chart_command(snapshot, paths, str(output_path) if output_path else None, width, height)
    result = run_command(command, cwd=paths.repo_root, dry_run=dry_run)
    chart_path = str(output_path) if output_path else command[command.index("--output") + 1]
    manager.save_current(SessionSnapshot(**{**snapshot.to_dict(), "last_chart_path": chart_path}), push_undo=False)
    emit(result_payload(result, chart_path=chart_path), as_json)


def _run_repl(ctx: click.Context) -> None:
    click.echo("Entering NaraCollector.Desktop REPL. Type 'help' for commands, 'quit' to exit.")
    while True:
        try:
            line = click.prompt("nara-collector-desktop", prompt_suffix="> ", default="", show_default=False)
        except (EOFError, KeyboardInterrupt):
            click.echo()
            break
        line = line.strip()
        if not line:
            continue
        if line in {"quit", "exit"}:
            break
        if line == "help":
            click.echo("commands: paths, status, state show|set|undo|redo|reset, launch, collect, analyze, export-chart")
            continue
        argv = shlex.split(line)
        try:
            cli.main(args=argv, prog_name="cli-anything-nara-collector-desktop", standalone_mode=False, obj=ctx.obj)
        except SystemExit as exc:  # pragma: no cover - click internals
            if exc.code not in (0, None):
                click.echo(f"command exited with code {exc.code}")
        except click.ClickException as exc:
            exc.show()
        except Exception as exc:  # pragma: no cover - defensive
            click.echo(f"error: {exc}")


if __name__ == "__main__":
    cli()
