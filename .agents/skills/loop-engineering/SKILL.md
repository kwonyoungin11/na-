---
name: loop-engineering
description: Use when implementing or fixing anything that must be proven: a failing test first, then the smallest change, then the same check again on failure. Use for TDD, collect/chart regressions, and any "it still fails" loop.
---

# Loop engineering

Do not stop after one pass.

1. Hypothesis: name the failing check (test name, UI symptom, crash log line).
2. Red: write or run a check that fails for that hypothesis.
3. Green: smallest change that makes that check pass.
4. Re-run the same check. If it fails, do not switch hypotheses until this one is disproven.
5. Only then run the next check (broader tests, build, launch).

Stop-and-fix: a failed validation command blocks the next milestone.

Desktop: after a desktop milestone, launch the Debug exe via explorer (do not let the shell Job kill it).
