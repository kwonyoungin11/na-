#define UNICODE
#define _UNICODE
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <commctrl.h>
#include <uxtheme.h>
#include <algorithm>
#include <cstdio>
#include <filesystem>
#include <set>
#include <string>
#include <vector>
#include <windowsx.h>
#include <gdiplus.h>

#include "nara/award_collect.h"
#include "nara/award_table.h"
#include "nara/chart_popup.h"
#include "nara/chart_theme.h"
#include "nara/notice_rules.h"
#include "nara/scatter_chart.h"
#include "nara/sqlite_store.h"

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "winsqlite3.lib")

namespace {

constexpr int kIdUpdate = 1001;
constexpr int kIdStop = 1002;
constexpr int kIdChart = 1003;
constexpr int kRowH = 38;
constexpr int kHeaderH = 40;
constexpr int kTableCardY = 348;

HWND g_mainHwnd = nullptr;
HWND g_btnUpdate = nullptr;
HWND g_btnStop = nullptr;
HWND g_btnChart = nullptr;
std::vector<nara::ParticipantRow> g_snapshot;
std::vector<nara::AwardNoticeRow> g_table;
std::string g_start;
std::string g_end;
std::string g_region_token = "전국";
int g_scroll = 0;
int g_selected = 0;
int g_notice_count = 0;
int g_lookback_months = nara::kCollectLookbackMonths;
std::string g_status;
RECT g_tableRect{};

std::wstring Utf8ToWide(const std::string& text) {
    if (text.empty()) {
        return {};
    }
    const int count = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring wide(count > 0 ? count - 1 : 0, L'\0');
    if (count > 1) {
        MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, wide.data(), count);
    }
    return wide;
}

std::string TodayMinusMonths(int months) {
    SYSTEMTIME now{};
    GetLocalTime(&now);
    return nara::YmdMinusMonths(now.wYear, now.wMonth, now.wDay, months);
}

std::string Today() {
    SYSTEMTIME now{};
    GetLocalTime(&now);
    return nara::FormatYmd(now.wYear, now.wMonth, now.wDay);
}

void RoundRectPath(Gdiplus::GraphicsPath& path, Gdiplus::RectF r, float radius) {
    const float d = radius * 2.0f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
    path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
    path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
    path.CloseFigure();
}

void ReloadTable(HWND hwnd) {
    g_end = Today();
    g_start = TodayMinusMonths(g_lookback_months);
    const auto live_db = nara::DefaultDashboardDbPath();
    auto raw = nara::LoadParticipants(live_db);
    const auto ref_db = nara::WorkspaceDashboardDbPath();
    if (std::filesystem::exists(ref_db)) {
        nara::PreferChartAdjustRates(raw, nara::LoadParticipants(ref_db));
    }
    auto rows = nara::BuildChartUniverse(raw, g_start, g_end, g_region_token, 99.0, 101.0);
    g_snapshot = rows;
    g_table = nara::BuildAwardNoticeRows(rows, g_start, g_end);
    g_notice_count = static_cast<int>(g_table.size());
    g_scroll = 0;
    g_selected = 0;
    if (nara::ChartWindowIsOpen()) {
        nara::RefreshChartSnapshot(g_snapshot, g_start, g_end, g_region_token);
    }
    InvalidateRect(hwnd, nullptr, FALSE);
}

void ApplyMergedSnapshot(HWND hwnd, const nara::JsonlMergeResult& merged) {
    if (merged.ok) {
        if (merged.notices > 0) {
            g_status += " · 누적 병합 " + std::to_string(merged.notices) + "공고";
        }
    } else if (!merged.error.empty()) {
        g_status += " · 병합 실패 " + merged.error;
    }
    ReloadTable(hwnd);
}

void OnUpdate(HWND hwnd) {
    nara::EnsurePortableDatabase();
    const auto db = nara::DefaultDashboardDbPath();
    const auto jsonl = nara::DefaultAwardJsonlPath();
    const auto pre = nara::MergeJsonlIntoStore(jsonl, db);
    g_status = pre.notices > 0 ? "기존 수집 반영" : "업데이트 시작";
    ApplyMergedSnapshot(hwnd, pre);

    const auto app_dir = nara::DefaultAppDir();
    const auto exe = std::filesystem::path(app_dir) / "nara_award_collect.exe";
    const auto exe_wide = Utf8ToWide(exe.string());
    if (GetFileAttributesW(exe_wide.c_str()) == INVALID_FILE_ATTRIBUTES) {
        g_status = "nara_award_collect.exe 없음";
        InvalidateRect(hwnd, nullptr, FALSE);
        return;
    }
    const auto env = nara::DefaultServiceKeyEnvPath();
    if (GetFileAttributesW(Utf8ToWide(env).c_str()) == INVALID_FILE_ATTRIBUTES) {
        g_status = "SERVICE_KEY 없음: 앱 폴더에 .env를 두세요";
        InvalidateRect(hwnd, nullptr, FALSE);
        return;
    }
    const auto start_month = TodayMinusMonths(g_lookback_months).substr(0, 7);
    const auto end_month = Today().substr(0, 7);
    const auto windows = nara::CollectThreeMonthWindows(start_month, end_month);
    const auto dir = Utf8ToWide(app_dir);
    for (size_t i = 0; i < windows.size(); ++i) {
        const auto& window = windows[i];
        g_status = "나라장터 월단위 수집 " + window.start_month + "~" + window.end_month + " ("
                   + std::to_string(i + 1) + "/" + std::to_string(windows.size()) + " · 3개월 창)";
        InvalidateRect(hwnd, nullptr, FALSE);
        UpdateWindow(hwnd);
        const auto cmd_utf8 =
            nara::AwardCollectCommandLine(exe.string(), window.start_month, window.end_month, jsonl, env);
        auto cmd = Utf8ToWide(cmd_utf8);
        std::vector<wchar_t> buf(cmd.begin(), cmd.end());
        buf.push_back(0);
        STARTUPINFOW si{};
        si.cb = sizeof(si);
        si.dwFlags = STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE;
        PROCESS_INFORMATION pi{};
        if (!CreateProcessW(nullptr, buf.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, dir.c_str(), &si,
                            &pi)) {
            g_status = "수집 프로세스 실행 실패 " + window.start_month + "~" + window.end_month;
            InvalidateRect(hwnd, nullptr, FALSE);
            return;
        }
        while (WaitForSingleObject(pi.hProcess, 200) == WAIT_TIMEOUT) {
            MSG msg{};
            while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
        DWORD code = 1;
        GetExitCodeProcess(pi.hProcess, &code);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        if (code != 0) {
            g_status = "수집 실패 " + window.start_month + "~" + window.end_month + " code="
                       + std::to_string(static_cast<int>(code));
            const auto failed = nara::MergeJsonlIntoStore(jsonl, db);
            ApplyMergedSnapshot(hwnd, failed);
            return;
        }
        g_status = "수집 반영 " + window.start_month + "~" + window.end_month;
        const auto chunk = nara::MergeJsonlIntoStore(jsonl, db);
        ApplyMergedSnapshot(hwnd, chunk);
    }
    g_status = "수집 완료 " + start_month + "~" + end_month + " · 3개월 창 "
               + std::to_string(windows.size()) + "개";
    InvalidateRect(hwnd, nullptr, FALSE);
}

void ApplyRegion(HWND hwnd, const std::string& token) {
    const auto next = nara::CanonicalizeRegion(token);
    if (next.empty() || next == g_region_token) {
        return;
    }
    g_region_token = next;
    ReloadTable(hwnd);
}

void OnChartRegion(const std::string& token) {
    if (g_mainHwnd) {
        ApplyRegion(g_mainHwnd, token);
    }
}

void PaintRegionSegment(Gdiplus::Graphics& g, const nara::chart_theme::RectI& slot, const Gdiplus::Font& font) {
    const auto seg = nara::chart_theme::SplitRegionSegment(slot);
    const int on = nara::chart_theme::RegionIndexFromToken(g_region_token);
    Gdiplus::RectF outer(static_cast<Gdiplus::REAL>(slot.x), static_cast<Gdiplus::REAL>(slot.y),
                         static_cast<Gdiplus::REAL>(slot.w), static_cast<Gdiplus::REAL>(slot.h));
    Gdiplus::GraphicsPath shell;
    RoundRectPath(shell, outer, static_cast<float>(nara::chart_theme::kRegionSegmentRadius));
    Gdiplus::SolidBrush idle_fill{Gdiplus::Color(nara::chart_theme::kCompanyPanelArgb)};
    g.FillPath(&idle_fill, &shell);
    const Gdiplus::GraphicsState saved = g.Save();
    g.SetClip(&shell);
    const auto& active = on == 0 ? seg.nationwide : seg.gyeongbuk;
    Gdiplus::SolidBrush on_fill{Gdiplus::Color(nara::chart_theme::kAccentSubtleArgb)};
    g.FillRectangle(&on_fill,
                    Gdiplus::RectF(static_cast<Gdiplus::REAL>(active.x), static_cast<Gdiplus::REAL>(active.y),
                                   static_cast<Gdiplus::REAL>(active.w), static_cast<Gdiplus::REAL>(active.h)));
    g.Restore(saved);
    Gdiplus::Pen border{Gdiplus::Color(nara::chart_theme::kComboStrokeArgb), nara::chart_theme::kComboStrokeW};
    g.DrawPath(&border, &shell);
    const Gdiplus::REAL mid = static_cast<Gdiplus::REAL>(seg.gyeongbuk.x) + 0.5f;
    g.DrawLine(&border, mid, outer.Y + 5.0f, mid, outer.GetBottom() - 5.0f);
    Gdiplus::StringFormat center;
    center.SetAlignment(Gdiplus::StringAlignmentCenter);
    center.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    auto label = [&](const nara::chart_theme::RectI& box, int index) {
        Gdiplus::RectF r(static_cast<Gdiplus::REAL>(box.x), static_cast<Gdiplus::REAL>(box.y),
                         static_cast<Gdiplus::REAL>(box.w), static_cast<Gdiplus::REAL>(box.h));
        Gdiplus::SolidBrush text(Gdiplus::Color(index == on ? nara::chart_theme::kAccentStrongArgb
                                                           : nara::chart_theme::kSubtleTextArgb));
        g.DrawString(nara::chart_theme::RegionLabelAt(index), -1, &font, r, &center, &text);
    };
    label(seg.nationwide, 0);
    label(seg.gyeongbuk, 1);
}

void LayoutMain(HWND hwnd) {
    RECT rc{};
    GetClientRect(hwnd, &rc);
    const auto tb = nara::chart_theme::LayoutMainToolbar(rc.right, kTableCardY);
    MoveWindow(g_btnChart, tb.chart.x, tb.chart.y, tb.chart.w, tb.chart.h, TRUE);
    MoveWindow(g_btnStop, tb.stop.x, tb.stop.y, tb.stop.w, tb.stop.h, TRUE);
    MoveWindow(g_btnUpdate, tb.update.x, tb.update.y, tb.update.w, tb.update.h, TRUE);
}

void PaintMain(HDC hdc, RECT rc) {
    const int w = rc.right;
    const int h = rc.bottom;
    HDC mem = CreateCompatibleDC(hdc);
    HBITMAP bmp = CreateCompatibleBitmap(hdc, w, h);
    HGDIOBJ old = SelectObject(mem, bmp);
    Gdiplus::Graphics g(mem);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    Gdiplus::LinearGradientBrush page(
        Gdiplus::Point(0, 0), Gdiplus::Point(w, h),
        Gdiplus::Color(255, 234, 242, 247), Gdiplus::Color(255, 238, 243, 248));
    g.FillRectangle(&page, 0, 0, w, h);
    Gdiplus::SolidBrush accent(Gdiplus::Color(255, 217, 119, 6));
    g.FillRectangle(&accent, 0, 0, w, 3);

    Gdiplus::FontFamily family(L"Malgun Gothic");
    Gdiplus::Font title(&family, 22.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font eyebrow(&family, 12.0f, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::Font body(&family, 13.0f, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::Font bodyBold(&family, 13.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font headerFont(&family, 12.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush ink(Gdiplus::Color(255, 17, 17, 17));
    Gdiplus::SolidBrush muted(Gdiplus::Color(255, 107, 114, 128));
    Gdiplus::SolidBrush copper(Gdiplus::Color(255, 185, 83, 9));
    Gdiplus::SolidBrush white(Gdiplus::Color(252, 255, 255, 255));
    Gdiplus::SolidBrush headerBg(Gdiplus::Color(255, 238, 242, 246));
    Gdiplus::SolidBrush stripe(Gdiplus::Color(255, 248, 250, 252));
    Gdiplus::SolidBrush select(Gdiplus::Color(255, 255, 247, 237));
    Gdiplus::SolidBrush plotFill(Gdiplus::Color(255, 247, 249, 252));
    Gdiplus::Pen copperPen(Gdiplus::Color(255, 231, 196, 140), 1.5f);

    Gdiplus::RectF hero(20, 16, static_cast<Gdiplus::REAL>(w - 40), 318);
    Gdiplus::GraphicsPath heroPath;
    RoundRectPath(heroPath, hero, 18);
    g.FillPath(&white, &heroPath);
    g.DrawPath(&copperPen, &heroPath);
    Gdiplus::SolidBrush accentBar(Gdiplus::Color(255, 217, 119, 6));
    g.FillRectangle(&accentBar, 28, 28, 3, 44);
    g.DrawString(L"낙찰분석프로그램", -1, &eyebrow, Gdiplus::PointF(44, 28), &copper);
    g.DrawString(L"동국문화유산연구원", -1, &title, Gdiplus::PointF(44, 46), &ink);
    wchar_t status_chip[80];
    swprintf_s(status_chip, L"%s · 공고 %d건", nara::chart_theme::RegionLabelAt(nara::chart_theme::RegionIndexFromToken(g_region_token)),
               g_notice_count > 0 ? g_notice_count : static_cast<int>(g_table.size()));
    Gdiplus::RectF chip_rect(static_cast<Gdiplus::REAL>(w - 196), 26, 156, 26);
    Gdiplus::GraphicsPath chipPath;
    RoundRectPath(chipPath, chip_rect, 13);
    Gdiplus::SolidBrush chipBg(Gdiplus::Color(230, 255, 255, 255));
    Gdiplus::Pen chipPen(Gdiplus::Color(255, 216, 224, 234), 1.0f);
    g.FillPath(&chipBg, &chipPath);
    g.DrawPath(&chipPen, &chipPath);
    Gdiplus::StringFormat chipFmt;
    chipFmt.SetAlignment(Gdiplus::StringAlignmentCenter);
    chipFmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    g.DrawString(status_chip, -1, &eyebrow, chip_rect, &chipFmt, &muted);

    if (g_table.size() >= 2) {
        const float plot_x = 56;
        const float plot_y = 96;
        const float plot_w = static_cast<float>(w - 120);
        const float plot_h = 168;
        const double lo = 98.9;
        const double hi = 101.2;
        Gdiplus::GraphicsPath plotPath;
        RoundRectPath(plotPath, Gdiplus::RectF(plot_x, plot_y, plot_w, plot_h), 12);
        g.FillPath(&plotFill, &plotPath);
        Gdiplus::Pen axis(Gdiplus::Color(255, 226, 232, 240), 1.0f);
        g.DrawPath(&axis, &plotPath);
        auto y_of = [&](double rate) {
            return plot_y + static_cast<Gdiplus::REAL>((hi - rate) / (hi - lo) * plot_h);
        };
        const double yticks[] = {98.9, 99.4, 100.0, 100.6, 101.2};
        for (double t : yticks) {
            const float y = y_of(t);
            g.DrawLine(&axis, plot_x, y, plot_x + plot_w, y);
            wchar_t lab[16];
            swprintf_s(lab, L"%.1f", t);
            g.DrawString(lab, -1, &eyebrow, Gdiplus::PointF(plot_x - 36, y - 8), &muted);
        }
        const int n = static_cast<int>(g_table.size());
        std::vector<Gdiplus::PointF> dongguk;
        std::vector<Gdiplus::PointF> award;
        for (int i = n - 1; i >= 0; --i) {
            const float x = plot_x + plot_w * static_cast<float>(n - 1 - i) / static_cast<float>(n - 1);
            dongguk.emplace_back(x, y_of(g_table[i].dongguk_rate));
            if (g_table[i].has_award_rate) {
                award.emplace_back(x, y_of(g_table[i].award_rate));
            }
        }
        Gdiplus::Pen blue(Gdiplus::Color(255, 29, 78, 216), 2.4f);
        Gdiplus::Pen pink(Gdiplus::Color(255, 251, 113, 133), 2.4f);
        g.SetClip(&plotPath);
        if (award.size() >= 2) {
            g.DrawLines(&pink, award.data(), static_cast<INT>(award.size()));
        }
        g.DrawLines(&blue, dongguk.data(), static_cast<INT>(dongguk.size()));
        Gdiplus::SolidBrush blueFill(Gdiplus::Color(255, 29, 78, 216));
        Gdiplus::SolidBrush pinkFill(Gdiplus::Color(255, 251, 113, 133));
        for (const auto& pt : dongguk) {
            g.FillEllipse(&blueFill, pt.X - 3.0f, pt.Y - 3.0f, 6.0f, 6.0f);
        }
        for (const auto& pt : award) {
            g.FillEllipse(&pinkFill, pt.X - 3.0f, pt.Y - 3.0f, 6.0f, 6.0f);
        }
        g.ResetClip();
        Gdiplus::RectF legend(plot_x + 12, plot_y + 10, 228, 44);
        Gdiplus::GraphicsPath legendPath;
        RoundRectPath(legendPath, legend, 8);
        g.FillPath(&white, &legendPath);
        g.DrawPath(&chipPen, &legendPath);
        g.FillEllipse(&blueFill, legend.X + 12, legend.Y + 10, 8.0f, 8.0f);
        g.DrawString(L"동국문화재연구원 기초대비사정율", -1, &eyebrow, Gdiplus::PointF(legend.X + 26, legend.Y + 6), &ink);
        g.FillEllipse(&pinkFill, legend.X + 12, legend.Y + 26, 8.0f, 8.0f);
        g.DrawString(L"같은 공고 실제 낙찰 사정율", -1, &eyebrow, Gdiplus::PointF(legend.X + 26, legend.Y + 22), &copper);
        const int ticks = 6;
        for (int i = 0; i <= ticks; ++i) {
            const int idx = (n - 1) * i / ticks;
            const float x = plot_x + plot_w * static_cast<float>(i) / ticks;
            auto d = g_table[n - 1 - idx].open_date;
            if (d.size() >= 10) {
                const auto label = Utf8ToWide(d.substr(5, 5));
                g.DrawString(label.c_str(), -1, &eyebrow, Gdiplus::PointF(x - 14, plot_y + plot_h + 4), &muted);
            }
        }
    }

    Gdiplus::RectF table_card(20, 348, static_cast<Gdiplus::REAL>(w - 40), static_cast<Gdiplus::REAL>(h - 400));
    Gdiplus::GraphicsPath tablePath;
    RoundRectPath(tablePath, table_card, 18);
    g.FillPath(&white, &tablePath);
    g.DrawPath(&copperPen, &tablePath);
    Gdiplus::Font section(&family, 18.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    const auto tb = nara::chart_theme::LayoutMainToolbar(w, kTableCardY);
    const float title_y = static_cast<float>(tb.update.y + (tb.update.h - 18) / 2);
    g.DrawString(L"동국 참여 낙찰공고", -1, &section,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>(nara::chart_theme::kMainCardInset
                                                            + nara::chart_theme::kMainTablePadX),
                                 title_y),
                 &ink);
    Gdiplus::StringFormat label_fmt;
    label_fmt.SetAlignment(Gdiplus::StringAlignmentFar);
    label_fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    g.DrawString(L"지역", -1, &eyebrow,
                 Gdiplus::RectF(static_cast<Gdiplus::REAL>(tb.region_label.x),
                                static_cast<Gdiplus::REAL>(tb.region_label.y),
                                static_cast<Gdiplus::REAL>(tb.region_label.w),
                                static_cast<Gdiplus::REAL>(tb.region_label.h)),
                 &label_fmt, &muted);
    PaintRegionSegment(g, tb.region_combo, eyebrow);
    std::string latest = g_table.empty() ? "" : g_table.front().open_date;
    if (latest.size() >= 16) {
        latest = latest.substr(5, 11);
    }
    const auto summary = Utf8ToWide("동국 연동 낙찰공고 " + std::to_string(g_table.size()) + "건 · 최신 " + latest);
    Gdiplus::RectF tag(248, static_cast<Gdiplus::REAL>(tb.update.y + (tb.update.h - 24) / 2), 280, 24);
    Gdiplus::GraphicsPath tagPath;
    RoundRectPath(tagPath, tag, 12);
    Gdiplus::SolidBrush tagFill(Gdiplus::Color(255, 255, 244, 229));
    g.FillPath(&tagFill, &tagPath);
    g.DrawPath(&copperPen, &tagPath);
    g.DrawString(summary.c_str(), -1, &eyebrow, Gdiplus::PointF(258, tag.Y + 4), &copper);

    const int table_top = nara::chart_theme::MainTableGridY(kTableCardY);
    const int table_bottom = h - 52;
    g_tableRect = {28, table_top, w - 28, table_bottom};
    const int cols[] = {28, 168, 328, 720, 920, 1000, w - 36};
    const wchar_t* headers[] = {L"개찰일", L"공고번호", L"공고명", L"발주처", L"지역", L"동국순위/낙찰"};
    g.FillRectangle(&headerBg, 28, table_top, w - 64, kHeaderH);
    for (int c = 0; c < 6; ++c) {
        g.DrawString(headers[c], -1, &headerFont, Gdiplus::PointF(static_cast<Gdiplus::REAL>(cols[c] + 8), static_cast<Gdiplus::REAL>(table_top + 8)), &muted);
    }
    g.SetClip(Gdiplus::Rect(28, table_top + kHeaderH, w - 64, table_bottom - table_top - kHeaderH));
    const int visible = (std::max)(1, (table_bottom - table_top - kHeaderH) / kRowH);
    const int max_scroll = (std::max)(0, static_cast<int>(g_table.size()) - visible);
    g_scroll = std::clamp(g_scroll, 0, max_scroll);
    for (int i = 0; i < visible; ++i) {
        const int idx = g_scroll + i;
        if (idx >= static_cast<int>(g_table.size())) {
            break;
        }
        const int y = table_top + kHeaderH + i * kRowH;
        if (idx == g_selected) {
            g.FillRectangle(&select, 28, y, w - 64, kRowH);
        } else if (i % 2 == 1) {
            g.FillRectangle(&stripe, 28, y, w - 64, kRowH);
        }
        const auto& row = g_table[idx];
        const std::wstring cells[] = {
            Utf8ToWide(row.open_date),
            Utf8ToWide(row.notice_number),
            Utf8ToWide(row.notice_name),
            Utf8ToWide(row.ordering_agency),
            Utf8ToWide(row.region),
            Utf8ToWide(row.rank_label),
        };
        for (int c = 0; c < 6; ++c) {
            Gdiplus::RectF clip(static_cast<Gdiplus::REAL>(cols[c] + 6), static_cast<Gdiplus::REAL>(y + 8),
                                static_cast<Gdiplus::REAL>(cols[c + 1] - cols[c] - 12), 20);
            g.SetClip(clip);
            g.DrawString(cells[c].c_str(), -1, &body, Gdiplus::PointF(clip.X, clip.Y), &ink);
            g.SetClip(Gdiplus::Rect(28, table_top + kHeaderH, w - 64, table_bottom - table_top - kHeaderH));
        }
    }
    g.ResetClip();
    const std::string period_label = g_lookback_months >= 12 ? "최근 1년" : "최근 " + std::to_string(g_lookback_months) + "개월";
    const auto footer = Utf8ToWide(period_label + " · " + g_start + "~" + g_end + " · " + g_region_token
                                   + " · 사정률 99.00000~101.00000"
                                   + (g_status.empty() ? "" : " · " + g_status));
    g.DrawString(footer.c_str(), -1, &eyebrow, Gdiplus::PointF(28, static_cast<Gdiplus::REAL>(h - 36)), &muted);

    BitBlt(hdc, 0, 0, w, h, mem, 0, 0, SRCCOPY);
    SelectObject(mem, old);
    DeleteObject(bmp);
    DeleteDC(mem);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_CREATE: {
        INITCOMMONCONTROLSEX icc{sizeof(icc), ICC_STANDARD_CLASSES};
        InitCommonControlsEx(&icc);
        g_mainHwnd = hwnd;
        nara::SetChartRegionHandler(&OnChartRegion);
        g_btnUpdate = CreateWindowW(L"BUTTON", L"업데이트", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 0, 0, 96, 36, hwnd,
                                    reinterpret_cast<HMENU>(static_cast<INT_PTR>(kIdUpdate)), nullptr, nullptr);
        g_btnStop = CreateWindowW(L"BUTTON", L"중지", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 0, 0, 72, 36, hwnd,
                                  reinterpret_cast<HMENU>(static_cast<INT_PTR>(kIdStop)), nullptr, nullptr);
        g_btnChart = CreateWindowW(L"BUTTON", L"차트 화면", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 0, 0, 96, 36, hwnd,
                                   reinterpret_cast<HMENU>(static_cast<INT_PTR>(kIdChart)), nullptr, nullptr);
        LayoutMain(hwnd);
        nara::EnsurePortableDatabase();
        nara::MergeJsonlIntoStore(nara::DefaultAwardJsonlPath(), nara::DefaultDashboardDbPath());
        ReloadTable(hwnd);
        return 0;
    }
    case WM_SIZE:
        LayoutMain(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT: {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc{};
        GetClientRect(hwnd, &rc);
        PaintMain(hdc, rc);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_MOUSEWHEEL:
        if (GET_WHEEL_DELTA_WPARAM(wParam) > 0) {
            g_scroll = (std::max)(0, g_scroll - 3);
        } else {
            g_scroll += 3;
        }
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_LBUTTONDOWN: {
        const int mx = GET_X_LPARAM(lParam);
        const int y = GET_Y_LPARAM(lParam);
        RECT client{};
        GetClientRect(hwnd, &client);
        const auto tb = nara::chart_theme::LayoutMainToolbar(client.right, kTableCardY);
        const auto seg = nara::chart_theme::SplitRegionSegment(tb.region_combo);
        const int region_hit = nara::chart_theme::HitRegionSegment(seg, mx, y);
        if (region_hit == 0) {
            ApplyRegion(hwnd, "전국");
            return 0;
        }
        if (region_hit == 1) {
            ApplyRegion(hwnd, "경북");
            return 0;
        }
        if (y >= g_tableRect.top + kHeaderH && y < g_tableRect.bottom) {
            const int row = g_scroll + (y - g_tableRect.top - kHeaderH) / kRowH;
            if (row >= 0 && row < static_cast<int>(g_table.size())) {
                g_selected = row;
                InvalidateRect(hwnd, nullptr, FALSE);
            }
        }
        return 0;
    }
    case WM_DRAWITEM: {
        const auto* item = reinterpret_cast<DRAWITEMSTRUCT*>(lParam);
        auto kind = nara::chart_theme::MainButtonKind::Secondary;
        if (item->CtlID == kIdUpdate) {
            kind = nara::chart_theme::MainButtonKind::Primary;
        } else if (item->CtlID == kIdStop) {
            kind = nara::chart_theme::MainStopKind(false);
        }
        const auto look = nara::chart_theme::MainButtonLook(kind);
        Gdiplus::Graphics g(item->hDC);
        g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);
        Gdiplus::RectF r(0, 0, static_cast<Gdiplus::REAL>(item->rcItem.right - item->rcItem.left),
                         static_cast<Gdiplus::REAL>(item->rcItem.bottom - item->rcItem.top));
        Gdiplus::SolidBrush card(Gdiplus::Color(255, 255, 255, 255));
        g.FillRectangle(&card, r);
        Gdiplus::RectF inner(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.0f, r.Height - 1.0f);
        Gdiplus::GraphicsPath path;
        RoundRectPath(path, inner, static_cast<float>(nara::chart_theme::kMainBtnRadius));
        if (!look.transparent) {
            if (look.gradient) {
                Gdiplus::LinearGradientBrush fill(
                    Gdiplus::PointF(inner.X, inner.Y),
                    Gdiplus::PointF(inner.X + inner.Width, inner.Y + inner.Height),
                    Gdiplus::Color(look.fill0), Gdiplus::Color(look.fill2));
                g.FillPath(&fill, &path);
            } else {
                Gdiplus::SolidBrush fill(Gdiplus::Color(look.fill0));
                g.FillPath(&fill, &path);
            }
            if ((look.border & 0xFF000000u) != 0) {
                Gdiplus::Pen border(Gdiplus::Color(look.border), 1.0f);
                g.DrawPath(&border, &path);
            }
        }
        Gdiplus::FontFamily family(nara::chart_theme::kFontFamily);
        Gdiplus::Font font(&family, nara::chart_theme::kFontBody, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
        Gdiplus::SolidBrush text(Gdiplus::Color(look.text));
        wchar_t caption[32] = {};
        GetWindowTextW(item->hwndItem, caption, 32);
        Gdiplus::StringFormat fmt;
        fmt.SetAlignment(Gdiplus::StringAlignmentCenter);
        fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
        g.DrawString(caption, -1, &font, r, &fmt, &text);
        return TRUE;
    }
    case WM_COMMAND:
        if (LOWORD(wParam) == kIdChart) {
            nara::OpenChartWindow(GetModuleHandleW(nullptr), hwnd, g_snapshot, g_start, g_end, g_region_token);
        } else if (LOWORD(wParam) == kIdUpdate) {
            OnUpdate(hwnd);
        }
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show) {
    Gdiplus::GdiplusStartupInput gdiplus_input;
    ULONG_PTR gdiplus_token = 0;
    Gdiplus::GdiplusStartup(&gdiplus_token, &gdiplus_input, nullptr);
    WNDCLASSW wc{};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(RGB(243, 245, 248));
    wc.lpszClassName = L"NaraDesktopMain";
    RegisterClassW(&wc);
    HWND hwnd = CreateWindowExW(0, wc.lpszClassName, L"동국문화유산연구원 낙찰분석프로그램",
                                WS_OVERLAPPEDWINDOW | WS_VISIBLE, CW_USEDEFAULT, CW_USEDEFAULT, 1600, 960, nullptr,
                                nullptr, instance, nullptr);
    ShowWindow(hwnd, SW_SHOWMAXIMIZED);
    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    Gdiplus::GdiplusShutdown(gdiplus_token);
    return static_cast<int>(msg.wParam);
}
