---
name: graph-engineering
description: Use before editing collect, sqlite, table, or 전장 맵 code. Map the data/call graph first, then change the smallest node. Use when 업데이트, 낙찰공고, and 전장 맵 must stay on the same snapshot.
---

# Graph engineering

Draw the path before editing:

`업데이트/CollectDataCommand` → C++ `nara_award_collect` jsonl → sqlite merge → `ApplySnapshot` → `CurrentParticipants` + 낙찰 표 → `SnapshotApplied`/`AwardCollectApplied` → chart `CopyPopupFilterStateFrom` → `NativeBidChartControl` (`ScatterChartRules`).

Rules:

- Chart reads the table snapshot. Do not re-query a stale store for the map.
- Chip z-order and labels are `ScatterChartRules` / `CompanyNamePresentation`, not stored Rank.
- Edit one node on this graph unless a second node is required for the same bug.
