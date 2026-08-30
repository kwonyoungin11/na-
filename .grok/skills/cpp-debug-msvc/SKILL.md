---
name: cpp-debug-msvc
description: Use when the C++ desktop crashes, hangs, shows a blank chart, clangd reports errors, or nara_desktop.exe is locked so Release build cannot link. Slash /cpp-debug-msvc.
---

# Debug C++ desktop (MSVC)

## Locked exe

`nara_desktop.exe` in use → link fails.

1. `Get-Process nara_desktop`
2. Stop it
3. Rebuild Release `nara_desktop`
4. Explorer launch

## Blank / wrong chart

Do not guess. Check in order:

1. Same snapshot as the table (`g_snapshot`), not a second sqlite read
2. Region combo actually slices (`전국` | `경북`)
3. Period `InPeriod` on `open_date`
4. Chart rules: 동국 notices only
5. Rates in 99–101 are 기초대비사정률, not 86.x `bidprcrt`
6. Fake 100 cluster vs real 1위 (`NoticeHasLowerLimitAwardCluster` / `IsMappedRank1`)

## Crash

- Reproduce with the Release exe the user runs
- Read the last paint/load path: `desktop_main.cpp` `ReloadTable`, `chart_popup.cpp` blit
- clangd on the edited `.cpp`/`.h` (`.grok/lsp.json`). Fix diagnostics before claiming done

## Hang

Collect is WinHTTP. UI must stay pumping messages. Do not join a long collect on the UI thread.

## Verify

Focused test exe first, then `nara_desktop` build. Report commands and pass/fail.
