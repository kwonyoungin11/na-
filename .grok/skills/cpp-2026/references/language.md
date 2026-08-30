# C++ language (MSVC 2022, this repo)

## Standard

Repo is **C++17**. Use C++20/23 only when the user asks and CMake is updated.

Prefer:

- `enum class`, `override`, `= delete`
- `std::optional` for missing rates (already on `ParticipantRow`)
- range-for, structured bindings
- `std::filesystem` for paths
- `string_view` for read-only text that is not UTF-16 UI

Avoid:

- `using namespace std;` in headers
- throwing across Win32 WndProc unless already the local pattern
- `new`/`delete` when `vector` / stack objects work
- macros for constants that belong in `chart_theme.h`
- `getenv` without a UTF-8 path helper (sqlite already has `PathUtf8`)

## Strings

- UI and GDI+/DWrite: `wchar_t` / `std::wstring`
- sqlite, jsonl, OpenAPI: UTF-8 `std::string`
- Convert at the boundary. Do not store Korean UI labels as `char` literals.

## Headers

- `#pragma once`
- include what you use
- keep Win32 after `NOMINMAX` when both are needed (see `sqlite_store.cpp`)

## Concurrency

Desktop is single-threaded UI plus collect worker. Do not touch `g_snapshot` / table from a worker without the existing marshal path. If none exists, do not invent a thread; collect writes jsonl, UI merges on the UI thread.
