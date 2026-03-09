from __future__ import annotations

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

from ultraqa_qa_cycle import (  # noqa: E402
    CommandResult,
    resolve_goal_commands,
    run_qa_cycles,
    split_command,
)


def test_resolve_goal_commands_for_tests_goal() -> None:
    commands = resolve_goal_commands("tests")

    assert len(commands) == 2
    assert commands[0][:2] == ["dotnet", "test"]
    assert commands[1][:3] == ["python", "-m", "pytest"]


def test_resolve_goal_commands_for_custom_goal() -> None:
    commands = resolve_goal_commands("custom", "python -c 'print(123)'")

    assert commands == [["python", "-c", "print(123)"]]


def test_split_command_rejects_empty_string() -> None:
    try:
        split_command("   ")
    except ValueError as exc:
        assert "must not be empty" in str(exc)
    else:
        raise AssertionError("Expected ValueError for empty command string")


def test_run_qa_cycles_passes_after_retry() -> None:
    calls = {"count": 0}

    def runner(command: list[str]) -> CommandResult:
        calls["count"] += 1
        if calls["count"] == 1:
            return CommandResult(tuple(command), returncode=1, stderr="temporary")
        return CommandResult(tuple(command), returncode=0)

    result = run_qa_cycles(
        commands=[["python", "-c", "print('ok')"]],
        runner=runner,
        max_cycles=5,
        same_failure_limit=3,
        log=lambda _: None,
    )

    assert result.status == "passed"
    assert result.completed_cycles == 2


def test_run_qa_cycles_stops_on_same_failure_three_times() -> None:
    def runner(command: list[str]) -> CommandResult:
        return CommandResult(tuple(command), returncode=1, stderr="always-fail")

    result = run_qa_cycles(
        commands=[["python", "-c", "print('x')"]],
        runner=runner,
        max_cycles=5,
        same_failure_limit=3,
        log=lambda _: None,
    )

    assert result.status == "stopped_same_failure"
    assert result.completed_cycles == 3
    assert result.last_failure_signature is not None
