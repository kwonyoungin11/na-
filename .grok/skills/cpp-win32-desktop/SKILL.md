---
name: cpp-win32-desktop
description: Use when implementing or fixing C++ Win32 UI — HWND, WndProc, GDI+, Direct2D, DirectWrite, DPI scale, explorer launch, nara_desktop WIN32 subsystem. Slash /cpp-win32-desktop.
---

# Win32 / GDI+ / DirectWrite

## Stack

`nara_desktop` is `WIN32`. Links: `user32 comctl32 gdi32 gdiplus ole32 uxtheme d2d1 dwrite winsqlite3`.

- Layout in DIP (96). Scale with `chart_theme::DpiScaleFromDpi`. Do not treat 20 as physical pixels
- Text: DirectWrite via `nara::dwrite_text` (`kTextUsesDirectWrite`). Queue jobs, clip to plot when needed
- Chips / fills: GDI+. Chip is a rectangle (`kChipRadius == 0`)
- Font family: `Malgun Gothic`

## Rules

- Hit-test and paint use the same DIP rects (`chart_theme` layout helpers)
- Labels 2 characters, clipped inside the chip. Do not grow font past the cell
- Rank-1 gold is a border (`kRankGoldArgb` / `kRankGoldOuterArgb`), not a fill swap
- 동국 fill is `#FF0000`
- Pan: overscan BitBlt. Zoom: StretchBlt until wheel settles (existing chart_popup path)
- Do not create WPF/C# controls for the live C++ app

## Launch

Explorer → `build/Release/nara_desktop.exe`. Shell Job must not kill the GUI.

If rebuild fails with file lock: stop `nara_desktop`, build, explorer again.

## New window / control

1. Fail a theme/layout test (sizes, DPI)
2. Add constants to `chart_theme.h`
3. Paint in `desktop_main.cpp` or `chart_popup.cpp`
4. Text through `dwrite_text::Queue` / `QueueBox`

**REQUIRED SUB-SKILL:** `cpp-design-system` for tokens, `cpp-design-layout` for chrome, `cpp-tdd-cmake` first.
