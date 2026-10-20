# Win32, GDI+, DirectWrite & DPI Scaling

## 1. Rendering Stack

- `nara_desktop` is a native `WIN32` subsystem application linking `user32`, `comctl32`, `gdi32`, `gdiplus`, `ole32`, `uxtheme`, `d2d1`, `dwrite`, and `winsqlite3`.
- **Shapes & Fills**: GDI+ (`Gdiplus::Graphics`) with `ScaleTransform(g_dpi_scale, g_dpi_scale)`.
- **Text Rendering**: All Hangul and UI text must go through `nara::dwrite_text` (`Queue`, `QueueBox`, `Flush`), using `Malgun Gothic` and DirectWrite (`DWRITE_RENDERING_MODE_NATURAL_SYMMETRIC`). Never use `Gdiplus::Graphics::DrawString` or `AddString` for UI/chip text.
- **Plot Clipping**: Queue chip labels with plot clipping enabled (`clip_plot`) so glyph strokes never bleed into axes or toolbars.

## 2. Per-Monitor DPI v2

- Enable `DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2` at startup and handle `WM_DPICHANGED`.
- Keep all layout numbers in DIPs (`96 DPI = 1.0f` via `chart_theme::DpiScaleFromDpi`).
- Convert to physical pixels (`DipToPx`) only at Win32 HWND boundaries (`MoveWindow`, `CreateDIBSection`, `BitBlt`).

## 3. UTF-8 & Wide Path Hygiene

- Always pass wide strings (`std::filesystem::path::wstring()`, `PathExistsWide`, `AwardCollectGapCommandLineW`) to Win32 file/process APIs (`CreateProcessW`, `GetFileAttributesW`).
- Always pass UTF-8 strings (`path.u8string()`, `PathUtf8`) to `sqlite3_open` so Korean user/folder names never fail to open the database.
