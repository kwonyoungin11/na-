# AGENTS.md

## Scope & Antigravity Official Customizations

This repository uses the official **Google Antigravity** workspace customization layout:

- **Workspace Rules**: `AGENTS.md` (root and directory-scoped)
- **Workspace Skills**: `.agents/skills/<skill-name>/SKILL.md`
  - Load [`cpp-nara-desktop`](.agents/skills/cpp-nara-desktop/SKILL.md) when working on the C++ desktop application, OpenAPI collector, SQLite store, award table, or battlefield map (`전장 맵`).
- **Mistake Log**: [`.agents/MISTAKES.md`](.agents/MISTAKES.md)
- **Execution Plans**: [`.agents/PLANS.md`](.agents/PLANS.md)

## Mistake Log

- Before coding, collecting, or editing chart/table logic, read [`.agents/MISTAKES.md`](.agents/MISTAKES.md). Do not repeat any listed mistake.
- When a new mistake is confirmed, append one item there (what went wrong + the rule). Do not only mention it in chat.

## Domain Invariants

- **Development Loop**: Hypothesis → failing check (`test_*.cpp`) → smallest fix → re-run the **same** check.
- **Single Snapshot Graph**: `업데이트` → C++ `nara_award_collect` JSONL → SQLite (`nara-dashboard.db`) → main table (`ReloadTable`) → in-memory snapshot (`g_snapshot`) → battlefield map (`전장 맵`). The chart popup copies `g_snapshot` and never re-reads SQLite on its own.
- **Region Tokens**: Strictly `전국` | `경북` only. Normalize `경상북도` / `경상북` / `대구,경북` to `경북`. Never expose `대구` in the UI, and never mix `전국` and `경북` notices in the same universe.
- **Award Filter**: Collect and display award notices only (`낙찰`). Drop notices with `< 6` unique companies. Exclude `국가유산영향진단` (`배수영향구간` is allowed).
- **Live Desktop Application**:
  - Live C++ desktop executable: `cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_desktop.exe`
  - Launch on the user's interactive `Default` desktop (never leave it hidden in a background/isolated desktop or killed by a shell job).
  - Launch the C# Debug executable only when the user explicitly asks for the C# app.

## Core Rules

- Do not guess. Inspect relevant files, tests, configs, and logs before editing.
- Prefer the smallest safe diff that solves the request.
- Follow existing repository patterns (`nara::` helpers, `chart_theme.h` tokens, `dwrite_text` DirectWrite queue) before introducing new abstractions.
- Do not change unrelated code unless required for correctness.
- Do not present assumptions as verified facts. State uncertainty clearly.

## Routing

- Start with the smallest set of files likely to answer the task.
- Prioritize repository instructions (`AGENTS.md`, `.agents/MISTAKES.md`), `cc-feature-implementer/desktop/nara_collect_cpp/CMakeLists.txt`, and the nearest `tests/test_*.cpp` files.
- If behavior is unclear, check the nearest relevant tests before widening the search.

## Planning & Execution

- Use an ExecPlan ([`.agents/PLANS.md`](.agents/PLANS.md) or a task-specific plan artifact) before major or cross-cutting edits, defining milestones, acceptance criteria, validation commands, and a stop-and-fix rule.
- Keep diffs scoped to the requested change.
- After each meaningful milestone, run the relevant checks and fix failures before moving on.

## Verification & Repo Commands

Do not claim completion without running the relevant checks that exist in this repo.

Preferred order:

1. Focused C++ test executable:
   ```powershell
   cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_<area>_tests
   .\cc-feature-implementer\desktop\nara_collect_cpp\build\Release\nara_<area>_tests.exe
   ```
2. Full C++ test suite:
   ```powershell
   ctest --test-dir cc-feature-implementer/desktop/nara_collect_cpp/build -C Release --output-on-failure
   ```
3. Live C++ desktop & collector build:
   ```powershell
   cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_desktop nara_award_collect
   ```

Always report:

- exact commands run and exit codes
- pass/fail result
- unresolved issues or follow-ups

## Safety & Dependencies

- Never commit secrets (`.env`, `**/igunsul.local.env`).
- Never hardcode machine-specific user or drive paths in source code or rules.
- Do not add or upgrade external dependencies unless explicitly approved.
