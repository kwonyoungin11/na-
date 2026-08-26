#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "nara/chart_popup.h"

#include "nara/award_table.h"
#include "nara/chart_theme.h"
#include "nara/chart_view.h"
#include "nara/scatter_cache.h"
#include "nara/igunsul_convert.h"
#include "nara/scatter_chart.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <map>
#include <objidl.h>
#include <set>
#include <string>
#include <vector>
#include <windowsx.h>
#include <gdiplus.h>
#include <uxtheme.h>

#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "uxtheme.lib")

namespace nara {
namespace {

using chart_theme::kAxisArgb;
using chart_theme::kAxisNotice;
using chart_theme::kAxisRate;
using chart_theme::kAxisWidth;
using chart_theme::kBgArgb;
using chart_theme::kAccentArgb;
using chart_theme::kAccentSubtleArgb;
using chart_theme::kCalloutPenArgb;
using chart_theme::kCaptionH;
using chart_theme::kCardFillArgb;
using chart_theme::kCardRadius;
using chart_theme::kCardShadowArgb;
using chart_theme::kCardShadowDepth;
using chart_theme::kCardStrokeArgb;
using chart_theme::kCardStrokeW;
using chart_theme::kCheckFillArgb;
using chart_theme::kCheckGap;
using chart_theme::kCheckMarkArgb;
using chart_theme::kCheckRadius;
using chart_theme::kCheckSize;
using chart_theme::kChipBorderWidth;
using chart_theme::ChipOutlineArgb;
using chart_theme::ChipRadiusPx;
using chart_theme::kCompanyAllH;
using chart_theme::kCompanyAllLabel;
using chart_theme::kCompanyAllW;
using chart_theme::kCompanyLabel;
using chart_theme::kCompanyPalette;
using chart_theme::kCompanyPanelArgb;
using chart_theme::kCompanyPanelRadius;
using chart_theme::kCompanyWrapRows;
using chart_theme::kComboH;
using chart_theme::kComboRadius;
using chart_theme::kDockFillArgb;
using chart_theme::kDockMinH;
using chart_theme::kDockRadius;
using chart_theme::kHoverFillArgb;
using chart_theme::kDonggukArgb;
using chart_theme::HitRegionSegment;
using chart_theme::RegionIndexFromToken;
using chart_theme::RegionLabelAt;
using chart_theme::SplitRegionSegment;
using chart_theme::kEmptyMessage;
using chart_theme::kFontAxis;
using chart_theme::kFontCaption;
using chart_theme::kFontCompany;
using chart_theme::kFontCompanyAll;
using chart_theme::kFontEmpty;
using chart_theme::kFontBody;
using chart_theme::kFontEyebrow;
using chart_theme::kFontFamily;
using chart_theme::kFontHover;
using chart_theme::kFontReset;
using chart_theme::kGlassFillArgb;
using chart_theme::kGoldWidth;
using chart_theme::kGridArgb;
using chart_theme::kGridWidth;
using chart_theme::kHoverRadius;
using chart_theme::kLeftPanelW;
using chart_theme::kMajorGridArgb;
using chart_theme::kMaxLabel;
using chart_theme::kMinLabel;
using chart_theme::kMutedCaptionArgb;
using chart_theme::kPeriodLabel;
using chart_theme::kPlotBgArgb;
using chart_theme::kPlotBottom;
using chart_theme::kPlotLeft;
using chart_theme::kPlotRight;
using chart_theme::kPlotTop;
using chart_theme::kPointerArgb;
using chart_theme::kPointerLabelBgArgb;
using chart_theme::kPointerLabelTextArgb;
using chart_theme::kPointerWidth;
using chart_theme::kPrecisionLabel;
using chart_theme::kRangeLabel;
using chart_theme::kRankBadgeArgb;
using chart_theme::kRankBadgeRadius;
using chart_theme::kRankGoldArgb;
using chart_theme::kRegionLabel;
using chart_theme::kResetBtnRadius;
using chart_theme::kResetH;
using chart_theme::kResetLabel;
using chart_theme::kResetMarginRight;
using chart_theme::kResetMarginTop;
using chart_theme::kResetMinW;
using chart_theme::kResetShadowArgb;
using chart_theme::kResetShadowDepth;
using chart_theme::kToolbarPad;
using chart_theme::kToolbarRadius;
using chart_theme::kToolbarShadowDepth;
using chart_theme::kWindowBgArgb;
using chart_theme::kShadowArgb;
using chart_theme::kStrokeStrongArgb;
using chart_theme::kStrokeSubtleArgb;
using chart_theme::kSubtleTextArgb;
using chart_theme::kTextArgb;
using chart_theme::kWindowMinH;
using chart_theme::kWindowMinW;
using chart_theme::kWindowTitle;
using chart_theme::ChartChrome;
using chart_theme::ChipLabelFontPx;
using chart_theme::ChipLabelScreenPx;
using chart_theme::ApplyCompanyClick;
using chart_theme::ChipPaintBefore;
using chart_theme::CompanyCheckBoxRect;
using chart_theme::CompanyCheckCellRect;
using chart_theme::CompanyChipVisible;
using chart_theme::CompanyHeaderLabelY;
using chart_theme::MainButtonKind;
using chart_theme::MainButtonLook;
using chart_theme::FormatNoticeYLabel;
using chart_theme::LayoutChartChrome;
using chart_theme::RectI;
using chart_theme::ScatterColumnCount;
using chart_theme::UniformGridCell;
using scatter_cache::ChromeHot;
using scatter_cache::IsOverlayOnlyFrame;
using scatter_cache::ScatterViewCacheKey;
using scatter_cache::BounceOverlaySkipsLabels;
using scatter_cache::CanCacheStaticLayer;
using scatter_cache::DonggukBounceLiftPx;
using scatter_cache::DonggukBounceScale;
using scatter_cache::SkipChipLabelsDuringPan;

constexpr double kMinRate = 99.0;
constexpr double kMaxRate = 101.0;
constexpr int kIdPeriod = 2102;
constexpr int kIdPrecision = 2103;
constexpr int kIdRegion = 2104;
constexpr int kIdMin = 2105;
constexpr int kIdMax = 2106;

struct ChartState {
    std::vector<ParticipantRow> full_rows;
    std::vector<ParticipantRow> rows;
    std::vector<ScatterNotice> notices;
    std::vector<std::string> open_dates;
    double min_rate = kMinRate;
    double max_rate = kMaxRate;
    double step = 0.01;
    std::string start_date;
    std::string end_date;
    ChartView view;
    bool panning = false;
    bool drag_armed = false;
    POINT last_pt{};
    bool hover_valid = false;
    int hover_x = 0;
    int hover_y = 0;
    double hover_rate = 0;
};

struct CompanyItem {
    std::string key;
    std::wstring display;
    bool checked = true;
};

ChartState g_chart;
ChartChrome g_chrome;
std::vector<CompanyItem> g_companyItems;
HWND g_chartHwnd = nullptr;
HWND g_period = nullptr;
HWND g_precision = nullptr;
HWND g_min = nullptr;
HWND g_max = nullptr;
std::string g_region_token = "전국";
ChartRegionHandler g_region_handler = nullptr;
HFONT g_uiFont = nullptr;
int g_hotCompany = -1;
bool g_hotReset = false;
bool g_hotAll = false;
int g_prepareVersion = 0;
HDC g_layerDc = nullptr;
HBITMAP g_layerBmp = nullptr;
HGDIOBJ g_layerOld = nullptr;
int g_layerW = 0;
int g_layerH = 0;
HDC g_frameDc = nullptr;
HBITMAP g_frameBmp = nullptr;
HGDIOBJ g_frameOld = nullptr;
int g_frameW = 0;
int g_frameH = 0;
bool g_hasStatic = false;
ScatterViewCacheKey g_staticKey{};
DWORD g_bounceOrigin = 0;
constexpr UINT_PTR kBounceTimer = 1;
void UpdateScroll(HWND hwnd);
void ReleaseLayer();
void PaintDonggukChips(Gdiplus::Graphics& g);
void PaintHoverOverlay(Gdiplus::Graphics& g);

Gdiplus::Color Argb(uint32_t argb) {
    return Gdiplus::Color(argb);
}

void RoundRectPath(Gdiplus::GraphicsPath& path, Gdiplus::RectF r, float radius) {
    const float rad = (std::min)(radius, (std::min)(r.Width, r.Height) / 2.0f);
    const float d = rad * 2.0f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
    path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
    path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
    path.CloseFigure();
}

void FillRoundRect(Gdiplus::Graphics& g, const Gdiplus::RectF& r, float radius, uint32_t fill,
                   uint32_t stroke, float stroke_width) {
    Gdiplus::GraphicsPath path;
    RoundRectPath(path, r, radius);
    Gdiplus::SolidBrush brush(Argb(fill));
    g.FillPath(&brush, &path);
    if (stroke_width > 0.0f) {
        Gdiplus::Pen pen(Argb(stroke), stroke_width);
        g.DrawPath(&pen, &path);
    }
}

void PaintChipBody(Gdiplus::Graphics& g, const Gdiplus::RectF& box, int cell, uint32_t fill, bool rank1) {
    const float radius = ChipRadiusPx(cell);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    FillRoundRect(g, box, radius, fill, ChipOutlineArgb(), kChipBorderWidth);
    if (rank1) {
        Gdiplus::GraphicsPath gold_path;
        RoundRectPath(gold_path, box, radius);
        Gdiplus::Pen gold(Argb(kRankGoldArgb), kGoldWidth);
        g.DrawPath(&gold, &gold_path);
    }
}

Gdiplus::RectF ToRectF(const RectI& r) {
    return {static_cast<Gdiplus::REAL>(r.x), static_cast<Gdiplus::REAL>(r.y), static_cast<Gdiplus::REAL>(r.w),
            static_cast<Gdiplus::REAL>(r.h)};
}

void PaintRegionSegment(Gdiplus::Graphics& g, const RectI& slot, const std::string& selected) {
    const auto seg = SplitRegionSegment(slot);
    const int on = RegionIndexFromToken(selected);
    Gdiplus::FontFamily family(kFontFamily);
    Gdiplus::Font font(&family, kFontCaption, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::StringFormat center;
    center.SetAlignment(Gdiplus::StringAlignmentCenter);
    center.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    auto paint = [&](const RectI& box, int index) {
        const bool active = index == on;
        FillRoundRect(g, ToRectF(box), 10.0f, active ? kAccentSubtleArgb : kCompanyPanelArgb,
                      active ? kAccentArgb : kStrokeSubtleArgb, 1.0f);
        Gdiplus::SolidBrush text(Argb(active ? chart_theme::kAccentStrongArgb : kSubtleTextArgb));
        g.DrawString(RegionLabelAt(index), -1, &font, ToRectF(box), &center, &text);
    };
    paint(seg.nationwide, 0);
    paint(seg.gyeongbuk, 1);
}

void DrawOffsetShadow(Gdiplus::Graphics& g, const Gdiplus::RectF& r, float radius, int depth, uint32_t argb) {
    FillRoundRect(g, Gdiplus::RectF(r.X + 1.0f, r.Y + static_cast<Gdiplus::REAL>(depth), r.Width, r.Height), radius,
                  argb, 0, 0);
}

bool CompanyVisible(const std::string& name) {
    if (g_companyItems.empty()) {
        return true;
    }
    bool any_on = false;
    bool this_on = false;
    const auto key = CanonicalizeCompany(name);
    for (const auto& item : g_companyItems) {
        if (item.checked) {
            any_on = true;
        }
        if (item.checked && item.key == key) {
            this_on = true;
        }
    }
    return CompanyChipVisible(any_on, this_on);
}

void ApplyCompanyFlags(int index, bool click_all) {
    std::vector<char> flags;
    flags.reserve(g_companyItems.size());
    for (const auto& item : g_companyItems) {
        flags.push_back(item.checked ? 1 : 0);
    }
    ApplyCompanyClick(flags, index, click_all);
    for (size_t i = 0; i < g_companyItems.size(); ++i) {
        g_companyItems[i].checked = flags[i] != 0;
    }
    g_prepareVersion += 1;
    g_hasStatic = false;
}

void StyleCombo(HWND combo, int w, int h) {
    if (!combo) {
        return;
    }
    SetWindowTheme(combo, L"", L"");
    SendMessageW(combo, CB_SETITEMHEIGHT, static_cast<WPARAM>(-1), h);
    SendMessageW(combo, CB_SETITEMHEIGHT, 0, 28);
    HRGN rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, kComboRadius * 2, kComboRadius * 2);
    SetWindowRgn(combo, rgn, TRUE);
}

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

bool IsDonggukName(const std::string& name) {
    return CanonicalizeCompany(name).find("동국문화재연구원") != std::string::npos;
}

int ColumnCount() {
    return ScatterColumnCount(g_chart.min_rate, g_chart.max_rate, g_chart.step);
}

int ToColumn(double rate) {
    const int col = static_cast<int>(std::llround((rate - g_chart.min_rate) / g_chart.step));
    return std::clamp(col, 0, ColumnCount() - 1);
}

std::string ComboText(HWND combo) {
    const int index = static_cast<int>(SendMessageW(combo, CB_GETCURSEL, 0, 0));
    if (index < 0) {
        return {};
    }
    wchar_t buf[64] = {};
    SendMessageW(combo, CB_GETLBTEXT, index, reinterpret_cast<LPARAM>(buf));
    char utf8[128] = {};
    WideCharToMultiByte(CP_UTF8, 0, buf, -1, utf8, sizeof(utf8), nullptr, nullptr);
    return utf8;
}

void RebuildNotices() {
    auto rows = InPeriod(g_chart.full_rows, g_chart.start_date, g_chart.end_date);
    g_chart.rows = rows;
    std::map<std::string, std::string> dates;
    for (const auto& row : g_chart.full_rows) {
        const auto day = row.open_date.size() >= 10 ? row.open_date.substr(0, 10) : row.open_date;
        if (day < g_chart.start_date || day > g_chart.end_date) {
            continue;
        }
        const auto key = row.notice_number.empty() ? row.notice_no : row.notice_number;
        if (dates.find(key) == dates.end() || row.open_date > dates[key]) {
            dates[key] = row.open_date;
        }
    }
    std::vector<std::pair<std::string, std::string>> ordered(dates.begin(), dates.end());
    std::sort(ordered.begin(), ordered.end(), [](const auto& a, const auto& b) { return a.second < b.second; });
    g_chart.notices.clear();
    g_chart.open_dates.clear();
    for (const auto& [notice, open_date] : ordered) {
        g_chart.notices.push_back(BuildScatterNotice(g_chart.rows, notice, g_chart.min_rate, g_chart.max_rate));
        g_chart.open_dates.push_back(open_date);
    }
}

std::string IsoToday() {
    SYSTEMTIME now{};
    GetLocalTime(&now);
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%04d-%02d-%02d", now.wYear, now.wMonth, now.wDay);
    return buf;
}

std::string IsoMinusMonths(int months) {
    SYSTEMTIME now{};
    GetLocalTime(&now);
    int year = now.wYear;
    int month = now.wMonth - months;
    while (month <= 0) {
        month += 12;
        year -= 1;
    }
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%04d-%02d-%02d", year, month, now.wDay);
    return buf;
}

void ApplyChartFilters() {
    if (!g_period) {
        return;
    }
    const auto period = ComboText(g_period);
    g_chart.end_date = IsoToday();
    if (period == "최근 1개월") {
        g_chart.start_date = IsoMinusMonths(1);
    } else if (period == "최근 3개월") {
        g_chart.start_date = IsoMinusMonths(3);
    } else if (period == "최근 1년") {
        g_chart.start_date = IsoMinusMonths(12);
    } else if (period == "전체기간") {
        g_chart.start_date = "2000-01-01";
    } else {
        g_chart.start_date = IsoMinusMonths(6);
    }
    try {
        g_chart.step = std::stod(ComboText(g_precision));
        g_chart.min_rate = std::stod(ComboText(g_min));
        g_chart.max_rate = std::stod(ComboText(g_max));
    } catch (...) {
        g_chart.step = 0.01;
        g_chart.min_rate = 99.0;
        g_chart.max_rate = 101.0;
    }
    RebuildNotices();
    g_prepareVersion += 1;
    g_hasStatic = false;
    if (g_chartHwnd) {
        UpdateScroll(g_chartHwnd);
        InvalidateRect(g_chartHwnd, nullptr, FALSE);
    }
}

void EnsureUiFont() {
    if (g_uiFont) {
        return;
    }
    g_uiFont = CreateFontW(-13, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS,
                           CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, VARIABLE_PITCH | FF_DONTCARE, kFontFamily);
}

void ApplyUiFont(HWND hwnd) {
    if (hwnd && g_uiFont) {
        SendMessageW(hwnd, WM_SETFONT, reinterpret_cast<WPARAM>(g_uiFont), TRUE);
    }
}

void LayoutChartDock(HWND hwnd) {
    RECT rc{};
    GetClientRect(hwnd, &rc);
    g_chrome = LayoutChartChrome(rc.right, rc.bottom, static_cast<int>(g_companyItems.size()));
    const int combo_drop = 220;
    auto place = [&](HWND combo, const RectI& box) {
        if (!combo) {
            return;
        }
        MoveWindow(combo, box.x, box.y, box.w, combo_drop, TRUE);
        StyleCombo(combo, box.w, kComboH);
    };
    place(g_period, g_chrome.combo_period);
    place(g_precision, g_chrome.combo_precision);
    place(g_min, g_chrome.combo_min);
    place(g_max, g_chrome.combo_max);
}

void AddCombo(HWND combo, const wchar_t* text, bool select) {
    const int i = static_cast<int>(SendMessageW(combo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(text)));
    if (select) {
        SendMessageW(combo, CB_SETCURSEL, i, 0);
    }
}

HWND MakeCombo(HWND hwnd, HINSTANCE instance, int id) {
    return CreateWindowW(L"COMBOBOX", L"",
                         WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST | CBS_OWNERDRAWFIXED | CBS_HASSTRINGS,
                         0, 0, 120, 220, hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance, nullptr);
}

void CreateFilterDock(HWND hwnd, HINSTANCE instance) {
    EnsureUiFont();
    g_period = MakeCombo(hwnd, instance, kIdPeriod);
    AddCombo(g_period, L"최근 1개월", false);
    AddCombo(g_period, L"최근 3개월", false);
    AddCombo(g_period, L"최근 6개월", false);
    AddCombo(g_period, L"최근 1년", true);
    AddCombo(g_period, L"전체기간", false);
    g_precision = MakeCombo(hwnd, instance, kIdPrecision);
    AddCombo(g_precision, L"0.01", true);
    AddCombo(g_precision, L"0.001", false);
    g_min = MakeCombo(hwnd, instance, kIdMin);
    AddCombo(g_min, L"99.00000", true);
    AddCombo(g_min, L"99.50000", false);
    g_max = MakeCombo(hwnd, instance, kIdMax);
    AddCombo(g_max, L"101.00000", true);
    AddCombo(g_max, L"100.00000", false);
    ApplyUiFont(g_period);
    ApplyUiFont(g_precision);
    ApplyUiFont(g_min);
    ApplyUiFont(g_max);
    std::set<std::string> names;
    for (const auto& row : g_chart.full_rows) {
        const auto canon = CanonicalizeCompany(row.normalized_company_name.empty() ? row.company_name
                                                                                   : row.normalized_company_name);
        if (!canon.empty()) {
            names.insert(canon);
        }
    }
    g_companyItems.clear();
    for (const auto& name : names) {
        g_companyItems.push_back({name, Utf8ToWide(BuildShortLabel(name)), false});
    }
    LayoutChartDock(hwnd);
}

uint32_t ScaleArgb(uint32_t argb, double scale) {
    auto channel = [&](int shift) {
        const int v = static_cast<int>(std::lround(static_cast<double>((argb >> shift) & 0xFFu) * scale));
        return static_cast<uint32_t>(std::clamp(v, 0, 255)) << shift;
    };
    return (argb & 0xFF000000u) | channel(16) | channel(8) | channel(0);
}

double ColorDistance(uint32_t a, uint32_t b) {
    const int dr = static_cast<int>((a >> 16) & 0xFF) - static_cast<int>((b >> 16) & 0xFF);
    const int dg = static_cast<int>((a >> 8) & 0xFF) - static_cast<int>((b >> 8) & 0xFF);
    const int db = static_cast<int>(a & 0xFF) - static_cast<int>(b & 0xFF);
    return std::sqrt(static_cast<double>(dr * dr + dg * dg + db * db));
}

Gdiplus::Color CompanyColor(const std::string& name) {
    const auto canonical = CanonicalizeCompany(name);
    if (IsDonggukName(canonical)) {
        return Argb(kDonggukArgb);
    }
    const auto wide = Utf8ToWide(canonical.empty() ? name : canonical);
    int32_t hash = 23;
    for (wchar_t ch : wide) {
        hash = static_cast<int32_t>(hash * 31 + static_cast<int32_t>(static_cast<uint16_t>(ch)));
    }
    const int n = static_cast<int>(kCompanyPalette.size());
    int idx = hash % n;
    if (idx < 0) {
        idx = -idx;
    }
    uint32_t color = kCompanyPalette[static_cast<size_t>(idx % n)];
    const int cycle = std::abs(hash / n) % 3;
    if (cycle == 1) {
        color = ScaleArgb(color, 0.85);
    } else if (cycle == 2) {
        color = ScaleArgb(color, 1.10);
    }
    if (ColorDistance(color, kDonggukArgb) < 70.0) {
        color = kCompanyPalette[static_cast<size_t>((idx + 9) % n)];
    }
    return Argb(color);
}

void SyncWorld(RECT client) {
    g_chrome = LayoutChartChrome(static_cast<int>(client.right), static_cast<int>(client.bottom),
                                 static_cast<int>(g_companyItems.size()));
    const int cell = CellSize(g_chart.view.scale);
    g_chart.view.plot_w = g_chrome.plot.w;
    g_chart.view.plot_h = g_chrome.plot.h;
    g_chart.view.world_w = ColumnCount() * cell;
    g_chart.view.world_h = ScatterWorldHeight(static_cast<int>(g_chart.notices.size()), cell);
    g_chart.view = ClampView(g_chart.view);
}

void DrawChipLabel(Gdiplus::Graphics& g, const Gdiplus::Rect& rect, const std::wstring& label,
                   const Gdiplus::FontFamily& family, int cell) {
    if (label.empty() || rect.Width < 6 || rect.Height < 6) {
        return;
    }
    const float font_px = ChipLabelScreenPx(cell, 2);
    Gdiplus::Font font(&family, font_px, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::StringFormat fmt;
    fmt.SetAlignment(Gdiplus::StringAlignmentCenter);
    fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    fmt.SetFormatFlags(Gdiplus::StringFormatFlagsNoWrap | Gdiplus::StringFormatFlagsNoClip);
    fmt.SetTrimming(Gdiplus::StringTrimmingNone);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);
    g.SetClip(rect);
    Gdiplus::SolidBrush white(Argb(0xFFFFFFFFu));
    const Gdiplus::RectF box(static_cast<Gdiplus::REAL>(rect.X), static_cast<Gdiplus::REAL>(rect.Y),
                             static_cast<Gdiplus::REAL>(rect.Width), static_cast<Gdiplus::REAL>(rect.Height));
    g.DrawString(label.c_str(), -1, &font, box, &fmt, &white);
}

void ReleaseFrame() {
    if (g_frameDc) {
        SelectObject(g_frameDc, g_frameOld);
        DeleteDC(g_frameDc);
        g_frameDc = nullptr;
        g_frameOld = nullptr;
    }
    if (g_frameBmp) {
        DeleteObject(g_frameBmp);
        g_frameBmp = nullptr;
    }
    g_frameW = 0;
    g_frameH = 0;
}

void ReleaseLayer() {
    if (g_layerDc) {
        SelectObject(g_layerDc, g_layerOld);
        DeleteDC(g_layerDc);
        g_layerDc = nullptr;
        g_layerOld = nullptr;
    }
    if (g_layerBmp) {
        DeleteObject(g_layerBmp);
        g_layerBmp = nullptr;
    }
    g_layerW = 0;
    g_layerH = 0;
    g_hasStatic = false;
    ReleaseFrame();
}

void EnsureLayer(HDC ref, int w, int h) {
    if (g_layerDc && g_layerW == w && g_layerH == h && g_layerBmp) {
        return;
    }
    ReleaseLayer();
    g_layerDc = CreateCompatibleDC(ref);
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = w;
    bmi.bmiHeader.biHeight = -h;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    g_layerBmp = CreateDIBSection(ref, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!g_layerBmp) {
        g_layerBmp = CreateCompatibleBitmap(ref, w, h);
    }
    g_layerOld = SelectObject(g_layerDc, g_layerBmp);
    g_layerW = w;
    g_layerH = h;
}

void EnsureFrame(HDC ref, int w, int h) {
    if (g_frameDc && g_frameW == w && g_frameH == h && g_frameBmp) {
        return;
    }
    ReleaseFrame();
    g_frameDc = CreateCompatibleDC(ref);
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = w;
    bmi.bmiHeader.biHeight = -h;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    g_frameBmp = CreateDIBSection(ref, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!g_frameBmp) {
        g_frameBmp = CreateCompatibleBitmap(ref, w, h);
    }
    g_frameOld = SelectObject(g_frameDc, g_frameBmp);
    g_frameW = w;
    g_frameH = h;
}

void PresentChart(HDC hdc, int w, int h) {
    EnsureFrame(hdc, w, h);
    BitBlt(g_frameDc, 0, 0, w, h, g_layerDc, 0, 0, SRCCOPY);
    {
        Gdiplus::Graphics overlay(g_frameDc);
        overlay.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        overlay.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);
        PaintDonggukChips(overlay);
        PaintHoverOverlay(overlay);
    }
    BitBlt(hdc, 0, 0, w, h, g_frameDc, 0, 0, SRCCOPY);
}

int FilterHash() {
    int hash = 0;
    for (const auto& item : g_companyItems) {
        hash = hash * 131 + (item.checked ? 1 : 0);
    }
    return hash;
}

ScatterViewCacheKey MakeViewKey(int width, int height) {
    ScatterViewCacheKey key;
    key.prepare = g_prepareVersion;
    key.pixel_w = width;
    key.pixel_h = height;
    key.scale_milli = static_cast<int>(std::llround(g_chart.view.scale * 1000.0));
    key.scroll_x = g_chart.view.scroll_x;
    key.scroll_y = g_chart.view.scroll_y;
    key.plot_w = g_chart.view.plot_w;
    key.plot_h = g_chart.view.plot_h;
    key.cols = ColumnCount();
    key.rows = static_cast<int>(g_chart.notices.size());
    key.filter_hash = FilterHash();
    key.chrome_hot = ChromeHot(g_hotCompany, g_hotReset, g_hotAll);
    return key;
}

void PaintDonggukChips(Gdiplus::Graphics& g) {
    const int cell = CellSize(g_chart.view.scale);
    const int plotL = g_chrome.plot.x;
    const int plotT = g_chrome.plot.y;
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    const int rows = static_cast<int>(g_chart.notices.size());
    const Gdiplus::Rect plot(plotL, plotT, plotW, plotH);
    const unsigned elapsed = GetTickCount() - g_bounceOrigin;
    const float scale = DonggukBounceScale(elapsed);
    const float lift = DonggukBounceLiftPx(elapsed);
    Gdiplus::FontFamily family(kFontFamily);
    g.SetClip(plot);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    for (int r = 0; r < rows; ++r) {
        const int y = plotT + (r * cell) - g_chart.view.scroll_y;
        if (y + cell < plotT || y > plotT + plotH) {
            continue;
        }
        const auto& scatter = g_chart.notices[r];
        std::map<int, std::vector<const ParticipantRow*>> buckets;
        for (const auto& chip : scatter.chips) {
            if (!chip.adjust_rate) {
                continue;
            }
            const auto chip_name = chip.normalized_company_name.empty() ? chip.company_name : chip.normalized_company_name;
            if (!IsDonggukName(chip_name) || !CompanyVisible(chip_name)) {
                continue;
            }
            buckets[ToColumn(*chip.adjust_rate)].push_back(&chip);
        }
        for (auto& [col, bucket] : buckets) {
            for (int i = 0; i < static_cast<int>(bucket.size()); ++i) {
                const auto& chip = *bucket[i];
                const int side = (std::max)(2, cell - 2);
                const int x = plotL + (col * cell) - g_chart.view.scroll_x + ((cell - side) / 2) + ((i % 2) * 1);
                const int yy = y + ((cell - side) / 2) + (((i / 2) % 2) * 1);
                const float cx = static_cast<float>(x) + static_cast<float>(side) * 0.5f;
                const float cy = static_cast<float>(yy) + static_cast<float>(side) * 0.5f;
                const float ns = static_cast<float>(side) * scale;
                Gdiplus::RectF box(cx - ns * 0.5f, cy - ns * 0.5f + lift, ns, ns);
                Gdiplus::Rect rect(static_cast<INT>(std::floor(box.X)), static_cast<INT>(std::floor(box.Y)),
                                   static_cast<INT>(std::ceil(box.Width)), static_cast<INT>(std::ceil(box.Height)));
                const auto color_name =
                    chip.normalized_company_name.empty() ? chip.company_name : chip.normalized_company_name;
                PaintChipBody(g, box, cell, CompanyColor(color_name).GetValue(),
                              chip.rank == 1 && !nara::LooksLikeOpenApiHundred(chip)
                                  && !nara::NoticeHasLowerLimitAwardCluster(scatter.chips));
                if (!BounceOverlaySkipsLabels() && (!SkipChipLabelsDuringPan() || !g_chart.panning)) {
                    DrawChipLabel(g, rect, Utf8ToWide(BuildShortLabel(color_name)), family, cell);
                }
                g.SetClip(plot);
            }
        }
    }
    g.ResetClip();
}

void PaintHoverOverlay(Gdiplus::Graphics& g) {
    if (!g_chart.hover_valid) {
        return;
    }
    const int plotL = g_chrome.plot.x;
    const int plotT = g_chrome.plot.y;
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    Gdiplus::FontFamily family(kFontFamily);
    Gdiplus::Font hover_font(&family, kFontHover, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Pen pointer(Argb(kPointerArgb), kPointerWidth);
    const Gdiplus::REAL dashes[] = {4.0f, 3.0f};
    pointer.SetDashPattern(dashes, 2);
    g.SetClip(Gdiplus::Rect(plotL, plotT, plotW, plotH));
    g.DrawLine(&pointer, g_chart.hover_x, plotT + 1, g_chart.hover_x, plotT + plotH - 1);
    g.DrawLine(&pointer, plotL + 1, g_chart.hover_y, plotL + plotW - 1, g_chart.hover_y);
    wchar_t rate_text[48];
    if (g_chart.step <= 0.0015) {
        swprintf_s(rate_text, L"기초대비사정률 %.3f", g_chart.hover_rate);
    } else {
        swprintf_s(rate_text, L"기초대비사정률 %.2f", g_chart.hover_rate);
    }
    const int label_w = 168;
    const int label_h = 26;
    int tx = g_chart.hover_x + 12;
    if (tx + label_w > plotL + plotW - 4) {
        tx = g_chart.hover_x - label_w - 12;
    }
    int ty = g_chart.hover_y - label_h - 10;
    if (ty < plotT + 4) {
        ty = g_chart.hover_y + 10;
    }
    tx = std::clamp(tx, plotL + 4, plotL + plotW - label_w - 4);
    ty = std::clamp(ty, plotT + 4, plotT + plotH - label_h - 4);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    FillRoundRect(g, Gdiplus::RectF(static_cast<Gdiplus::REAL>(tx), static_cast<Gdiplus::REAL>(ty),
                                    static_cast<Gdiplus::REAL>(label_w), static_cast<Gdiplus::REAL>(label_h)),
                  static_cast<float>(kHoverRadius), kPointerLabelBgArgb, kCalloutPenArgb, 1.2f);
    Gdiplus::SolidBrush hover_ink(Argb(kPointerLabelTextArgb));
    g.DrawString(rate_text, -1, &hover_font,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>(tx + 8), static_cast<Gdiplus::REAL>(ty + 5)),
                 &hover_ink);
    g.ResetClip();
}

void PaintChart(HDC hdc, RECT client) {
    const int width = (std::max)(1, static_cast<int>(client.right));
    const int height = (std::max)(1, static_cast<int>(client.bottom));
    SyncWorld(client);
    const auto view_key = MakeViewKey(width, height);
    if (IsOverlayOnlyFrame(g_hasStatic, g_staticKey, view_key) && g_layerDc) {
        PresentChart(hdc, width, height);
        return;
    }
    EnsureLayer(hdc, width, height);
    HDC mem = g_layerDc;

    {
    Gdiplus::Graphics g(mem);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);
    g.Clear(Argb(kWindowBgArgb));

    Gdiplus::LinearGradientBrush accent_bar(Gdiplus::Point(0, 0), Gdiplus::Point(width, 0),
                                            Argb(chart_theme::kAccentBar0Argb), Argb(chart_theme::kAccentBar1Argb));
    g.FillRectangle(&accent_bar, g_chrome.accent.x, g_chrome.accent.y, g_chrome.accent.w, g_chrome.accent.h);

    Gdiplus::FontFamily family(kFontFamily);
    Gdiplus::Font axis_font(&family, kFontAxis, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::Font axis_bold(&family, kFontAxis, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font eyebrow(&family, kFontEyebrow, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font caption(&family, kFontCaption, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::Font company_font(&family, kFontCompany, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font empty_font(&family, kFontEmpty, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush muted(Argb(kSubtleTextArgb));
    Gdiplus::SolidBrush caption_brush(Argb(kMutedCaptionArgb));
    Gdiplus::SolidBrush ink(Argb(kTextArgb));
    Gdiplus::SolidBrush copper(Argb(chart_theme::kAccentStrongArgb));
    Gdiplus::SolidBrush white(Argb(kPlotBgArgb));
    Gdiplus::Pen grid(Argb(kGridArgb), kGridWidth);
    Gdiplus::Pen major(Argb(kMajorGridArgb), kGridWidth);
    Gdiplus::Pen axis(Argb(kAxisArgb), kAxisWidth);
    Gdiplus::StringFormat center;
    center.SetAlignment(Gdiplus::StringAlignmentCenter);
    center.SetLineAlignment(Gdiplus::StringAlignmentCenter);

    const int cell = CellSize(g_chart.view.scale);
    const int plotL = g_chrome.plot.x;
    const int plotT = g_chrome.plot.y;
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    const int cols = ColumnCount();
    const int rows = static_cast<int>(g_chart.notices.size());
    const Gdiplus::Rect plot(plotL, plotT, plotW, plotH);
    const Gdiplus::RectF card = ToRectF(g_chrome.map_card);
    DrawOffsetShadow(g, card, static_cast<float>(kCardRadius), kCardShadowDepth, kCardShadowArgb);
    FillRoundRect(g, card, static_cast<float>(kCardRadius), kCardFillArgb, kCardStrokeArgb, kCardStrokeW);
    Gdiplus::SolidBrush chart_bg(Argb(kBgArgb));
    g.FillRectangle(&chart_bg, g_chrome.map_inner.x, g_chrome.map_inner.y, g_chrome.map_inner.w, g_chrome.map_inner.h);

    g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
    g.FillRectangle(&white, plot);
    g.DrawRectangle(&axis, plot);
    g.SetClip(plot);
    for (int col = 0; col <= cols; ++col) {
        const int x = plotL + (col * cell) - g_chart.view.scroll_x;
        if (x < plotL - cell || x > plotL + plotW + cell) {
            continue;
        }
        g.DrawLine(col % 10 == 0 ? &major : &grid, x, plotT, x, plotT + plotH);
    }
    const int grid_rows = rows + kBottomPadCells;
    for (int r = 0; r <= grid_rows; ++r) {
        const int y = plotT + (r * cell) - g_chart.view.scroll_y;
        if (y < plotT - cell || y > plotT + plotH + cell) {
            continue;
        }
        g.DrawLine(&grid, plotL, y, plotL + plotW, y);
    }

    if (rows == 0) {
        g.ResetClip();
        Gdiplus::StringFormat center;
        center.SetAlignment(Gdiplus::StringAlignmentCenter);
        center.SetLineAlignment(Gdiplus::StringAlignmentCenter);
        wchar_t empty[160];
        swprintf_s(empty, L"동국 참여 낙찰공고가 없습니다. 지역을 %s에서 바꿔 보세요.",
                   RegionLabelAt(RegionIndexFromToken(g_region_token)));
        g.DrawString(empty, -1, &empty_font,
                     Gdiplus::RectF(static_cast<Gdiplus::REAL>(plotL), static_cast<Gdiplus::REAL>(plotT),
                                    static_cast<Gdiplus::REAL>(plotW), static_cast<Gdiplus::REAL>(plotH)),
                     &center, &muted);
    }

    for (int r = 0; r < rows; ++r) {
        const int y = plotT + (r * cell) - g_chart.view.scroll_y;
        if (y + cell < plotT || y > plotT + plotH) {
            continue;
        }
        const auto& scatter = g_chart.notices[r];
        const std::string open_date = r < static_cast<int>(g_chart.open_dates.size()) ? g_chart.open_dates[r] : "";
        const auto caption_utf8 = FormatNoticeYLabel(scatter.notice_number, open_date);
        g.ResetClip();
        g.SetClip(Gdiplus::Rect(g_chrome.map_inner.x + 6, plotT, plotL - g_chrome.map_inner.x - 10, plotH));
        Gdiplus::StringFormat yfmt;
        yfmt.SetAlignment(Gdiplus::StringAlignmentNear);
        yfmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
        yfmt.SetTrimming(Gdiplus::StringTrimmingEllipsisCharacter);
        yfmt.SetFormatFlags(Gdiplus::StringFormatFlagsNoWrap);
        const Gdiplus::RectF ybox(static_cast<Gdiplus::REAL>(g_chrome.map_inner.x + 6), static_cast<Gdiplus::REAL>(y),
                                  static_cast<Gdiplus::REAL>(plotL - g_chrome.map_inner.x - 12),
                                  static_cast<Gdiplus::REAL>(cell));
        g.DrawString(Utf8ToWide(caption_utf8).c_str(), -1, &axis_font, ybox, &yfmt, &muted);
        g.SetClip(plot);

        std::map<int, std::vector<const ParticipantRow*>> buckets;
        for (const auto& chip : scatter.chips) {
            if (!chip.adjust_rate) {
                continue;
            }
            const auto chip_name = chip.normalized_company_name.empty() ? chip.company_name : chip.normalized_company_name;
            if (!CompanyVisible(chip_name)) {
                continue;
            }
            buckets[ToColumn(*chip.adjust_rate)].push_back(&chip);
        }
        for (auto& [col, bucket] : buckets) {
            std::sort(bucket.begin(), bucket.end(), [](const ParticipantRow* a, const ParticipantRow* b) {
                const auto name_a = a->normalized_company_name.empty() ? a->company_name : a->normalized_company_name;
                const auto name_b = b->normalized_company_name.empty() ? b->company_name : b->normalized_company_name;
                return ChipPaintBefore(IsDonggukName(name_a), a->rank == 1, a->rank, IsDonggukName(name_b),
                                       b->rank == 1, b->rank);
            });
            for (int i = 0; i < static_cast<int>(bucket.size()); ++i) {
                const auto& chip = *bucket[i];
                const int side = (std::max)(2, cell - 2);
                const int x = plotL + (col * cell) - g_chart.view.scroll_x + ((cell - side) / 2) + ((i % 2) * 1);
                const int yy = y + ((cell - side) / 2) + (((i / 2) % 2) * 1);
                const auto color_name = chip.normalized_company_name.empty() ? chip.company_name
                                                                            : chip.normalized_company_name;
                Gdiplus::Rect rect(x, yy, side, side);
                Gdiplus::RectF box(static_cast<Gdiplus::REAL>(x), static_cast<Gdiplus::REAL>(yy),
                                   static_cast<Gdiplus::REAL>(side), static_cast<Gdiplus::REAL>(side));
                g.SetClip(plot);
                const bool rank1 = chip.rank == 1 && !nara::LooksLikeOpenApiHundred(chip)
                                   && !nara::NoticeHasLowerLimitAwardCluster(scatter.chips);
                PaintChipBody(g, box, cell, CompanyColor(color_name).GetValue(), rank1);
                if (rank1) {
                    const int bw = (std::max)(6, static_cast<int>(side * 0.42));
                    const int bh = (std::max)(6, static_cast<int>(side * 0.38));
                    Gdiplus::RectF badge(static_cast<Gdiplus::REAL>(rect.X + side - bw),
                                         static_cast<Gdiplus::REAL>(rect.Y + 1), static_cast<Gdiplus::REAL>(bw),
                                         static_cast<Gdiplus::REAL>(bh));
                    FillRoundRect(g, badge, static_cast<float>(kRankBadgeRadius), kRankBadgeArgb, 0, 0);
                }
                if (!SkipChipLabelsDuringPan() || !g_chart.panning) {
                    const auto short_name = BuildShortLabel(color_name);
                    DrawChipLabel(g, rect, Utf8ToWide(short_name), family, cell);
                }
                g.SetClip(plot);
            }
        }
    }

    g.ResetClip();
    g.DrawLine(&axis, plotL, plotT + plotH, plotL + plotW, plotT + plotH);
    g.DrawLine(&axis, plotL, plotT, plotL, plotT + plotH);
    g.DrawString(kAxisNotice, -1, &axis_bold,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>(g_chrome.map_inner.x + 6),
                                 static_cast<Gdiplus::REAL>((std::max)(g_chrome.map_inner.y + 2, plotT - 16))),
                 &muted);
    g.DrawString(kAxisRate, -1, &axis_bold,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>((std::max)(plotL + 4, plotL + plotW - 116)),
                                 static_cast<Gdiplus::REAL>(plotT + plotH + 22)),
                 &muted);
    for (int col = 0; col <= cols; col += 10) {
        const int x = plotL + (col * cell) - g_chart.view.scroll_x + cell / 2;
        if (x < plotL - 8 || x > plotL + plotW) {
            continue;
        }
        g.DrawLine(&axis, x, plotT + plotH, x, plotT + plotH + 4);
        const double rate = ColumnToRate(col, g_chart.min_rate, g_chart.step);
        wchar_t tick[16];
        if (g_chart.step <= 0.0015) {
            swprintf_s(tick, L"%.3f", rate);
        } else {
            swprintf_s(tick, L"%.2f", rate);
        }
        g.DrawString(tick, -1, &axis_font,
                     Gdiplus::PointF(static_cast<Gdiplus::REAL>(x - 18), static_cast<Gdiplus::REAL>(plotT + plotH + 6)),
                     &muted);
    }

    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    const Gdiplus::RectF toolbar = ToRectF(g_chrome.reset_toolbar);
    const Gdiplus::RectF reset = ToRectF(g_chrome.reset_btn);
    DrawOffsetShadow(g, toolbar, static_cast<float>(kToolbarRadius), kToolbarShadowDepth, kResetShadowArgb);
    FillRoundRect(g, toolbar, static_cast<float>(kToolbarRadius), kGlassFillArgb, kStrokeSubtleArgb, 1.0f);
    DrawOffsetShadow(g, reset, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowArgb);
    FillRoundRect(g, reset, static_cast<float>(kResetBtnRadius), kGlassFillArgb,
                  g_hotReset ? kAccentArgb : kStrokeStrongArgb, 1.0f);
    Gdiplus::Font reset_font(&family, kFontReset, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    g.DrawString(kResetLabel, -1, &reset_font, reset, &center, &ink);

    const Gdiplus::RectF dock = ToRectF(g_chrome.dock);
    DrawOffsetShadow(g, dock, static_cast<float>(kDockRadius), kCardShadowDepth, kCardShadowArgb);
    FillRoundRect(g, dock, static_cast<float>(kDockRadius), kDockFillArgb, kStrokeSubtleArgb, 1.0f);
    Gdiplus::Pen divider(Argb(kStrokeSubtleArgb), 1.0f);
    g.DrawLine(&divider, static_cast<Gdiplus::REAL>(g_chrome.divider.x), static_cast<Gdiplus::REAL>(g_chrome.divider.y),
               static_cast<Gdiplus::REAL>(g_chrome.divider.x), static_cast<Gdiplus::REAL>(g_chrome.divider.Bottom()));

    auto caption_at = [&](const RectI& combo, const wchar_t* label) {
        g.DrawString(label, -1, &caption,
                     Gdiplus::PointF(static_cast<Gdiplus::REAL>(combo.x),
                                     static_cast<Gdiplus::REAL>(combo.y - kCaptionH)),
                     &caption_brush);
    };
    g.DrawString(kRangeLabel, -1, &eyebrow,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>(g_chrome.range_pane.x),
                                 static_cast<Gdiplus::REAL>(g_chrome.range_pane.y)),
                 &copper);
    caption_at(g_chrome.combo_period, kPeriodLabel);
    caption_at(g_chrome.combo_precision, kPrecisionLabel);
    caption_at(g_chrome.combo_region, kRegionLabel);
    PaintRegionSegment(g, g_chrome.combo_region, g_region_token);
    caption_at(g_chrome.combo_min, kMinLabel);
    caption_at(g_chrome.combo_max, kMaxLabel);

    g.DrawString(kCompanyLabel, -1, &eyebrow,
                 Gdiplus::PointF(static_cast<Gdiplus::REAL>(g_chrome.company_pane.x),
                                 static_cast<Gdiplus::REAL>(CompanyHeaderLabelY(g_chrome.company_pane.y))),
                 &copper);
    const auto all_look = MainButtonLook(MainButtonKind::Ghost);
    if (g_hotAll) {
        FillRoundRect(g, ToRectF(g_chrome.company_all), 8.0f, kHoverFillArgb, 0, 0);
    }
    Gdiplus::Font all_font(&family, kFontCompanyAll, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush all_ink(Argb(g_hotAll ? kAccentArgb : all_look.text));
    g.DrawString(kCompanyAllLabel, -1, &all_font, ToRectF(g_chrome.company_all), &center, &all_ink);

    FillRoundRect(g, ToRectF(g_chrome.company_wrap), static_cast<float>(kCompanyPanelRadius), kCompanyPanelArgb,
                  kStrokeSubtleArgb, 1.0f);

    const int n = static_cast<int>(g_companyItems.size());
    Gdiplus::StringFormat wrap_fmt;
    wrap_fmt.SetAlignment(Gdiplus::StringAlignmentNear);
    wrap_fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    wrap_fmt.SetTrimming(Gdiplus::StringTrimmingEllipsisCharacter);
    wrap_fmt.SetFormatFlags(Gdiplus::StringFormatFlagsNoWrap);
    Gdiplus::Font check_font(&family, 11.0f, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    for (int i = 0; i < n; ++i) {
        const RectI cell = CompanyCheckCellRect(g_chrome.company_wrap, i, n);
        if (cell.Bottom() > g_chrome.company_wrap.Bottom() - 1) {
            continue;
        }
        const RectI box = CompanyCheckBoxRect(g_chrome.company_wrap, i, n);
        const bool hot = g_hotCompany == i;
        const bool on = g_companyItems[static_cast<size_t>(i)].checked;
        FillRoundRect(g, ToRectF(box), static_cast<float>(kCheckRadius),
                      on ? chart_theme::kAccentArgb : kCheckFillArgb,
                      on || hot ? chart_theme::kAccentStrongArgb : kStrokeStrongArgb, 1.0f);
        if (on) {
            Gdiplus::SolidBrush mark(Argb(kCheckMarkArgb));
            g.DrawString(L"✓", -1, &check_font, ToRectF(box), &center, &mark);
        }
        Gdiplus::RectF label_box(static_cast<Gdiplus::REAL>(box.Right() + kCheckGap), static_cast<Gdiplus::REAL>(cell.y),
                                 static_cast<Gdiplus::REAL>((std::max)(8, cell.Right() - box.Right() - kCheckGap)),
                                 static_cast<Gdiplus::REAL>(cell.h));
        g.SetClip(label_box);
        g.DrawString(g_companyItems[static_cast<size_t>(i)].display.c_str(), -1, &company_font, label_box, &wrap_fmt,
                     &ink);
        g.ResetClip();
    }
    }

    if (CanCacheStaticLayer(g_chart.panning)) {
        g_staticKey = view_key;
        g_hasStatic = true;
    } else {
        g_hasStatic = false;
    }
    PresentChart(hdc, width, height);
}

void UpdateScroll(HWND hwnd) {
    RECT rc{};
    GetClientRect(hwnd, &rc);
    SyncWorld(rc);
    SCROLLINFO hsi{sizeof(SCROLLINFO), SIF_RANGE | SIF_PAGE | SIF_POS};
    hsi.nMin = -kViewPad;
    hsi.nMax = (std::max)(0, g_chart.view.world_w) + kViewPad;
    hsi.nPage = g_chart.view.plot_w;
    hsi.nPos = g_chart.view.scroll_x;
    SetScrollInfo(hwnd, SB_HORZ, &hsi, TRUE);
    SCROLLINFO vsi{sizeof(SCROLLINFO), SIF_RANGE | SIF_PAGE | SIF_POS};
    vsi.nMin = -kViewPad;
    vsi.nMax = (std::max)(0, g_chart.view.world_h) + kViewPad;
    vsi.nPage = g_chart.view.plot_h;
    vsi.nPos = g_chart.view.scroll_y;
    SetScrollInfo(hwnd, SB_VERT, &vsi, TRUE);
}

int ScrollPos(HWND hwnd, int bar, int code, int pos) {
    SCROLLINFO si{sizeof(si), SIF_ALL};
    GetScrollInfo(hwnd, bar, &si);
    int next = si.nPos;
    switch (code) {
    case SB_LINELEFT:
        next -= CellSize(g_chart.view.scale);
        break;
    case SB_LINERIGHT:
        next += CellSize(g_chart.view.scale);
        break;
    case SB_PAGELEFT:
        next -= static_cast<int>(si.nPage);
        break;
    case SB_PAGERIGHT:
        next += static_cast<int>(si.nPage);
        break;
    case SB_THUMBTRACK:
        next = pos;
        break;
    default:
        break;
    }
    next = std::clamp(next, 0, (std::max)(0, si.nMax - static_cast<int>(si.nPage)));
    si.fMask = SIF_POS;
    si.nPos = next;
    SetScrollInfo(hwnd, bar, &si, TRUE);
    return next;
}

void PaintComboItem(const DRAWITEMSTRUCT* item) {
    Gdiplus::Graphics dg(item->hDC);
    dg.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    dg.SetTextRenderingHint(Gdiplus::TextRenderingHintClearTypeGridFit);
    const RECT& rc = item->rcItem;
    const Gdiplus::RectF r(static_cast<Gdiplus::REAL>(rc.left) + 0.5f, static_cast<Gdiplus::REAL>(rc.top) + 0.5f,
                           static_cast<Gdiplus::REAL>(rc.right - rc.left) - 1.0f,
                           static_cast<Gdiplus::REAL>(rc.bottom - rc.top) - 1.0f);
    const bool edit = (item->itemState & ODS_COMBOBOXEDIT) != 0;
    const bool selected = (item->itemState & ODS_SELECTED) != 0;
    uint32_t fill = kPlotBgArgb;
    if (!edit && selected) {
        fill = kAccentSubtleArgb;
    }
    if (edit) {
        FillRoundRect(dg, r, static_cast<float>(kComboRadius), fill, kStrokeSubtleArgb, 1.0f);
        Gdiplus::SolidBrush chevron(Argb(kMutedCaptionArgb));
        const Gdiplus::REAL cx = r.X + r.Width - 18.0f;
        const Gdiplus::REAL cy = r.Y + r.Height * 0.5f;
        Gdiplus::PointF pts[3] = {{cx - 5.0f, cy - 3.0f}, {cx + 5.0f, cy - 3.0f}, {cx, cy + 3.0f}};
        dg.FillPolygon(&chevron, pts, 3);
    } else {
        Gdiplus::SolidBrush brush(Argb(fill));
        dg.FillRectangle(&brush, r);
    }
    wchar_t caption[64] = {};
    if (item->itemID != 0xFFFFFFFFu) {
        SendMessageW(item->hwndItem, CB_GETLBTEXT, item->itemID, reinterpret_cast<LPARAM>(caption));
    }
    Gdiplus::FontFamily family(kFontFamily);
    Gdiplus::Font font(&family, kFontBody, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush text(Argb(kTextArgb));
    Gdiplus::StringFormat fmt;
    fmt.SetAlignment(Gdiplus::StringAlignmentNear);
    fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
    fmt.SetTrimming(Gdiplus::StringTrimmingEllipsisCharacter);
    fmt.SetFormatFlags(Gdiplus::StringFormatFlagsNoWrap);
    Gdiplus::RectF text_box(r.X + 10.0f, r.Y, (std::max)(8.0f, r.Width - (edit ? 32.0f : 16.0f)), r.Height);
    dg.DrawString(caption, -1, &font, text_box, &fmt, &text);
}

void ResetMapView(HWND hwnd) {
    g_chart.view.scale = 1.0;
    RECT rc{};
    GetClientRect(hwnd, &rc);
    SyncWorld(rc);
    g_chart.view = ShowLatestView(g_chart.view);
    UpdateScroll(hwnd);
    InvalidateRect(hwnd, nullptr, FALSE);
}

int HitCompanyIndex(int mx, int my) {
    const int n = static_cast<int>(g_companyItems.size());
    for (int i = 0; i < n; ++i) {
        if (CompanyCheckCellRect(g_chrome.company_wrap, i, n).Contains(mx, my)) {
            return i;
        }
    }
    return -1;
}

LRESULT CALLBACK ChartProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_PAINT: {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc{};
        GetClientRect(hwnd, &rc);
        PaintChart(hdc, rc);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_TIMER:
        if (wParam == kBounceTimer) {
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        return 0;
    case WM_SIZE:
        LayoutChartDock(hwnd);
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_GETMINMAXINFO: {
        auto* info = reinterpret_cast<MINMAXINFO*>(lParam);
        info->ptMinTrackSize.x = kWindowMinW;
        info->ptMinTrackSize.y = kWindowMinH;
        return 0;
    }
    case WM_MEASUREITEM: {
        auto* measure = reinterpret_cast<MEASUREITEMSTRUCT*>(lParam);
        if (measure->CtlType == ODT_COMBOBOX) {
            measure->itemHeight = 28;
            return TRUE;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    case WM_DRAWITEM: {
        const auto* item = reinterpret_cast<DRAWITEMSTRUCT*>(lParam);
        if (item->CtlType == ODT_COMBOBOX) {
            PaintComboItem(item);
            return TRUE;
        }
        return TRUE;
    }
    case WM_COMMAND: {
        const int id = LOWORD(wParam);
        const int code = HIWORD(wParam);
        if (code == CBN_SELCHANGE
            && (id == kIdPeriod || id == kIdPrecision || id == kIdMin || id == kIdMax)) {
            ApplyChartFilters();
            return 0;
        }
        return 0;
    }
    case WM_SETCURSOR: {
        POINT pt{};
        GetCursorPos(&pt);
        ScreenToClient(hwnd, &pt);
        if (g_chrome.reset_btn.Contains(pt.x, pt.y) || g_chrome.reset_toolbar.Contains(pt.x, pt.y)
            || g_chrome.company_all.Contains(pt.x, pt.y) || HitCompanyIndex(pt.x, pt.y) >= 0) {
            SetCursor(LoadCursor(nullptr, IDC_HAND));
            return TRUE;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_HSCROLL:
        g_chart.view.scroll_x = ScrollPos(hwnd, SB_HORZ, LOWORD(wParam), HIWORD(wParam));
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_VSCROLL:
        g_chart.view.scroll_y = ScrollPos(hwnd, SB_VERT, LOWORD(wParam), HIWORD(wParam));
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_LBUTTONDOWN: {
        const int mx = GET_X_LPARAM(lParam);
        const int my = GET_Y_LPARAM(lParam);
        RECT rc{};
        GetClientRect(hwnd, &rc);
        SyncWorld(rc);
        const auto region_seg = SplitRegionSegment(g_chrome.combo_region);
        const int region_hit = HitRegionSegment(region_seg, mx, my);
        if (region_hit >= 0) {
            if (g_region_handler) {
                g_region_handler(region_hit == 1 ? "경북" : "전국");
            }
            return 0;
        }
        if (g_chrome.reset_btn.Contains(mx, my) || g_chrome.reset_toolbar.Contains(mx, my)) {
            ResetMapView(hwnd);
            return 0;
        }
        if (g_chrome.company_all.Contains(mx, my)) {
            ApplyCompanyFlags(-1, true);
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        const int hit = HitCompanyIndex(mx, my);
        if (hit >= 0) {
            ApplyCompanyFlags(hit, false);
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        if (g_chrome.dock.Contains(mx, my)) {
            return 0;
        }
        if (!g_chrome.plot.Contains(mx, my)) {
            return 0;
        }
        g_chart.drag_armed = true;
        g_chart.panning = false;
        g_chart.last_pt.x = mx;
        g_chart.last_pt.y = my;
        SetCapture(hwnd);
        return 0;
    }
    case WM_MOUSEMOVE: {
        const int mx = GET_X_LPARAM(lParam);
        const int my = GET_Y_LPARAM(lParam);
        if (g_chart.drag_armed || g_chart.panning) {
            const int dx = mx - g_chart.last_pt.x;
            const int dy = my - g_chart.last_pt.y;
            if (!g_chart.panning && (std::abs(dx) + std::abs(dy) >= 4)) {
                g_chart.panning = true;
                SetCursor(LoadCursor(nullptr, IDC_SIZEALL));
            }
            if (g_chart.panning) {
                g_chart.view = PanBy(g_chart.view, dx, dy);
                g_chart.last_pt.x = mx;
                g_chart.last_pt.y = my;
                UpdateScroll(hwnd);
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;
        }
        RECT rc{};
        GetClientRect(hwnd, &rc);
        SyncWorld(rc);
        g_hotReset = g_chrome.reset_btn.Contains(mx, my) || g_chrome.reset_toolbar.Contains(mx, my);
        g_hotAll = g_chrome.company_all.Contains(mx, my);
        g_hotCompany = HitCompanyIndex(mx, my);
        if (g_chrome.plot.Contains(mx, my)) {
            const int col = HoverColumn(mx - g_chrome.plot.x, g_chart.view.scroll_x, g_chart.view.scale);
            g_chart.hover_valid = true;
            g_chart.hover_x = mx;
            g_chart.hover_y = my;
            g_chart.hover_rate = ColumnToRate(col, g_chart.min_rate, g_chart.step);
        } else {
            g_chart.hover_valid = false;
        }
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_LBUTTONUP:
        g_chart.drag_armed = false;
        g_chart.panning = false;
        ReleaseCapture();
        g_hasStatic = false;
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_KEYDOWN:
        if (wParam == VK_HOME) {
            ResetMapView(hwnd);
            return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    case WM_MOUSEWHEEL: {
        POINT pt{GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
        ScreenToClient(hwnd, &pt);
        const bool zoom_in = GET_WHEEL_DELTA_WPARAM(wParam) > 0;
        g_chart.view = ZoomAt(g_chart.view, pt.x - g_chrome.plot.x, pt.y - g_chrome.plot.y, zoom_in);
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_DESTROY:
        KillTimer(hwnd, kBounceTimer);
        g_chartHwnd = nullptr;
        ReleaseLayer();
        if (g_uiFont) {
            DeleteObject(g_uiFont);
            g_uiFont = nullptr;
        }
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}

void UpdateChartTitle() {
    if (!g_chartHwnd || !IsWindow(g_chartHwnd)) {
        return;
    }
    wchar_t title[160];
    swprintf_s(title, L"%s · %s", kWindowTitle, RegionLabelAt(RegionIndexFromToken(g_region_token)));
    SetWindowTextW(g_chartHwnd, title);
}

void RebuildCompanyItems() {
    std::set<std::string> names;
    for (const auto& row : g_chart.full_rows) {
        const auto canon = CanonicalizeCompany(row.normalized_company_name.empty() ? row.company_name
                                                                                   : row.normalized_company_name);
        if (!canon.empty()) {
            names.insert(canon);
        }
    }
    g_companyItems.clear();
    for (const auto& name : names) {
        g_companyItems.push_back({name, Utf8ToWide(BuildShortLabel(name)), false});
    }
}

void ApplySnapshotToChart(
    const std::vector<ParticipantRow>& snapshot,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region) {
    g_chart.full_rows = snapshot;
    g_chart.start_date = start_date;
    g_chart.end_date = end_date;
    g_region_token = CanonicalizeRegion(region).empty() ? "전국" : CanonicalizeRegion(region);
    g_chart.min_rate = kMinRate;
    g_chart.max_rate = kMaxRate;
    if (g_chart.step <= 0.0) {
        g_chart.step = 0.01;
    }
    RebuildNotices();
    RebuildCompanyItems();
    g_prepareVersion += 1;
    g_hasStatic = false;
    UpdateChartTitle();
}

}  // namespace

void SetChartRegionHandler(ChartRegionHandler handler) {
    g_region_handler = handler;
}

bool ChartWindowIsOpen() {
    return g_chartHwnd != nullptr && IsWindow(g_chartHwnd);
}

void RefreshChartSnapshot(
    const std::vector<ParticipantRow>& snapshot,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region) {
    ApplySnapshotToChart(snapshot, start_date, end_date, region);
    if (g_chartHwnd && IsWindow(g_chartHwnd)) {
        UpdateScroll(g_chartHwnd);
        InvalidateRect(g_chartHwnd, nullptr, FALSE);
    }
}

void OpenChartWindow(
    HINSTANCE instance,
    HWND owner,
    const std::vector<ParticipantRow>& snapshot,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region) {
    ApplySnapshotToChart(snapshot, start_date, end_date, region);
    g_chart.view = ChartView{};
    g_chart.view.scroll_y = 1 << 30;
    g_chart.hover_valid = false;
    g_chart.panning = false;

    static bool registered = false;
    if (!registered) {
        WNDCLASSW wc{};
        wc.lpfnWndProc = ChartProc;
        wc.hInstance = instance;
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
        wc.lpszClassName = L"NaraDesktopChart";
        wc.style = CS_HREDRAW | CS_VREDRAW;
        RegisterClassW(&wc);
        registered = true;
    }
    if (g_bounceOrigin == 0) {
        g_bounceOrigin = GetTickCount();
    }
    if (g_chartHwnd && IsWindow(g_chartHwnd)) {
        SetTimer(g_chartHwnd, kBounceTimer, 50, nullptr);
        UpdateScroll(g_chartHwnd);
        InvalidateRect(g_chartHwnd, nullptr, TRUE);
        ShowWindow(g_chartHwnd, SW_SHOW);
        SetForegroundWindow(g_chartHwnd);
        return;
    }
    g_chartHwnd = CreateWindowExW(0, L"NaraDesktopChart", kWindowTitle,
                                  WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_CLIPCHILDREN, CW_USEDEFAULT, CW_USEDEFAULT,
                                  1400, 900, owner, nullptr, instance, nullptr);
    SetTimer(g_chartHwnd, kBounceTimer, 50, nullptr);
    CreateFilterDock(g_chartHwnd, instance);
    UpdateChartTitle();
    RECT work{};
    SystemParametersInfoW(SPI_GETWORKAREA, 0, &work, 0);
    SetWindowPos(g_chartHwnd, HWND_TOP, work.left, work.top, work.right - work.left, work.bottom - work.top,
                 SWP_SHOWWINDOW);
    UpdateScroll(g_chartHwnd);
    InvalidateRect(g_chartHwnd, nullptr, FALSE);
}

}  // namespace nara
