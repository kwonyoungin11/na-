# Design System, Layout & Battlefield Map Tokens

All visible dimensions, layout rectangles, and ARGB colors reside in `cc-feature-implementer/desktop/nara_collect_cpp/include/nara/chart_theme.h` and are verified by `tests/test_chart_theme.cpp`. Never hardcode a second palette in `desktop_main.cpp` or `chart_popup.cpp`.

## 1. Core Color Tokens

| Token | Value | Purpose |
| :--- | :--- | :--- |
| `kWindowBgArgb` | `#D6F2FF` / page wash | Main window background |
| `kBgArgb` | `#F6F8FB` | Chart surface |
| `kPlotBgArgb` | `#FFFFFF` | Scatter plot area |
| `kCardFillArgb` | `#FFFFFF` | Card fill |
| `kTextArgb` | `#111111` | Primary body text |
| `kSubtleTextArgb` | `#5A6473` | Secondary caption text |
| `kDonggukArgb` | `#FF0000` | 동국 chip fill |
| `kRankGoldArgb` | `#FACC15` | 1위 chip inner border |
| `kRankGoldOuterArgb` | `#CA8A04` | 1위 chip outer border |
| `kChipBorderArgb` | `#21252B` | Standard chip border |
| `kCompanyPalette` | 32 distinct colors | Competing company chips |

## 2. Battlefield Map (전장 맵) Rules

- **Axes**: X = 기초대비사정률 `99.0–101.0` (step `0.01`, 201 columns), Y = 공고 (oldest at top, newest at bottom via `SortNoticeKeysOldestFirst`).
- **Chips**:
  - Rectangular (`kChipRadius == 0`), sized by `kCellPx` (`20 DIP`) × zoom.
  - Labels: 2-character Hangul (`BuildDisplayMap` for collision-free short labels), clipped strictly inside the cell.
  - Overlapping chips in the same bucket: 동국 (`#FF0000`) is painted last (top of z-order), and hit-testing iterates back-to-front so the visible top chip is selected first.
- **Pan & Zoom Performance**:
  - Pan uses overscan bitmap `BitBlt` with labels already rendered.
  - Zoom uses `StretchBlt` of the last crisp plot bitmap while the mouse wheel is moving, and rebuilds once after `ZoomSettleMs`.

## 3. Chrome & Layout Rules

- All layout helpers (`LayoutMainMosaic`, `LayoutMainToolbar`, etc.) compute coordinates in **96-DPI DIPs**.
- Hit-testing (`WM_LBUTTONDOWN`, `WM_MOUSEMOVE`) must convert physical pixels via `PxToDip` and consume the exact same DIP rects returned by `chart_theme.h`.
- Owner-draw buttons (`WM_DRAWITEM`) must paint the parent window's local page gradient/bloom wash (`PaintPageWashLocal`) before filling the rounded button path so corners never show rectangular artifacts.
