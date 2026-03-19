# GitHub Actions CI refactor review notes

This note captures the **confirmed CI intent**, the **startup failure evidence**, and the **review guidance** for the GitHub Actions refactor that was investigated on **March 19, 2026**.

## CI intent to preserve

The repository CI should continue to do all of the following:

1. run on `push` to `main`, `pull_request`, and `workflow_dispatch`
2. validate the Python code under `cc-feature-implementer/tests`
3. validate the desktop/test/build path for:
   - `cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj`
   - `cc-feature-implementer/desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj`
   - `cc-feature-implementer/desktop/NaraCollector.Analytics.Cli/NaraCollector.Analytics.Cli.csproj`
4. keep diffs small and reversible while narrowing the startup boundary

## Confirmed startup_failure evidence

The recurring failures happen **before any CI job starts**.

### Direct GitHub evidence

- **March 9, 2026** — workflow_dispatch run `22835883056`
  - workflow id: `243400597`
  - path: `.github/workflows/desktop-ci.yml`
  - conclusion: `startup_failure`
  - `gh run view 22835883056` reports: `This run likely failed because of a workflow file issue.`
  - jobs/check-runs returned by the API: none
- **March 9, 2026** — push run `22835850514`
  - workflow id: `243400598`
  - path: `BuildFailed`
  - conclusion: `startup_failure`
  - jobs/check-runs returned by the API: none
- **March 11, 2026** — push run `22943626097`
  - workflow id: `243400598`
  - path: `BuildFailed`
  - conclusion: `startup_failure`
- **March 15, 2026** — push run `23106008857`
  - workflow id: `243400598`
  - path: `BuildFailed`
  - conclusion: `startup_failure`

### Diagnostic conclusion

Because the failing runs create **zero jobs** and **zero check-runs**, the failure boundary is at workflow startup/resolution time, not inside `pytest`, `dotnet test`, or `dotnet build` execution.

That conclusion is confirmed by GitHub run metadata. The exact root cause is still not fully isolated, so maintainers should treat the deleted `BuildFailed` workflow record as a **ghost/stale workflow symptom**, not as a verified standalone cause.

## Local verification completed on March 19, 2026

These checks succeeded locally in WSL and show that the main project commands themselves are runnable:

- `gh workflow view desktop-ci.yml --yaml`
- `cd cc-feature-implementer && python -m pytest tests/test_api_region_filter.py -q`
  - result: `3 passed`
- `dotnet restore cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj -v minimal`
- `dotnet test cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj --configuration Release --no-restore --verbosity minimal`
  - result: `163 passed`
- `dotnet build cc-feature-implementer/desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj --configuration Release --verbosity minimal`
- `dotnet build cc-feature-implementer/desktop/NaraCollector.Analytics.Cli/NaraCollector.Analytics.Cli.csproj --configuration Release --verbosity minimal`

### Known baseline failures outside the CI refactor

A broader Python sweep still fails outside the workflow refactor scope:

- `cd cc-feature-implementer && python -m pytest tests -q`
- current failures:
  - `tests/test_bidder_visualization.py::test_filter_visualization_rows_keeps_my_company_and_applies_period_filter`
  - `tests/test_chart_highlight_and_exclusions.py::test_rank_one_highlight_uses_dark_red_border`
  - `tests/test_chart_highlight_and_exclusions.py::test_excluded_company_clause_contains_only_hyorim`

Those failures should not be attributed to the GitHub Actions file split itself.

## Commit review summary

### Reviewed commit `3ebc1f2`

**Assessment:** approve

- The split from one mixed workflow into `project-ci.yml` plus reusable `desktop-build.yml` is a good containment move.
- Moving the Windows path behind `workflow_call` keeps the public trigger surface small and makes startup debugging more targeted.
- Replacing `setup-dotnet` `10.0.x` with `8.0.x` matches the current `net8.0` and `net8.0-windows10.0.19041.0` targets.

**Residual risk:** this improves structure but does not by itself prove the startup boundary is fixed until GitHub creates real jobs for a fresh run.

### Reviewed commit `5b9b074`

**Assessment:** approve

- The new `test_github_actions_ci_intent.py` file is a good non-invasive guard for trigger and coverage drift during workflow splitting.
- Keeping the check text-based avoids adding a YAML/actionlint dependency while the workflow files are still moving.

**Residual risk:** the test protects CI intent, not GitHub's full workflow syntax/semantics, so it complements but does not replace a real post-merge Actions run.

## Review recommendations for the workflow refactor

### 1. Split orchestration from Windows desktop execution

Recommended structure:

- `project-ci.yml` — top-level triggers, concurrency, Python validation, orchestration
- `desktop-build.yml` — reusable `workflow_call` workflow for the Windows desktop/test/build path

This keeps the public trigger surface in one place while isolating the Windows desktop path behind a reusable boundary.

### 2. Keep the desktop workflow on .NET 8

The repository projects currently target `net8.0` and `net8.0-windows10.0.19041.0`.

That makes `actions/setup-dotnet` with `8.0.x` the safest default for the desktop workflow. Moving the workflow to .NET 10 before the projects move would add unnecessary startup/debug surface.

### 3. Keep documentation outside `.github/workflows`

The CI-intent regression test added for this refactor expects `.github/workflows` to contain only YAML workflow files. Put durable maintainer notes in a tracked path outside `.github/workflows` (for example a root maintenance note such as this file), not next to the workflow YAML files.

### 4. Preserve the current Python test intent until the Python baseline is cleaned up

The current workflow runs test files individually and surfaces a list of failing files. That behavior is still useful while the Python suite has unrelated baseline failures.

Once the broader Python baseline is green, the workflow can be simplified to a single `pytest tests -q` step.

## Suggested post-merge check

After merging the workflow split, trigger a fresh `workflow_dispatch` run and verify all of the following:

1. the run is attached to the active workflow path, not `BuildFailed`
2. at least one job is created immediately
3. the Python and Windows desktop stages appear as separate, debuggable units

If the next run still ends in `startup_failure` with zero jobs, the remaining investigation should move from workflow contents to repository/account-level Actions configuration.
