# C++ desktop exe port (option 2)

C# `NaraCollector.Desktop` is the behavior spec until dual-app verify passes. Do not invent a KPI strip or 입찰/낙찰 dual list.

**Goal:** One native exe that collects (existing `nara_award_collect`), loads the same sqlite snapshot, shows 동국 참여 낙찰공고, and paints the 전장 맵 from that snapshot.

**Path:** `cc-feature-implementer/desktop/nara_collect_cpp/`

## Graph

`업데이트` → `nara_award_collect` jsonl → sqlite merge → `ApplyChartRules` → 낙찰 표 → copy participants → 전장 맵 (`BuildScatterNotice`).

## Milestones

1. Notice rules + award table rows (this session)
   - `ApplyChartRules` / persist rules match C# `DonggukChartNoticeFilterTests`
   - Table label = `FormatDonggukRankLabel` (chip count, not stored Rank)
2. sqlite load (`participants` table, same columns as C#)
3. Win32 first screen: title, 업데이트/중지/차트 화면, 지역, 동국 참여 낙찰공고 ListView
4. 전장 맵 popup from the same in-memory snapshot (GDI+, 2-char chips, 99–101)
5. 업데이트 wires `nara_award_collect` 6 months, merge jsonl, refresh snapshot
6. dual-app-verify: notice count, chip count, 동국 rank vs C# on the same db

## Stop-and-fix

A failing C++ test blocks the next milestone. Do not ship a C++ first screen that disagrees with C# table ranks.
