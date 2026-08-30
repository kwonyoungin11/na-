# CMake / MSVC (this repo)

Root: `cc-feature-implementer/desktop/nara_collect_cpp/CMakeLists.txt`  
Build dir: `cc-feature-implementer/desktop/nara_collect_cpp/build`

```
cmake -S cc-feature-implementer/desktop/nara_collect_cpp -B cc-feature-implementer/desktop/nara_collect_cpp/build -G "Visual Studio 17 2022" -A x64
cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target <name>
```

Live GUI target: `nara_desktop`  
Collect target: `nara_award_collect`

## New test

1. `tests/test_foo.cpp` with `int main()` returning 0 / 1
2. `add_executable(nara_foo_tests tests/test_foo.cpp …)`
3. `target_include_directories(... PRIVATE include)`
4. `add_test(NAME nara_foo_tests COMMAND nara_foo_tests)`
5. MSVC: `target_compile_options(nara_foo_tests PRIVATE /utf-8 /W3)`
6. Link only what the test needs (`winsqlite3`, `winhttp`, …)

Run:

```
cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_foo_tests
cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_foo_tests.exe
```

## New source on nara_desktop

Add the `.cpp` to `add_executable(nara_desktop WIN32 …)` and the header under `include/nara/`. Reconfigure CMake after CMakeLists changes.

## Output

Release exe: `cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_desktop.exe`
