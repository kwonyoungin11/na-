"""Draft UltraQA QA-cycle runner.

This script is an executable draft for the UltraQA loop used during net10
upgrade readiness. It focuses on repeatable QA command execution and
conservative stop conditions:

- PASS: all configured commands return exit code 0
- STOP (same failure): identical failure repeats N times
- STOP (max cycles): maximum cycle count reached

The diagnose/fix phase is intentionally left to manual follow-up in this draft.
"""

from __future__ import annotations

import argparse
import shlex
import subprocess
from dataclasses import dataclass
from typing import Callable, Sequence

DEFAULT_GOAL_COMMANDS: dict[str, tuple[str, ...]] = {
    "build": (
        "dotnet build desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj -v minimal",
    ),
    "tests": (
        "dotnet test desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj -v minimal",
        "python -m pytest tests -q",
    ),
    "lint": (
        "python -m ruff check src tests",
    ),
    "typecheck": (
        "python -m py_compile src/ultraqa_qa_cycle.py",
    ),
}


@dataclass(frozen=True)
class CommandResult:
    """Single command execution result."""

    command: tuple[str, ...]
    returncode: int
    stdout: str = ""
    stderr: str = ""


@dataclass(frozen=True)
class CycleRunResult:
    """Summary of qa-cycle execution."""

    status: str
    completed_cycles: int
    last_failure_signature: str | None = None


Runner = Callable[[Sequence[str]], CommandResult]


def split_command(command: str) -> list[str]:
    """Split a shell-like command string into argv tokens."""

    parts = shlex.split(command)
    if not parts:
        raise ValueError("Command string must not be empty")
    return parts


def resolve_goal_commands(goal: str, custom_command: str | None = None) -> list[list[str]]:
    """Resolve goal into executable command lists."""

    if goal == "custom":
        if not custom_command:
            raise ValueError("--command is required when --goal custom is used")
        return [split_command(custom_command)]

    if goal not in DEFAULT_GOAL_COMMANDS:
        raise ValueError(f"Unsupported goal: {goal}")

    return [split_command(raw) for raw in DEFAULT_GOAL_COMMANDS[goal]]


def build_failure_signature(result: CommandResult) -> str:
    """Create stable signature used for same-failure detection."""

    stderr = result.stderr.strip()
    stdout = result.stdout.strip()
    return "|".join(
        [
            " ".join(result.command),
            f"rc={result.returncode}",
            f"stderr={stderr}",
            f"stdout={stdout}",
        ]
    )


def run_commands_once(commands: Sequence[Sequence[str]], runner: Runner) -> tuple[bool, CommandResult | None]:
    """Run all commands once; stop at the first failure."""

    for command in commands:
        result = runner(command)
        if result.returncode != 0:
            return False, result
    return True, None


def run_qa_cycles(
    commands: Sequence[Sequence[str]],
    runner: Runner,
    *,
    max_cycles: int = 5,
    same_failure_limit: int = 3,
    log: Callable[[str], None] = print,
) -> CycleRunResult:
    """Run QA loop until pass or stop condition is reached."""

    if max_cycles <= 0:
        raise ValueError("max_cycles must be greater than 0")
    if same_failure_limit <= 0:
        raise ValueError("same_failure_limit must be greater than 0")

    failure_counts: dict[str, int] = {}
    last_failure_signature: str | None = None

    for cycle in range(1, max_cycles + 1):
        log(f"[ULTRAQA Cycle {cycle}/{max_cycles}] Running QA commands...")
        passed, failed = run_commands_once(commands, runner)

        if passed:
            log(f"[ULTRAQA COMPLETE] Goal met after {cycle} cycle(s)")
            return CycleRunResult(
                status="passed",
                completed_cycles=cycle,
                last_failure_signature=last_failure_signature,
            )

        if failed is None:
            raise RuntimeError("Internal error: missing failure result")

        failure_signature = build_failure_signature(failed)
        last_failure_signature = failure_signature
        failure_counts[failure_signature] = failure_counts.get(failure_signature, 0) + 1

        log(
            f"[ULTRAQA Cycle {cycle}/{max_cycles}] FAILED - "
            f"exit {failed.returncode}: {' '.join(failed.command)}"
        )

        if failure_counts[failure_signature] >= same_failure_limit:
            log(
                "[ULTRAQA STOPPED] Same failure detected "
                f"{failure_counts[failure_signature]} times"
            )
            return CycleRunResult(
                status="stopped_same_failure",
                completed_cycles=cycle,
                last_failure_signature=failure_signature,
            )

    log(f"[ULTRAQA STOPPED] Max cycles reached ({max_cycles})")
    return CycleRunResult(
        status="stopped_max_cycles",
        completed_cycles=max_cycles,
        last_failure_signature=last_failure_signature,
    )


def run_subprocess(command: Sequence[str]) -> CommandResult:
    """Run one command with subprocess and capture output."""

    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    return CommandResult(
        command=tuple(command),
        returncode=completed.returncode,
        stdout=completed.stdout,
        stderr=completed.stderr,
    )


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments."""

    parser = argparse.ArgumentParser(description="UltraQA QA-cycle draft runner")
    parser.add_argument(
        "--goal",
        choices=("build", "tests", "lint", "typecheck", "custom"),
        default="tests",
        help="QA goal to execute",
    )
    parser.add_argument(
        "--command",
        help="Custom command used with --goal custom",
    )
    parser.add_argument(
        "--max-cycles",
        type=int,
        default=5,
        help="Maximum QA cycles",
    )
    parser.add_argument(
        "--same-failure-limit",
        type=int,
        default=3,
        help="Stop early when identical failure repeats N times",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print resolved commands and exit",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entrypoint."""

    args = parse_args(argv)
    commands = resolve_goal_commands(args.goal, args.command)

    if args.dry_run:
        print("[ULTRAQA DRAFT] Dry-run command plan:")
        for index, command in enumerate(commands, start=1):
            print(f"  {index}. {' '.join(command)}")
        return 0

    result = run_qa_cycles(
        commands,
        run_subprocess,
        max_cycles=args.max_cycles,
        same_failure_limit=args.same_failure_limit,
    )

    return 0 if result.status == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
