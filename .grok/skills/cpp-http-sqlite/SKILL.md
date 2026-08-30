---
name: cpp-http-sqlite
description: Use when changing C++ nara_award_collect, WinHTTP, jsonl, sqlite merge, UTF-8 db paths, lookback months, 참가가능지역, ordering_agency. Slash /cpp-http-sqlite.
---

# Collect → jsonl → sqlite

Entry: `nara_award_collect` (C++). Python collect is forbidden.

## Flow

1. `nara_award_collect` writes jsonl next to the exe
2. Desktop merges jsonl into `nara-dashboard.db` in the app folder (portable, not a hard-coded `A:\c\…` only path)
3. `ReloadTable` reads sqlite → `g_snapshot` → table and chart

Empty list-region is not `전국`. Parse 참가가능지역 (`prtcptPsblRgn`). Keep `전국` | `경북` only at merge. Drop 전북 and the rest.

Lookback is local today minus `kCollectLookbackMonths` (currently 12). Query OpenAPI in 3-month windows. Persist filter (6 companies, 영향진단) still applies before display.

## Files

- `src/award_collect.cpp` / `award_collect_main.cpp` / `http_get.cpp`
- `src/sqlite_store.cpp` — `PathUtf8`, `CREATE TABLE participants`
- Tests: `test_award_collect.cpp`, `test_sqlite_store.cpp`

jsonl must include `ordering_agency`. Do not store `bidprcrt` as 기초대비사정률. Foundation amount is PreparPcDetail `bssamt`.

## After collect

Merge into sqlite, then reload the snapshot. Running collect and then painting an old db is a listed mistake.
