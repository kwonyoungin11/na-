# Forced skills: superpowers + unlazy

These two skills are mandatory at their usage points. Hooks inject the reminder and can block a done report.

## Superpowers (implement)

Before writing production `.cpp` / `.h` / `.cs`:

1. Read `.grok/skills/superpowers/SKILL.md`
2. Read `.grok/skills/test-driven-development/SKILL.md`
3. Follow `.grok/skills/cpp-tdd-cmake/SKILL.md` for this repo's test exe

Iron law: no production code without a failing test first. Watch `nara_*_tests.exe` FAIL, then edit production, then run the **same** exe and watch PASS.

## Unlazy (complete)

Before reporting substantial work done:

1. Read `.grok/skills/unlazy/SKILL.md`
2. For multi-step work, write `GATES.md` from `.grok/skills/unlazy/templates/gates-leaf.md`
3. Runnable gates use `CHECK:` + `EXPECT:`. Inspect with `node .grok/skills/unlazy/scripts/gate-check.mjs --status GATES.md` (does not execute)
4. Report only commands actually run, with pass/fail

Do not write gates for a factual reply or a config-only edit.
