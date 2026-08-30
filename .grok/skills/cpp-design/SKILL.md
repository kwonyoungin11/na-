---
name: cpp-design
description: Use when inventing a new C++ visual that does not yet exist — dialog, empty state, about window, icon, print, new chart type, new control. Routes to cpp-design-system, scatter, layout. Slash /cpp-design.
---

# Any C++ visual

If it is on screen in `nara_desktop`, it is C++ (Win32 + GDI+ + DirectWrite), not WPF.

## Decide

| What | Skill |
|------|--------|
| Color / type / radius / palette | `cpp-design-system` |
| 전장 맵 chips / 1위 / pan zoom | `cpp-design-scatter` |
| Window, dock, toolbar, combo | `cpp-design-layout` |
| HWND / DPI / DWrite plumbing | `cpp-win32-desktop` |

## New thing (dialog, extra chart, overlay)

1. Put every size and ARGB in `chart_theme.h`
2. Fail `test_chart_theme.cpp` (or a dedicated test) with the new constants
3. Layout in DIP, scale with `DpiScaleFromDpi`
4. Text: `nara::dwrite_text`, Malgun Gothic, clip
5. 2-character chip labels if it is a company mark
6. Region UI is only `전국` | `경북`
7. Do not add a tab that repeats the 1위 toggle
8. Paint from `g_snapshot` if it shows bids

No second design language. No CSS, no XAML in the live exe.
