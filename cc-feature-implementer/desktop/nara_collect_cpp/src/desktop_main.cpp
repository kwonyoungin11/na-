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
#include "nara/dwrite_text.h"
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
const int kTableCardY = nara::chart_theme::kTableCardY;

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
bool g_updating = false;
RECT g_tableRect{};
float g_dpi_scale = 1.0f;

unsigned WindowDpi(HWND hwnd) {
    using Fn = UINT(WINAPI*)(HWND);
    static Fn fn =
        reinterpret_cast<Fn>(GetProcAddress(GetModuleHandleW(L"user32.dll"), "GetDpiForWindow"));
    UINT dpi = 96;
    if (fn != nullptr && hwnd != nullptr) {
        dpi = fn(hwnd);
    }
    if (dpi < 96) {
        dpi = 96;
    }
    if (dpi > 384) {
        dpi = 384;
    }
    return dpi;
}

void SyncDpi(HWND hwnd) {
    g_dpi_scale = nara::chart_theme::DpiScaleFromDpi(WindowDpi(hwnd));
}

int ToPx(int dip) { return nara::chart_theme::DipToPx(dip, g_dpi_scale); }
int ToDip(int px) { return nara::chart_theme::PxToDip(px, g_dpi_scale); }

void ApplyDpiTransform(Gdiplus::Graphics& g) { g.ScaleTransform(g_dpi_scale, g_dpi_scale); }

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
    const float rad = (std::min)(radius, (std::min)(r.Width, r.Height) / 2.0f);
    if (rad < 0.5f) {
        path.AddRectangle(r);
        return;
    }
    const float d = rad * 2.0f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
    path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
    path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
    path.CloseFigure();
}

void FillRoundRect(Gdiplus::Graphics& g, const Gdiplus::RectF& r, float radius, uint32_t fill) {
    Gdiplus::GraphicsPath path;
    RoundRectPath(path, r, radius);
    Gdiplus::SolidBrush fill_brush(static_cast<Gdiplus::ARGB>(fill));
    g.FillPath(&fill_brush, &path);
}

void DrawBlurShadow(Gdiplus::Graphics& g, const Gdiplus::RectF& r, float radius, int depth, int blur, uint32_t argb) {
    const int layers = nara::chart_theme::BlurShadowLayerCount(blur);
    for (int i = layers - 1; i >= 0; --i) {
        const float inf = nara::chart_theme::BlurShadowInflate(blur, i, layers);
        FillRoundRect(g,
                      Gdiplus::RectF(r.X - inf + 1.0f, r.Y - inf + static_cast<Gdiplus::REAL>(depth),
                                     r.Width + inf * 2.0f, r.Height + inf * 2.0f),
                      radius + inf, nara::chart_theme::BlurShadowArgb(argb, i, layers));
    }
}

nara::dwrite_text::Align DwAlign(Gdiplus::StringAlignment a) {
    if (a == Gdiplus::StringAlignmentCenter) {
        return nara::dwrite_text::Align::Center;
    }
    if (a == Gdiplus::StringAlignmentFar) {
        return nara::dwrite_text::Align::Far;
    }
    return nara::dwrite_text::Align::Near;
}

void FillGlyphs(Gdiplus::Graphics&, const wchar_t* text, const Gdiplus::FontFamily&, int style, float size,
                const Gdiplus::RectF& box, Gdiplus::StringAlignment h, Gdiplus::StringAlignment v, uint32_t argb) {
    nara::dwrite_text::QueueBox(box.X, box.Y, box.Width, box.Height, text, size, argb, DwAlign(h), DwAlign(v),
                                style == Gdiplus::FontStyleBold);
}

void QueuePoint(const wchar_t* text, float x, float y, float w, float h, float size, uint32_t argb, bool bold) {
    nara::dwrite_text::QueueBox(x, y, w, h, text, size, argb, nara::dwrite_text::Align::Near,
                                nara::dwrite_text::Align::Near, bold);
}

void ApplyCrispGraphics(Gdiplus::Graphics& g) {
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.SetPixelOffsetMode(Gdiplus::PixelOffsetModeNone);
    g.SetCompositingQuality(Gdiplus::CompositingQualityAssumeLinear);
    g.SetInterpolationMode(Gdiplus::InterpolationModeNearestNeighbor);
    g.SetTextRenderingHint(Gdiplus::TextRenderingHintClearTypeGridFit);
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

void PumpUi(HWND hwnd) {
    InvalidateRect(hwnd, nullptr, FALSE);
    UpdateWindow(hwnd);
    MSG msg{};
    while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
}

void SetCollectStatus(HWND hwnd, const std::string& text) {
    g_status = text;
    PumpUi(hwnd);
}

void SetUpdatingUi(HWND hwnd, bool busy) {
    g_updating = busy;
    if (g_btnUpdate) {
        EnableWindow(g_btnUpdate, busy ? FALSE : TRUE);
        SetWindowTextW(g_btnUpdate, busy ? L"수집 중" : L"업데이트");
        InvalidateRect(g_btnUpdate, nullptr, FALSE);
    }
    if (g_btnStop) {
        InvalidateRect(g_btnStop, nullptr, FALSE);
    }
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
    PumpUi(hwnd);
}

bool RunCollectCommand(HWND hwnd, const std::string& cmd_utf8, const std::wstring& dir, const std::string& label,
                       const std::string& jsonl, const std::string& db) {
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
        SetCollectStatus(hwnd, "수집 프로세스 실행 실패 " + label);
        return false;
    }
    while (WaitForSingleObject(pi.hProcess, 200) == WAIT_TIMEOUT) {
        PumpUi(hwnd);
    }
    DWORD code = 1;
    GetExitCodeProcess(pi.hProcess, &code);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    if (code != 0) {
        SetCollectStatus(hwnd, "수집 실패 " + label + " code=" + std::to_string(static_cast<int>(code)));
        ApplyMergedSnapshot(hwnd, nara::MergeJsonlIntoStore(jsonl, db));
        return false;
    }
    SetCollectStatus(hwnd, "수집 반영 " + label);
    ApplyMergedSnapshot(hwnd, nara::MergeJsonlIntoStore(jsonl, db));
    return true;
}

void OnUpdate(HWND hwnd) {
    if (g_updating) {
        return;
    }
    SetUpdatingUi(hwnd, true);
    nara::EnsurePortableDatabase();
    const auto db = nara::DefaultDashboardDbPath();
    const auto jsonl = nara::DefaultAwardJsonlPath();
    const auto stored = nara::MaxParticipantOpenDay(db);
    SetCollectStatus(hwnd, stored.empty() ? "업데이트 시작 · 저장된 공고 없음"
                                          : "업데이트 시작 · 저장 최신 개찰 " + stored);

    const auto app_dir = nara::DefaultAppDir();
    const auto exe = std::filesystem::path(app_dir) / "nara_award_collect.exe";
    if (GetFileAttributesW(Utf8ToWide(exe.string()).c_str()) == INVALID_FILE_ATTRIBUTES) {
        SetCollectStatus(hwnd, "nara_award_collect.exe 없음");
        SetUpdatingUi(hwnd, false);
        return;
    }
    const auto env = nara::DefaultServiceKeyEnvPath();
    if (GetFileAttributesW(Utf8ToWide(env).c_str()) == INVALID_FILE_ATTRIBUTES) {
        SetCollectStatus(hwnd, "SERVICE_KEY 없음: 앱 폴더에 .env를 두세요");
        SetUpdatingUi(hwnd, false);
        return;
    }

    const auto plan = nara::PlanMissingCollect(stored, Today(), g_lookback_months);
    const auto dir = Utf8ToWide(app_dir);
    if (plan.need == nara::CollectNeed::AlreadyCurrent) {
        SetCollectStatus(hwnd, "이미 최신 · 개찰 " + stored + " · 나라장터를 다시 받지 않음");
        SetUpdatingUi(hwnd, false);
        return;
    }
    if (plan.need == nara::CollectNeed::GapFromStore) {
        SetCollectStatus(hwnd, "빠진 구간만 수집 " + plan.from_date + "~" + plan.to_date + " · 나라장터 조회 중");
        const auto cmd = nara::AwardCollectGapCommandLine(exe.string(), plan.from_date, plan.to_date, jsonl, env);
        if (!RunCollectCommand(hwnd, cmd, dir, plan.from_date + "~" + plan.to_date, jsonl, db)) {
            SetUpdatingUi(hwnd, false);
            return;
        }
        SetCollectStatus(hwnd, "수집 완료 · 빠진 구간 " + plan.from_date + "~" + plan.to_date);
        SetUpdatingUi(hwnd, false);
        return;
    }

    const auto start_month = plan.from_date.substr(0, 7);
    const auto end_month = plan.to_date.substr(0, 7);
    const auto windows = nara::CollectThreeMonthWindows(start_month, end_month);
    for (size_t i = 0; i < windows.size(); ++i) {
        const auto& window = windows[i];
        SetCollectStatus(hwnd, "나라장터 월단위 수집 " + window.start_month + "~" + window.end_month + " ("
                                   + std::to_string(i + 1) + "/" + std::to_string(windows.size()) + ")");
        const auto cmd = nara::AwardCollectCommandLine(exe.string(), window.start_month, window.end_month, jsonl, env);
        if (!RunCollectCommand(hwnd, cmd, dir, window.start_month + "~" + window.end_month, jsonl, db)) {
            SetUpdatingUi(hwnd, false);
            return;
        }
    }
    SetCollectStatus(hwnd, "수집 완료 " + start_month + "~" + end_month);
    SetUpdatingUi(hwnd, false);
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
        nara::dwrite_text::QueueBox(r.X, r.Y, r.Width, r.Height, nara::chart_theme::RegionLabelAt(index),
                                    nara::chart_theme::kFontCaption,
                                    index == on ? nara::chart_theme::kAccentStrongArgb
                                                : nara::chart_theme::kSubtleTextArgb,
                                    nara::dwrite_text::Align::Center, nara::dwrite_text::Align::Center, true);
    };
    label(seg.nationwide, 0);
    label(seg.gyeongbuk, 1);
}

void LayoutMain(HWND hwnd) {
    SyncDpi(hwnd);
    RECT rc{};
    GetClientRect(hwnd, &rc);
    const auto tb = nara::chart_theme::LayoutMainToolbar(ToDip(rc.right), kTableCardY);
    MoveWindow(g_btnChart, ToPx(tb.chart.x), ToPx(tb.chart.y), ToPx(tb.chart.w), ToPx(tb.chart.h), TRUE);
    MoveWindow(g_btnStop, ToPx(tb.stop.x), ToPx(tb.stop.y), ToPx(tb.stop.w), ToPx(tb.stop.h), TRUE);
    MoveWindow(g_btnUpdate, ToPx(tb.update.x), ToPx(tb.update.y), ToPx(tb.update.w), ToPx(tb.update.h), TRUE);
}

void PaintMain(HWND hwnd, HDC hdc) {
    SyncDpi(hwnd);
    RECT rc{};
    GetClientRect(hwnd, &rc);
    const int pixel_w = (std::max)(1, static_cast<int>(rc.right));
    const int pixel_h = (std::max)(1, static_cast<int>(rc.bottom));
    const int w = ToDip(pixel_w);
    const int h = ToDip(pixel_h);
    HDC mem = CreateCompatibleDC(hdc);
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = pixel_w;
    bmi.bmiHeader.biHeight = -pixel_h;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    HBITMAP bmp = CreateDIBSection(hdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!bmp) {
        bmp = CreateCompatibleBitmap(hdc, pixel_w, pixel_h);
    }
    HGDIOBJ old = SelectObject(mem, bmp);
    nara::dwrite_text::ResetQueue();
    Gdiplus::Graphics g(mem);
    ApplyCrispGraphics(g);
    ApplyDpiTransform(g);
    Gdiplus::SolidBrush page{Gdiplus::Color(nara::chart_theme::kWindowBgArgb)};
    g.FillRectangle(&page, 0, 0, w, h);
    Gdiplus::SolidBrush accent(Gdiplus::Color(255, 217, 119, 6));
    g.FillRectangle(&accent, 0, 0, w, 3);

    Gdiplus::FontFamily family(L"Malgun Gothic");
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
    Gdiplus::Pen copperPen(Gdiplus::Color(255, 231, 196, 140), 1.5f);

    nara::dwrite_text::QueueBox(36, 18, 280, 16, L"낙찰분석프로그램", 12.0f, nara::chart_theme::kAccentStrongArgb,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, true);
    nara::dwrite_text::QueueBox(36, 34, 520, 28, L"동국문화유산연구원", 22.0f, nara::chart_theme::kTextArgb,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, true);

    const auto kpis = nara::chart_theme::LayoutMainKpis(w);
    std::string latest_kpi = g_table.empty() ? "-" : g_table.front().open_date;
    if (latest_kpi.size() >= 16) {
        latest_kpi = latest_kpi.substr(0, 16);
    }
    const std::string period_kpi = g_lookback_months >= 12 ? "최근 1년" : "최근 " + std::to_string(g_lookback_months) + "개월";
    wchar_t notice_n[32];
    swprintf_s(notice_n, L"%d건", g_notice_count > 0 ? g_notice_count : static_cast<int>(g_table.size()));
    auto paint_kpi = [&](const nara::chart_theme::RectI& box, const wchar_t* caption, const wchar_t* value) {
        Gdiplus::RectF r(static_cast<Gdiplus::REAL>(box.x), static_cast<Gdiplus::REAL>(box.y),
                         static_cast<Gdiplus::REAL>(box.w), static_cast<Gdiplus::REAL>(box.h));
        Gdiplus::GraphicsPath path;
        RoundRectPath(path, r, 12);
        g.FillPath(&white, &path);
        Gdiplus::Pen edge(Gdiplus::Color(nara::chart_theme::kComboStrokeArgb), 1.0f);
        g.DrawPath(&edge, &path);
        nara::dwrite_text::QueueBox(r.X + 14, r.Y + 8, r.Width - 24, 16, caption, 11.0f,
                                    nara::chart_theme::kSubtleTextArgb, nara::dwrite_text::Align::Near,
                                    nara::dwrite_text::Align::Near, false);
        nara::dwrite_text::QueueBox(r.X + 14, r.Y + 26, r.Width - 24, 24, value, 18.0f, nara::chart_theme::kTextArgb,
                                    nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, true);
    };
    paint_kpi(kpis.notices, L"낙찰공고", notice_n);
    paint_kpi(kpis.latest, L"최신 개찰", Utf8ToWide(latest_kpi).c_str());
    paint_kpi(kpis.period, L"기간", Utf8ToWide(period_kpi).c_str());

    Gdiplus::RectF table_card(20, static_cast<Gdiplus::REAL>(kTableCardY), static_cast<Gdiplus::REAL>(w - 40),
                              static_cast<Gdiplus::REAL>((std::max)(200, h - kTableCardY - 40)));
    Gdiplus::GraphicsPath tablePath;
    RoundRectPath(tablePath, table_card, 18);
    g.FillPath(&white, &tablePath);
    g.DrawPath(&copperPen, &tablePath);
    Gdiplus::Font section(&family, 18.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    const auto tb = nara::chart_theme::LayoutMainToolbar(w, kTableCardY);
    const float title_y = static_cast<float>(tb.update.y + (tb.update.h - 18) / 2);
    nara::dwrite_text::QueueBox(static_cast<float>(nara::chart_theme::kMainCardInset + nara::chart_theme::kMainTablePadX),
                                title_y, 280, 24, L"동국 참여 낙찰공고", 18.0f, 0xFF111111u,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, true);
    Gdiplus::StringFormat label_fmt;
    label_fmt.SetAlignment(Gdiplus::StringAlignmentFar);
    label_fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    nara::dwrite_text::QueueBox(static_cast<float>(tb.region_label.x), static_cast<float>(tb.region_label.y),
                                static_cast<float>(tb.region_label.w), static_cast<float>(tb.region_label.h), L"지역",
                                12.0f, 0xFF6B7280u, nara::dwrite_text::Align::Far, nara::dwrite_text::Align::Center,
                                false);
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
    nara::dwrite_text::QueueBox(258, tag.Y + 4, 260, 18, summary.c_str(), 12.0f, 0xFFB45309u,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, false);

    const auto status_box = nara::chart_theme::LayoutMainStatus(w, kTableCardY);
    Gdiplus::RectF status_r(static_cast<Gdiplus::REAL>(status_box.x), static_cast<Gdiplus::REAL>(status_box.y),
                            static_cast<Gdiplus::REAL>(status_box.w), static_cast<Gdiplus::REAL>(status_box.h));
    Gdiplus::GraphicsPath status_path;
    RoundRectPath(status_path, status_r, 10);
    const bool busy = g_updating;
    Gdiplus::SolidBrush status_fill(
        Gdiplus::Color(busy ? nara::chart_theme::kAccentArgb : nara::chart_theme::kHoverFillArgb));
    g.FillPath(&status_fill, &status_path);
    const auto status_text = Utf8ToWide(g_status.empty() ? "업데이트는 없는 개찰일만 조회합니다." : g_status);
    nara::dwrite_text::QueueBox(status_r.X + 12, status_r.Y, status_r.Width - 20, status_r.Height, status_text.c_str(),
                                13.0f, busy ? 0xFFFFFFFFu : nara::chart_theme::kSubtleTextArgb,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, true, true, true);

    const int table_top = nara::chart_theme::MainTableGridY(kTableCardY);
    const int table_bottom = h - 52;
    g_tableRect = {28, table_top, w - 28, table_bottom};
    const int cols[] = {28, 168, 328, 720, 920, 1000, w - 36};
    const wchar_t* headers[] = {L"개찰일", L"공고번호", L"공고명", L"발주처", L"지역", L"동국순위/낙찰"};
    g.FillRectangle(&headerBg, 28, table_top, w - 64, kHeaderH);
    for (int c = 0; c < 6; ++c) {
        nara::dwrite_text::QueueBox(static_cast<float>(cols[c] + 8), static_cast<float>(table_top + 8),
                                    static_cast<float>(cols[c + 1] - cols[c] - 12), 20, headers[c], 12.0f, 0xFF6B7280u,
                                    nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, true);
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
            nara::dwrite_text::QueueBox(clip.X, clip.Y, clip.Width, clip.Height, cells[c].c_str(), 13.0f, 0xFF111111u,
                                        nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, false, true,
                                        true);
            g.SetClip(Gdiplus::Rect(28, table_top + kHeaderH, w - 64, table_bottom - table_top - kHeaderH));
        }
    }
    g.ResetClip();
    const std::string period_label = g_lookback_months >= 12 ? "최근 1년" : "최근 " + std::to_string(g_lookback_months) + "개월";
    const auto footer = Utf8ToWide(period_label + " · " + g_start + "~" + g_end + " · " + g_region_token
                                   + " · 사정률 99.00000~101.00000"
                                   + (g_status.empty() ? "" : " · " + g_status));
    nara::dwrite_text::QueueBox(28, static_cast<float>(h - 36), static_cast<float>(w - 56), 18, footer.c_str(), 12.0f,
                                0xFF6B7280u, nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Near, false,
                                true, true);
    g.Flush();
    nara::dwrite_text::Flush(mem, pixel_w, pixel_h, g_dpi_scale);

    BitBlt(hdc, 0, 0, pixel_w, pixel_h, mem, 0, 0, SRCCOPY);
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
        const auto stored = nara::MaxParticipantOpenDay(nara::DefaultDashboardDbPath());
        g_status = stored.empty() ? "준비됨 · 저장된 공고 없음 · 업데이트하면 1년치를 받습니다"
                                  : "준비됨 · 저장 최신 개찰 " + stored + " · 업데이트는 그 다음 날부터만 조회";
        return 0;
    }
    case WM_SIZE:
        LayoutMain(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_DPICHANGED: {
        const auto* rec = reinterpret_cast<const RECT*>(lParam);
        SetWindowPos(hwnd, nullptr, rec->left, rec->top, rec->right - rec->left, rec->bottom - rec->top,
                     SWP_NOZORDER | SWP_NOACTIVATE);
        LayoutMain(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_GETMINMAXINFO: {
        SyncDpi(hwnd);
        auto* info = reinterpret_cast<MINMAXINFO*>(lParam);
        info->ptMinTrackSize.x = ToPx(nara::chart_theme::kWindowMinW);
        info->ptMinTrackSize.y = ToPx(nara::chart_theme::kWindowMinH);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT: {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc{};
        GetClientRect(hwnd, &rc);
        PaintMain(hwnd, hdc);
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
        const int mx = ToDip(GET_X_LPARAM(lParam));
        const int y = ToDip(GET_Y_LPARAM(lParam));
        RECT client{};
        GetClientRect(hwnd, &client);
        const auto tb = nara::chart_theme::LayoutMainToolbar(ToDip(client.right), kTableCardY);
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
                if (nara::ChartWindowIsOpen()) {
                    nara::SetChartFocusNotice(g_table[static_cast<size_t>(row)].notice_number);
                }
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
            kind = nara::chart_theme::MainStopKind(g_updating);
        }
        const auto look = nara::chart_theme::MainButtonLook(kind);
        const int pw = item->rcItem.right - item->rcItem.left;
        const int ph = item->rcItem.bottom - item->rcItem.top;
        Gdiplus::RectF r(0, 0, static_cast<Gdiplus::REAL>(ToDip(pw)), static_cast<Gdiplus::REAL>(ToDip(ph)));
        {
            Gdiplus::Graphics g(item->hDC);
            ApplyCrispGraphics(g);
            ApplyDpiTransform(g);
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
        }
        wchar_t caption[32] = {};
        GetWindowTextW(item->hwndItem, caption, 32);
        nara::dwrite_text::ResetQueue();
        nara::dwrite_text::QueueBox(r.X, r.Y, r.Width, r.Height, caption, nara::chart_theme::kFontBody, look.text,
                                    nara::dwrite_text::Align::Center, nara::dwrite_text::Align::Center, true);
        nara::dwrite_text::Flush(item->hDC, pw, ph, g_dpi_scale);
        return TRUE;
    }
    case WM_COMMAND:
        if (LOWORD(wParam) == kIdChart) {
            nara::OpenChartWindow(GetModuleHandleW(nullptr), hwnd, g_snapshot, g_start, g_end, g_region_token);
            if (!g_table.empty() && g_selected >= 0 && g_selected < static_cast<int>(g_table.size())) {
                nara::SetChartFocusNotice(g_table[static_cast<size_t>(g_selected)].notice_number);
            }
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
    using SetDpiCtxFn = BOOL(WINAPI*)(DPI_AWARENESS_CONTEXT);
    if (const auto set_dpi = reinterpret_cast<SetDpiCtxFn>(
            GetProcAddress(GetModuleHandleW(L"user32.dll"), "SetProcessDpiAwarenessContext"))) {
        set_dpi(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }
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
