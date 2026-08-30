---
name: cpp-2026
description: Use when writing, compiling, or changing any C++ in this repo (.cpp .h CMake MSVC C++17 Win32). Covers new targets, headers, C++20/23 opt-in, and routing to the other cpp-* skills. Slash /cpp-2026.
---

# C++ 2026 (this repo)

C++17, MSVC 2022 x64, CMake. Do not bump `CMAKE_CXX_STANDARD` unless the user asks. Do not add libraries when Win32 / winsqlite3 / WinHTTP already cover it.

## Route

| Task | Skill |
|------|--------|
| 표·전장 맵·지역·스냅샷 | `cpp-nara-desktop` |
| 실패 테스트 먼저 | `cpp-tdd-cmake` |
| HWND / GDI+ / D2D / DWrite | `cpp-win32-desktop` |
| 수집·jsonl·sqlite | `cpp-http-sqlite` |
| 크래시·exe 잠김·clangd | `cpp-debug-msvc` |
| 색·폰트·반경 | `cpp-design-system` |
| 칩·1위 금색·99–101 | `cpp-design-scatter` |
| 창·독·툴바·DIP | `cpp-design-layout` |

Heavy language/CMake notes: `references/language.md`, `references/cmake.md`.

## Layout

- Headers: `cc-feature-implementer/desktop/nara_collect_cpp/include/nara/`
- Sources: `.../src/`
- Tests: `.../tests/test_*.cpp` → one `add_executable` + `add_test` each
- Third party: `third_party/miniz` only. New deps need a reason.

## Defaults

- `/utf-8 /W3` on every new MSVC target
- `UNICODE` + `_UNICODE` on Win32 GUI
- UTF-8 paths for sqlite (`PathUtf8`)
- Numbers, colors, DIP in `chart_theme.h` (test them; do not hardcode in paint)
- clangd: `.cpp` / `.h` via `.grok/lsp.json`

## Do not

- Python collect
- C# Debug exe when the user is on C++ desktop
- New abstraction when an existing `nara::` helper exists
