# CMake Test-Driven Development Workflow

## 1. TDD Loop

```text
Hypothesis → Failing C++ Test Executable → Smallest Production Fix → Re-run Same Test Executable
```

Never write production `.cpp` changes before observing a failing test.

## 2. Standalone Test Executables

Each test suite in `cc-feature-implementer/desktop/nara_collect_cpp/tests/test_*.cpp` is a fast, standalone `int main()` executable registered via `add_executable` and `add_test` in `CMakeLists.txt` with `/utf-8 /W3`.

```cpp
#include <iostream>

namespace {
int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}
}  // namespace

int main() {
    if (/* invariant broken */) {
        return Fail("expected invariant");
    }
    return 0;
}
```

## 3. Build & Verification Commands

Run from the repository root:

1. **Configure (if `build/CMakeCache.txt` is missing)**:
   ```powershell
   cmake -S cc-feature-implementer/desktop/nara_collect_cpp -B cc-feature-implementer/desktop/nara_collect_cpp/build -G "Visual Studio 17 2022" -A x64
   ```
2. **Build & Run a Focused Test**:
   ```powershell
   cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_scatter_chart_tests
   .\cc-feature-implementer\desktop\nara_collect_cpp\build\Release\nara_scatter_chart_tests.exe
   ```
3. **Run Full C++ Test Suite**:
   ```powershell
   ctest --test-dir cc-feature-implementer/desktop/nara_collect_cpp/build -C Release --output-on-failure
   ```
4. **Build Live Desktop & Collector Executables**:
   ```powershell
   cmake --build cc-feature-implementer/desktop/nara_collect_cpp/build --config Release --target nara_desktop nara_award_collect
   ```
