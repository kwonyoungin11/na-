---
name: cpp-tdd-cmake
description: Use when adding or changing C++ behavior, before writing production .cpp. Covers failing test_*.cpp, CMake add_test, Release test exe, theme number/color tests. Slash /cpp-tdd-cmake.
---

# C++ TDD (CMake test exe)

Also load `.grok/skills/superpowers/SKILL.md` and `.grok/skills/test-driven-development/SKILL.md` (forced on production edits).

Write a failing test. Watch it fail. Smallest production fix. Run the **same** test.

No exceptions: do not keep untested production code as "reference". Delete it and start from the test.

## Pattern in this repo

Tests are standalone `int main()` exes, not gtest.

```cpp
int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

int main() {
    if (/* invariant broken */) {
        return Fail("…");
    }
    return 0;
}
```

Put helpers (color, DIP, tokens) in `include/nara/chart_theme.h` or the domain header so the test can include them without the Win32 window.

## Add a test

1. `tests/test_<area>.cpp`
2. CMake `add_executable` + `add_test` + `/utf-8 /W3` (see `cpp-2026` `references/cmake.md`)
3. `cmake --build … --config Release --target nara_<area>_tests`
4. Run `build/Release/nara_<area>_tests.exe` — expect FAIL
5. Edit production
6. Run the same exe — expect PASS

## What to test

- Rank / chip count / 동국 rank: `BuildScatterNotice`, `IsMappedRank1`
- Region tokens: `전국` | `경북`
- Collect lookback, 영향진단 drop, 6-company gate
- UI numbers: `chart_theme` constexpr (cell 20, gold `#FACC15`, DIP 96→1.0)
- DirectWrite queue / clip: `tests/test_dwrite_text.cpp`

## Red flags — stop

- Production `.cpp` before a failing exe
- "I'll run the desktop and look"
- Changing a different test to make it pass
- Python test for C++ chart math

Report: exact command, pass/fail.
