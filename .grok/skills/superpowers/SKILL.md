---
name: superpowers
description: Forced Superpowers TDD and verification for this repo. Use when implementing, fixing, refactoring C++/C#, or claiming that work is done. Slash /superpowers.
---

# Superpowers (this repo)

Vendored from [obra/superpowers](https://github.com/obra/superpowers). This project **forces** TDD at implementation and verification at completion via `.grok/hooks/force-two-skills.json`.

## Load in this order

1. `.grok/skills/test-driven-development/SKILL.md` — iron law, red-green-refactor
2. `.grok/skills/cpp-tdd-cmake/SKILL.md` — `tests/test_*.cpp`, Release `nara_*_tests.exe`
3. `.grok/skills/verification-before-completion/SKILL.md` — no done claim without a fresh command
4. `.grok/skills/using-superpowers/SKILL.md` — pack bootstrap (optional except when starting a large implementation)

## Iron law

```
NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST
```

If production `.cpp` / `.h` / `.cs` was written first, delete it and start from the failing test. Do not keep it as "reference".

## This repo's check

```text
cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_<area>_tests
cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_<area>_tests.exe
```

Same exe must FAIL, then PASS. Report the exact command and pass/fail.

## Pair with unlazy

Substantial or multi-step work also loads `.grok/skills/unlazy/SKILL.md` and writes `GATES.md` before coding.
