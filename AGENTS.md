
# AGENTS.md

## Scope

This file contains durable repository instructions only.

- Put personal communication preferences in `~/.codex/AGENTS.md`.
- Put directory-specific rules in the closest nested `AGENTS.md`.
- Put repeatable multi-step workflows in `.agents/skills/`.
- Use `.agents/PLANS.md` (or a task-specific plan file) for complex or long-running work.

## Core Rules

- Do not guess. Inspect relevant files, tests, configs, and logs before editing.
- Prefer the smallest safe diff that solves the request.
- Follow existing repository patterns before introducing new abstractions.
- Do not change unrelated code unless required for correctness.
- Do not present assumptions as verified facts. State uncertainty clearly.

## Routing

- Start with the smallest set of files likely to answer the task.
- Prioritize these entry points first: repository instructions, root build/config files, and the nearest application or test directories that actually exist.
- If behavior is unclear, check the nearest relevant tests before widening the search.
- If you are reading too broadly, stop and narrow the search using file paths, build scripts, and test names.

## Planning

- For small or local changes, do not stop to present an upfront plan; inspect, edit, and verify.
- Use an ExecPlan (`.agents/PLANS.md` or a task-specific plan file) before major edits when the work is complex, ambiguous, cross-cutting, risky, or likely to require multiple edit/verify loops.
- An ExecPlan must define:

  - milestones
  - acceptance criteria
  - validation commands
  - a stop-and-fix rule if validation fails
- Keep the plan updated as work progresses.

## Execution

- Keep diffs scoped to the requested change.
- After each meaningful milestone, run the relevant checks and fix failures before moving on.
- Update docs when behavior, commands, configuration, or developer workflow changes.

## Verification

Do not claim completion without running the relevant checks that exist in this repo.

Preferred order:

1. focused test(s)
2. broader test suite when needed
3. lint
4. typecheck
5. build

Always report:

- exact commands run
- pass/fail result
- unresolved issues or follow-ups

## Safety

For auth, permissions, secrets, payments, infra, production config, migrations, data deletion, or security-sensitive changes:

- inspect surrounding code and callers before editing
- call out risks explicitly
- prefer reversible, narrowly scoped changes
- avoid opportunistic refactors

## Dependencies and CI

- Do not add or upgrade dependencies unless necessary.
- Reuse existing libraries, helpers, and patterns before introducing new ones.
- Call out dependency, lockfile, package manager, or CI/CD changes explicitly.

## Skills

- If the task matches a repeatable workflow (for example release notes, commit hygiene, docs refresh, dependency drift checks, or migration checklists), prefer the appropriate skill instead of embedding the whole procedure here.

## Repo Commands

Keep these commands exact and current for this repo.

- Add install, dev, test, lint, typecheck, and build commands only after confirming the repository toolchain.
- Do not guess command names, package managers, or scripts.

## Review Output

For team-facing summaries, keep the final report brief and review-friendly:

- changed files
- what changed
- verification run
- risks / side effects
- follow-up actions

## Maintenance

When a mistake, routing correction, or recurring review comment repeats, update the closest applicable `AGENTS.md` so the fix persists.
