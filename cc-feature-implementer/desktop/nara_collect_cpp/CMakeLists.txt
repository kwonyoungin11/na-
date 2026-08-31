cmake_minimum_required(VERSION 3.20)
project(nara_igunsul_ingest LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(NARA_CONVERT_SRC
    src/igunsul_convert.cpp
    src/company_name.cpp
    src/xlsx_store.cpp
    third_party/miniz/miniz.c)

add_executable(nara_igunsul_ingest
    src/main.cpp
    ${NARA_CONVERT_SRC})

target_include_directories(nara_igunsul_ingest PRIVATE include third_party/miniz)

add_executable(nara_igunsul_convert_tests
    tests/test_igunsul_convert.cpp
    ${NARA_CONVERT_SRC})

add_executable(nara_scatter_chart_tests
    tests/test_scatter_chart.cpp
    src/scatter_chart.cpp
    ${NARA_CONVERT_SRC})

target_include_directories(nara_scatter_chart_tests PRIVATE include third_party/miniz)
add_test(NAME nara_scatter_chart_tests COMMAND nara_scatter_chart_tests)

add_executable(nara_company_name_tests
    tests/test_company_name.cpp
    src/company_name.cpp)
target_include_directories(nara_company_name_tests PRIVATE include)
add_test(NAME nara_company_name_tests COMMAND nara_company_name_tests)

add_executable(nara_notice_rules_tests
    tests/test_notice_rules.cpp
    src/notice_rules.cpp
    src/company_name.cpp)
target_include_directories(nara_notice_rules_tests PRIVATE include)
add_test(NAME nara_notice_rules_tests COMMAND nara_notice_rules_tests)

add_executable(nara_award_table_tests
    tests/test_award_table.cpp
    src/award_table.cpp
    src/notice_rules.cpp
    src/scatter_chart.cpp
    src/company_name.cpp)
target_include_directories(nara_award_table_tests PRIVATE include)
add_test(NAME nara_award_table_tests COMMAND nara_award_table_tests)

add_executable(nara_dual_snapshot_tests
    tests/test_dual_snapshot.cpp
    src/sqlite_store.cpp
    src/award_collect.cpp
    src/award_table.cpp
    src/notice_rules.cpp
    src/scatter_chart.cpp
    src/company_name.cpp)
target_include_directories(nara_dual_snapshot_tests PRIVATE include)
target_link_libraries(nara_dual_snapshot_tests PRIVATE winsqlite3)
add_test(NAME nara_dual_snapshot_tests COMMAND nara_dual_snapshot_tests)

add_executable(nara_sqlite_store_tests
    tests/test_sqlite_store.cpp
    src/sqlite_store.cpp
    src/award_collect.cpp
    src/notice_rules.cpp
    src/company_name.cpp)
target_include_directories(nara_sqlite_store_tests PRIVATE include)
target_link_libraries(nara_sqlite_store_tests PRIVATE winsqlite3)
add_test(NAME nara_sqlite_store_tests COMMAND nara_sqlite_store_tests)

add_executable(nara_chart_view_tests
    tests/test_chart_view.cpp
    src/chart_view.cpp)
target_include_directories(nara_chart_view_tests PRIVATE include)
add_test(NAME nara_chart_view_tests COMMAND nara_chart_view_tests)

add_executable(nara_chart_theme_tests
    tests/test_chart_theme.cpp)
target_include_directories(nara_chart_theme_tests PRIVATE include)
add_test(NAME nara_chart_theme_tests COMMAND nara_chart_theme_tests)

add_executable(nara_design_page_tests
    tests/test_design_page.cpp)
target_include_directories(nara_design_page_tests PRIVATE include)
add_test(NAME nara_design_page_tests COMMAND nara_design_page_tests)

add_executable(nara_histogram_chart_tests
    tests/test_histogram_chart.cpp
    src/company_name.cpp
    src/scatter_chart.cpp)
target_include_directories(nara_histogram_chart_tests PRIVATE include)
add_test(NAME nara_histogram_chart_tests COMMAND nara_histogram_chart_tests)

add_executable(nara_dongguk_band_tests
    tests/test_dongguk_band.cpp
    src/dongguk_band.cpp
    src/company_name.cpp)
target_include_directories(nara_dongguk_band_tests PRIVATE include)
add_test(NAME nara_dongguk_band_tests COMMAND nara_dongguk_band_tests)

add_executable(nara_first_page_dash_tests
    tests/test_first_page_dash.cpp
    src/first_page_dash.cpp
    src/scatter_chart.cpp
    src/notice_rules.cpp
    src/company_name.cpp)
target_include_directories(nara_first_page_dash_tests PRIVATE include)
add_test(NAME nara_first_page_dash_tests COMMAND nara_first_page_dash_tests)

add_executable(nara_scatter_cache_tests
    tests/test_scatter_cache.cpp)
target_include_directories(nara_scatter_cache_tests PRIVATE include)
add_test(NAME nara_scatter_cache_tests COMMAND nara_scatter_cache_tests)

add_executable(nara_desktop WIN32
    src/desktop_main.cpp
    src/sqlite_store.cpp
    src/award_collect.cpp
    src/award_table.cpp
    src/notice_rules.cpp
    src/scatter_chart.cpp
    src/company_name.cpp
    src/chart_popup.cpp
    src/chart_view.cpp
    src/dwrite_text.cpp
    src/dongguk_band.cpp
    src/first_page_dash.cpp
    src/first_page_mosaic.cpp
    src/design_principles_window.cpp)
target_include_directories(nara_desktop PRIVATE include)
target_compile_definitions(nara_desktop PRIVATE UNICODE _UNICODE)
target_link_libraries(nara_desktop PRIVATE user32 comctl32 gdi32 gdiplus ole32 uxtheme d2d1 dwrite winsqlite3)

add_executable(nara_dwrite_text_tests
    tests/test_dwrite_text.cpp
    src/dwrite_text.cpp)
target_include_directories(nara_dwrite_text_tests PRIVATE include)
target_link_libraries(nara_dwrite_text_tests PRIVATE d2d1 dwrite ole32)
add_test(NAME nara_dwrite_text_tests COMMAND nara_dwrite_text_tests)

target_include_directories(nara_igunsul_convert_tests PRIVATE include third_party/miniz)

add_executable(nara_award_collect
    src/award_collect_main.cpp
    src/award_collect.cpp
    src/http_get.cpp)
target_include_directories(nara_award_collect PRIVATE include)
target_link_libraries(nara_award_collect PRIVATE winhttp)

add_executable(nara_award_collect_tests
    tests/test_award_collect.cpp
    src/award_collect.cpp)
target_include_directories(nara_award_collect_tests PRIVATE include)
add_test(NAME nara_award_collect_tests COMMAND nara_award_collect_tests)

if (MSVC)
    target_compile_options(nara_igunsul_ingest PRIVATE /utf-8 /W3)
    target_compile_options(nara_igunsul_convert_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_scatter_chart_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_company_name_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_notice_rules_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_award_table_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_dual_snapshot_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_sqlite_store_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_chart_view_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_chart_theme_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_design_page_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_histogram_chart_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_dongguk_band_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_first_page_dash_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_dwrite_text_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_scatter_cache_tests PRIVATE /utf-8 /W3)
    target_compile_options(nara_desktop PRIVATE /utf-8 /W3)
    target_compile_options(nara_award_collect PRIVATE /utf-8 /W3)
    target_compile_options(nara_award_collect_tests PRIVATE /utf-8 /W3)
    set_source_files_properties(third_party/miniz/miniz.c PROPERTIES COMPILE_FLAGS "/wd4244 /wd4267 /wd4456")
endif()

enable_testing()
add_test(NAME nara_igunsul_convert_tests COMMAND nara_igunsul_convert_tests)
