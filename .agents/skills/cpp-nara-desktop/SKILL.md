---
name: cpp-nara-desktop
description: >-
  Use this skill when modifying, debugging, or testing the C++ nara desktop
  application (nara_collect_cpp / nara_desktop.exe / nara_award_collect.exe),
  including OpenAPI collection, JSONL-to-SQLite merge, award notice table,
  region filtering, scatter battlefield map (전장 맵), DirectWrite/GDI+ rendering,
  and CMake TDD tests.
---

# C++ Nara Desktop (`nara_collect_cpp`)

Before making any chart, collection, or table changes, read [.agents/MISTAKES.md](../../MISTAKES.md).

## 1. Data & UI Pipeline Graph

```text
업데이트 (UI)
  → nara_award_collect.exe (C++ OpenAPI WinHTTP)
  → award_notices.jsonl
  → SQLite Merge (nara-dashboard.db)
  → ReloadTable (BuildChartUniverse / BuildAwardNoticeRows)
  → g_snapshot (In-Memory Participant Snapshot)
  → 전장 맵 (BuildScatterNotice)
```

- **Single Snapshot Rule**: The battlefield map (`chart_popup.cpp`) copies the in-memory `g_snapshot` from the main window. It must never re-open SQLite independently.
- **Smallest Node**: Modify only the minimal node in the pipeline needed to satisfy the failing test.

## 2. Live Application & Build Paths

- **Source Root**: `cc-feature-implementer/desktop/nara_collect_cpp/`
- **Live C++ Desktop Executable**: `cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_desktop.exe`
- **Live C++ Collector Executable**: `cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_award_collect.exe`
- Do not launch the C# Debug executable unless the user explicitly asks for the C# app.
- If `nara_desktop.exe` is locked during linking, stop the running `nara_desktop` process, build the `Release` target, and relaunch it on the interactive `Default` desktop.

## 3. Domain Invariants

- **Award Notices Only**: Never collect, store, or chart `국가유산영향진단` (`배수영향구간` is allowed and is not 영향진단).
- **Minimum Bidders**: Drop any notice with fewer than 6 unique companies (`< 6`).
- **Chart Universe**: Keep only notices where 동국 (`동국문화유산연구원` / `동국문화재연구원`) participated; preserve all joint/competing bidders in those notices.
- **Region Tokens (`전국` | `경북`)**:
  - Allowed tokens are strictly `전국` and `경북`.
  - Normalize `경상북도` / `경상북` / `대구,경북` to `경북`. Never expose `대구` in the UI, and never mix `전국` and `경북` notices in the same view.
  - Empty list-region from OpenAPI is not automatically `전국`; query `getBidPblancListInfoPrtcptPsblRgn` (참가가능지역).
- **Period Slicing**: Main table and battlefield map share the same `g_start` / `g_end` (`open_date`) window via `InPeriod`.
- **Rank & Chip Count**:
  - Table rank = 동국's rank among unique companies inside the `99.0–101.0` window (`FormatDonggukRankLabel`), never the raw OpenAPI `Rank`.
  - The `N개사` label must match the rendered chip count.
- **Adjust Rate & 1위 Gold**:
  - Chart X-axis is 기초대비사정률 (`ComputeBaseAdjustRate(투찰금액, bssamt, 하한)` in `99.0–101.0`), never raw `bidprcrt` (`86.x`) or `96.x`.
  - Gold border (`IsMappedRank1`) applies only to the true 1st-place awarded chip; filter out fake 100.00 clusters via `NoticeHasLowerLimitAwardCluster`.

## 4. Source File Routing

| Area | Files |
| :--- | :--- |
| OpenAPI Collect / Region / Lower Limit | `src/award_collect.cpp`, `include/nara/award_collect.h`, `tests/test_award_collect.cpp` |
| SQLite Merge / UTF-8 Path | `src/sqlite_store.cpp`, `include/nara/sqlite_store.h`, `tests/test_sqlite_store.cpp` |
| Main Table / Region Slice | `src/award_table.cpp`, `include/nara/award_table.h`, `tests/test_award_table.cpp` |
| Persist & 동국 Gate Rules | `src/notice_rules.cpp`, `include/nara/notice_rules.h`, `tests/test_notice_rules.cpp` |
| 99–101 Scatter Chips & 1위 | `src/scatter_chart.cpp`, `include/nara/scatter_chart.h`, `tests/test_scatter_chart.cpp` |
| 동국 Band & First Page Mosaic | `src/dongguk_band.cpp`, `src/first_page_mosaic.cpp`, `tests/test_dongguk_band.cpp` |
| Main Window (Win32) | `src/desktop_main.cpp` |
| Battlefield Map Popup | `src/chart_popup.cpp`, `src/chart_view.cpp`, `tests/test_chart_view.cpp` |
| Theme Tokens & Layout Rects | `include/nara/chart_theme.h`, `tests/test_chart_theme.cpp` |
| DirectWrite Text Queue | `src/dwrite_text.cpp`, `include/nara/dwrite_text.h`, `tests/test_dwrite_text.cpp` |

## 5. Detailed References (Load on Demand)

- **Design System, Layout & Battlefield Map Tokens**: [references/design-tokens.md](./references/design-tokens.md)
- **Win32, GDI+, DirectWrite & DPI Scaling**: [references/win32-dwrite.md](./references/win32-dwrite.md)
- **CMake Test-Driven Development Workflow**: [references/cmake-tdd.md](./references/cmake-tdd.md)
