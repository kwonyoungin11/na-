---
name: cpp-design-layout
description: Use when laying out C++ desktop chrome — main window, table card, toolbar, region combo, chart popup dock, 1위/맵 초기화 pills, DIP hit-test, 20% dock shrink. Slash /cpp-design-layout.
---

# C++ chrome layout

Layout in DIP. Hit-test uses the same rects as paint (`chart_theme` `Layout*` helpers).

## Main window

- Min 1320×820
- Table card inset 20, pad 16×14
- Buttons height 40, radius 14: 업데이트 124, 정지 108, 차트 132
- Region: one control, two cells `전국` | `경북` with a divider. No 대구
- Company wrap 5 rows

`src/desktop_main.cpp` + `LayoutMainToolbar`.

## Chart popup

- Plot inset L220 T16 R22 B46
- 1위 pill 72×40 in the **toolbar**, not plot center
- 맵 초기화 to the right of 1위
- Bottom dock min 134, 20% smaller than the old dock; filters live here (기간, 정밀도, 지역, 최소/최대, 업체)
- Toolbar/card shadow: depth/blur tokens in `chart_theme.h`

Do not overlap the plot with a negative-margin dock.

## New chrome

1. Constants in `chart_theme.h`
2. Fail layout asserts in `test_chart_theme.cpp`
3. `Layout*` returns DIP rects
4. Paint + hit-test consume those rects
5. Text via DirectWrite, clipped

**REQUIRED SUB-SKILL:** `cpp-design-system`, `cpp-win32-desktop`.
