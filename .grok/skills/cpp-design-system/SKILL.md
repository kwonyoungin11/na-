---
name: cpp-design-system
description: Use when designing or restyling any C++ desktop visual — color, font, radius, stroke, shadow, palette, tokens in chart_theme.h, new control look. Slash /cpp-design-system.
---

# C++ design system

Every visible number and color lives in `include/nara/chart_theme.h` and is asserted in `tests/test_chart_theme.cpp`. Paint code reads tokens. Do not invent a second palette in `chart_popup.cpp`.

## Surfaces

| Token | Value | Use |
|-------|--------|-----|
| `kWindowBgArgb` | `#F3F5F8` | Window |
| `kBgArgb` | `#F6F8FB` | Chart card |
| `kPlotBgArgb` | `#FFFFFF` | Plot |
| `kCardFillArgb` | `#FFFFFF` | Cards |
| `kDockFillArgb` | glass white | Bottom dock |
| `kTextArgb` | `#111111` | Body |
| `kSubtleTextArgb` | `#5A6473` | Captions |
| `kAccentArgb` | `#D97706` | Accent / 1위 pill on |
| `kDonggukArgb` | `#FF0000` | 동국 chip |
| `kRankGoldArgb` | `#FACC15` | 1위 border |
| `kRankGoldOuterArgb` | `#CA8A04` | 1위 outer |
| `kChipBorderArgb` | `#21252B` | Chip outline |
| `kCompanyPalette` | 32 colors | Other firms |

## Type

- Family: `Malgun Gothic`
- DirectWrite NATURAL_SYMMETRIC (`kTextUsesDirectWrite`)
- Axis 10, caption 12, body 13, reset 13, chip 4–11, hover 9.8

## Radius

Card 22, map inner / plot / dock / toolbar 16, reset 14, combo 12, hover 6, chip **0** (rectangle).

## New visual

1. Add constexpr in `chart_theme.h`
2. Fail `test_chart_theme.cpp` (or a new test) without the paint
3. Paint with the token
4. DIP: `DpiScaleFromDpi` / `DipToPx`. 20 DIP cell is 30 px at 150%

Any C++ visual (dialog, badge, empty state, new chart) starts here. Scatter specifics → `cpp-design-scatter`. Chrome/layout → `cpp-design-layout`.
