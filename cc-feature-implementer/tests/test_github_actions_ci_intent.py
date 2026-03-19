from __future__ import annotations

from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
YAML_SUFFIXES = {".yml", ".yaml"}


def _workflow_files() -> list[Path]:
    return sorted(path for path in WORKFLOWS_DIR.iterdir() if path.is_file())


def _workflow_yaml_files() -> list[Path]:
    return [path for path in _workflow_files() if path.suffix in YAML_SUFFIXES]


def _workflow_text() -> str:
    workflow_files = _workflow_yaml_files()

    assert workflow_files, "Expected at least one GitHub Actions workflow file."

    return "\n\n".join(
        path.read_text(encoding="utf-8") for path in workflow_files
    )


def test_workflow_directory_contains_only_yaml_files() -> None:
    non_yaml_files = [
        path.name for path in _workflow_files() if path.suffix not in YAML_SUFFIXES
    ]

    assert not non_yaml_files, (
        "Unexpected non-YAML workflow files under .github/workflows: "
        f"{non_yaml_files}"
    )


def test_ci_workflows_preserve_required_triggers() -> None:
    workflow_text = _workflow_text()

    assert re.search(r"(?m)^\s*push\s*:", workflow_text)
    assert re.search(r"(?m)^\s*pull_request\s*:", workflow_text)
    assert re.search(r"(?m)^\s*workflow_dispatch\s*:", workflow_text)
    assert re.search(
        r"push\s*:\s*(?:\n[ \t]+.*)*?\n[ \t]+branches\s*:\s*\[\s*main\s*\]",
        workflow_text,
    ), "Expected push trigger to stay scoped to main."


def test_ci_workflows_preserve_python_validation_intent() -> None:
    workflow_text = _workflow_text()

    assert "ubuntu-latest" in workflow_text
    assert "actions/setup-python" in workflow_text
    assert "pytest" in workflow_text
    assert "cc-feature-implementer" in workflow_text
    assert (REPO_ROOT / "cc-feature-implementer" / "tests").is_dir()


def test_ci_workflows_preserve_windows_desktop_validation_intent() -> None:
    workflow_text = _workflow_text()

    expected_project_paths = [
        Path("cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj"),
        Path("cc-feature-implementer/desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj"),
        Path("cc-feature-implementer/desktop/NaraCollector.Analytics.Cli/NaraCollector.Analytics.Cli.csproj"),
    ]

    assert "windows-latest" in workflow_text
    assert "actions/setup-dotnet" in workflow_text

    for relative_path in expected_project_paths:
        assert str(relative_path).replace("\\", "/") in workflow_text
        assert (REPO_ROOT / relative_path).is_file()
