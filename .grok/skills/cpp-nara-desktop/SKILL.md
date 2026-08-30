---
name: cpp-nara-desktop
description: Use when changing the C++ nara desktop graph — 업데이트, nara_award_collect, jsonl, sqlite, 표, g_snapshot, 전장 맵, 전국/경북, 1위 칩, 동국 순위, nara_desktop.exe. Slash /cpp-nara-desktop.
---

# nara C++ desktop

Read `.agents/MISTAKES.md` before chart, collect, or table edits.

## Graph

`업데이트` → C++ `nara_award_collect` jsonl → sqlite → 표 → `g_snapshot` → 전장 맵.

Map copies the table snapshot. Map does not open sqlite again.

## Live app

`cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_desktop.exe`

Launch with explorer. Do not launch C# Debug unless the user asked for C#. If the exe is locked, kill `nara_desktop`, rebuild, launch again.

## Invariants

- 낙찰 only. No 영향진단 in collect/store/chart (`배수영향구간` is not 영향진단)
- Unique companies ≥ 6 or drop the notice
- Chart universe: 동국 참여 공고 only; joint bidders stay
- Region tokens: `전국` | `경북` only. `경상북도` / `경상북` → `경북`. No 대구 in UI. Do not mix regions. Changing the combo without slicing data is a bug
- Period: same start/end as the table via `InPeriod` (open_date). Do not hardcode `"전국"` in ReloadTable / footer / chart
- Chip label: 2 Hangul, clipped inside the cell (`CompanyNamePresentation` / C++ short label)
- Rank on the table = unique companies in the 99–101 window, not stored OpenAPI Rank. Phrase `N개사` matches chip count
- 1위 gold: `IsMappedRank1`. Fake 100 cluster is `NoticeHasLowerLimitAwardCluster` only. Chart X is 기초대비사정률 (`ComputeBaseAdjustRate`). Do not treat `bidprcrt` 86.x as 99–101

## Nodes

| Change | Files |
|--------|--------|
| Collect / 하한 / 지역 API | `src/award_collect.cpp`, `include/nara/award_collect.h`, `tests/test_award_collect.cpp` |
| sqlite merge / UTF-8 path | `src/sqlite_store.cpp` |
| 표 / region slice | `src/award_table.cpp`, `tests/test_award_table.cpp` |
| persist / 동국 gate | `src/notice_rules.cpp` |
| 99–101 chips / 1위 | `src/scatter_chart.cpp` |
| Main window | `src/desktop_main.cpp` |
| Chart popup | `src/chart_popup.cpp` |
| DIP text | `src/dwrite_text.cpp` |

Smallest node only. **REQUIRED SUB-SKILL:** `cpp-tdd-cmake` before production edits.
