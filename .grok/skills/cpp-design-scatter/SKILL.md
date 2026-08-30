---
name: cpp-design-scatter
description: Use when designing or changing the C++ battlefield map — chips, 1위 gold, 99-101 axis, pan/zoom blit, 1위 toggle, company filter, hover, short Hangul labels. Slash /cpp-design-scatter.
---

# Battlefield map design

Same snapshot as the table. X = 기초대비사정률 99–101, Y = 공고(개찰일시).

## Chips

- Rectangle, not rounded (`kChipRadius 0`)
- Size from `kCellPx` (20 DIP) × zoom
- Label: **2 Hangul**, clipped in the cell. Never let glyphs spill into the neighbor (`화서`+`미리` → `화사미리` is a listed bug)
- 동국 `#FF0000` on top of z-order among equals; 1위 gold border on the awarded chip that `IsMappedRank1`
- 1위 toggle (`ChipPassesRank1Filter`) hides non-1위 chips. Do not add a second 1위 view

## Motion

- Pan: overscan BitBlt
- Zoom: StretchBlt until the wheel settles, then rebuild
- Hover callout uses theme hover fill/radius; keep it inside the plot clip

## What not to draw

- Fake 100 cluster as gold
- OpenAPI 86.x as X
- Heatmap / 과열 overlay
- Duplicate 1위-only tab (the 1위 button already does that)

## Files

`src/chart_popup.cpp`, `src/scatter_chart.cpp`, `src/dwrite_text.cpp`, `include/nara/chart_theme.h`, `tests/test_chart_theme.cpp`, `tests/test_scatter_chart.cpp`.
