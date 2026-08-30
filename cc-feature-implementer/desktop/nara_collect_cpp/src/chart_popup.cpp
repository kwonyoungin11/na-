#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "nara/chart_popup.h"

#include "nara/award_table.h"
#include "nara/chart_theme.h"
#include "nara/chart_view.h"
#include "nara/histogram_chart.h"
#include "nara/dongguk_band.h"
#include "nara/scatter_cache.h"
#include "nara/igunsul_convert.h"
#include "nara/scatter_chart.h"
#include "nara/dwrite_text.h"

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
using chart_theme::kAxisRank1Pct;
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
using chart_theme::kChipBorderArgb;
using chart_theme::kChipBorderWidth;
using chart_theme::kChipLabelOutlineArgb;
using chart_theme::kCardShadowBlur;
using chart_theme::BlurShadowArgb;
using chart_theme::BlurShadowInflate;
using chart_theme::BlurShadowLayerCount;
using chart_theme::ChipLabelStrokeBehindFill;
using chart_theme::ChipRadiusPx;
using chart_theme::kTextRenderingHintIdeal;
using chart_theme::DpiScaleFromDpi;
using chart_theme::DipToPx;
using chart_theme::PxToDip;
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
using chart_theme::kComboStrokeArgb;
using chart_theme::kComboStrokeW;
using chart_theme::kRegionSegmentRadius;
using chart_theme::kMapInnerRadius;
using chart_theme::kPlotRadius;
using chart_theme::kDockFillArgb;
using chart_theme::kDockMinH;
using chart_theme::kDockRadius;
using chart_theme::kHoverFillArgb;
using chart_theme::kDonggukArgb;
using chart_theme::ChartPageUsesFullCard;
using chart_theme::ChartShowsHistogram;
using chart_theme::ChartShowsMapToolbar;
using chart_theme::ChartShowsScatter;
using chart_theme::ChartTabLabelAt;
using chart_theme::ChartTabRect;
using chart_theme::HitChartTab;
using chart_theme::kChartTabCount;
using chart_theme::HitRegionSegment;
using chart_theme::RegionIndexFromToken;
using chart_theme::RegionLabelAt;
using chart_theme::SplitRegionSegment;
using chart_theme::DarkenArgb;
using chart_theme::HistDensityArgb;
using chart_theme::LightenArgb;
using chart_theme::WithAlpha;
using chart_theme::kHistMarketMidArgb;
using chart_theme::kChartTabRadius;
using chart_theme::kHistAxisCount;
using chart_theme::kHistEmpty;
using chart_theme::kHistFloorArgb;
using chart_theme::kHistGridArgb;
using chart_theme::kHistMarketGhostA;
using chart_theme::kHistMarketLabel;
using chart_theme::kHistRank1Label;
using chart_theme::LerpArgb;
using chart_theme::kHistTabOffFillArgb;
using chart_theme::kHistTabOffTextArgb;
using chart_theme::kHistTabOnFillArgb;
using chart_theme::kHistTabOnTextArgb;
using chart_theme::kHistogramBinCount;
using chart_theme::kHistogramMaxSeries;
using chart_theme::BandLegendRect;
using chart_theme::BandRank1GutterRect;
using chart_theme::BandUseStageArgbAt;
using chart_theme::BandUseStageInterval;
using chart_theme::kBandLegendFillArgb;
using chart_theme::kBandLegendPadX;
using chart_theme::kBandLegendStrokeArgb;
using chart_theme::kBandLegendSwatchGap;
using chart_theme::kBandLegendSwatchH;
using chart_theme::kBandLegendSwatchW;
using chart_theme::kBandLegendTextGap;
using chart_theme::kBandMedianArgb;
using chart_theme::kBandMedianWidth;
using chart_theme::kBandPaintMedian;
using chart_theme::kBandPaintOnStaticScatter;
using chart_theme::kBandPaintPreyChips;
using chart_theme::kBandPaintTerritory;
using chart_theme::kBandPaintThreat;
using chart_theme::kBandPaintUseStages;
using chart_theme::kBandPreyChipArgb;
using chart_theme::kBandTerritoryArgb;
using chart_theme::kBandThreatChipArgb;
using chart_theme::kBandThreatFillArgb;
using chart_theme::kBandUseArgb;
using chart_theme::kBandUseStageCount;
using chart_theme::kBandPaintWatch;
using chart_theme::kBandPaintHome;
using chart_theme::kBandPaintSit;
using chart_theme::kBandWatchFillArgb;
using chart_theme::kBandHomeArgb;
using chart_theme::kBandSitArgb;
using chart_theme::kWatchRegularMarkArgb;
using chart_theme::kWatchOnceMarkArgb;
using chart_theme::kSitPulseTimerMs;
using chart_theme::kSitGlowLayerCount;
using chart_theme::SitGlowLayerArgb;
using chart_theme::SitGlowPadDip;
using chart_theme::SitPulseArgb;
using chart_theme::kChartTabPad;
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
using chart_theme::kResetShadowBlur;
using chart_theme::kResetShadowDepth;
using chart_theme::kRank1BtnH;
using chart_theme::kRank1BtnW;
using chart_theme::kRank1Label;
using chart_theme::kMapDesignFillArgb;
using chart_theme::kMapDesignHotArgb;
using chart_theme::kMapDesignLabel;
using chart_theme::kMapDesignTextArgb;
using chart_theme::ChipPassesRank1Filter;
using chart_theme::kToolbarPad;
using chart_theme::kToolbarRadius;
using chart_theme::kToolbarShadowBlur;
using chart_theme::kToolbarShadowDepth;
using chart_theme::kWindowBgArgb;
using chart_theme::kShadowArgb;
using chart_theme::kStrokeStrongArgb;
using chart_theme::kStrokeSubtleArgb;
using chart_theme::kSubtleTextArgb;
using chart_theme::kTextArgb;
using chart_theme::kWindowMinH;
using chart_theme::kWindowMinH;
using chart_theme::kWindowMinW;
using chart_theme::kWindowTitle;
using chart_theme::ChartChrome;
using chart_theme::ChipLabelFontPx;
using chart_theme::ChipLabelOutlineWidthPx;
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
using chart_theme::AxisNoticeTitleY;
using chart_theme::ChipDodgeDrawsLabel;
using chart_theme::ChipGoldInset;
using chart_theme::LayoutChartChrome;
using chart_theme::LayoutChipDodge;
using chart_theme::NoticeYLabelFits;
using chart_theme::RectI;
using chart_theme::ScatterColumnCount;
using chart_theme::UniformGridCell;
using scatter_cache::ChromeHot;
using scatter_cache::IsOverlayOnlyFrame;
using scatter_cache::ScatterViewCacheKey;
using scatter_cache::BounceOverlaySkipsLabels;
using scatter_cache::CanBlitPlotCacheDuringPan;
using scatter_cache::CanStretchPlotCacheDuringZoom;
using scatter_cache::ZoomSettleMs;
using scatter_cache::ZoomSrcOrigin;
using scatter_cache::ZoomSrcSpan;
using scatter_cache::CanCacheStaticLayer;
using scatter_cache::CanReuseWorldLayer;
using scatter_cache::PlotCacheCoversView;
using scatter_cache::PlotCacheOverscanDip;
using scatter_cache::ScatterWorldCacheKey;
using scatter_cache::DonggukBounceEnabled;
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
std::map<std::string, std::string> g_displayLabels;
void RebuildCompanyItems();
HWND g_chartHwnd = nullptr;
HWND g_period = nullptr;
HWND g_precision = nullptr;
HWND g_min = nullptr;
HWND g_max = nullptr;
WNDPROC g_comboOrigProc = nullptr;
std::string g_region_token = "전국";
ChartRegionHandler g_region_handler = nullptr;
HFONT g_uiFont = nullptr;
int g_hotCompany = -1;
bool g_hotReset = false;
bool g_hotAll = false;
bool g_hotRank1 = false;
bool g_hotDesign = false;
bool g_rank1Only = false;
int g_chartTab = 0;
DonggukBandCache g_bandCache;
BandResult g_band;
std::string g_bandNotice;
int g_bandClickRow = -1;
void RefreshBand();
int g_hotTab = -1;
int g_histHoverBin = -1;
int g_histDataHash = 0;
bool g_histReady = false;
HistogramModel g_hist;
HistScene g_histScene{};
float g_dpi_scale = 1.0f;
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
HDC g_plotCacheDc = nullptr;
HBITMAP g_plotCacheBmp = nullptr;
HGDIOBJ g_plotCacheOld = nullptr;
int g_plotCachePxW = 0;
int g_plotCachePxH = 0;
int g_plotCacheOriginX = 0;
int g_plotCacheOriginY = 0;
int g_plotCacheOverscanX = 0;
int g_plotCacheOverscanY = 0;
int g_plotCacheAxisW = 0;
int g_plotCachePlotW = 0;
int g_plotCachePlotH = 0;
ScatterWorldCacheKey g_plotCacheKey{};
bool g_hasPlotCache = false;
ScatterViewCacheKey g_staticKey{};
DWORD g_bounceOrigin = 0;
constexpr UINT_PTR kBounceTimer = 1;
constexpr UINT_PTR kZoomSettleTimer = 2;
constexpr UINT_PTR kSitPulseTimer = 3;
bool g_zooming = false;
int g_staticScrollX = 0;
int g_staticScrollY = 0;
int g_staticCell = 20;
void UpdateScroll(HWND hwnd);
void ReleaseLayer();
void PaintDonggukChips(Gdiplus::Graphics& g);
void PaintHoverOverlay(Gdiplus::Graphics& g);
void PaintChartTabs(Gdiplus::Graphics& g, const Gdiplus::FontFamily& family);
void PaintHistogramHover(Gdiplus::Graphics& g);
void EnsureHistogram();
int FilterHash();

Gdiplus::Color Argb(uint32_t argb) {
    return Gdiplus::Color(argb);
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

void ApplyCrispGraphics(Gdiplus::Graphics& g);
void ApplyDpiTransform(Gdiplus::Graphics& g);
bool MapRank1(const ParticipantRow& chip, const ScatterNotice& scatter);
void SyncWorld(HWND hwnd);

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
    const float scale = DpiScaleFromDpi(WindowDpi(hwnd));
    if (std::fabs(scale - g_dpi_scale) > 0.001f) {
        g_dpi_scale = scale;
        g_hasStatic = false;
        if (g_uiFont) {
            DeleteObject(g_uiFont);
            g_uiFont = nullptr;
        }
    }
}

int ToPx(int dip) { return DipToPx(dip, g_dpi_scale); }
int ToDip(int px) { return PxToDip(px, g_dpi_scale); }

void ApplyDpiTransform(Gdiplus::Graphics& g) { g.ScaleTransform(g_dpi_scale, g_dpi_scale); }
void FillGlyphs(Gdiplus::Graphics& g, const wchar_t* text, const Gdiplus::FontFamily& family, int style, float size,
                const Gdiplus::RectF& box, Gdiplus::StringAlignment h, Gdiplus::StringAlignment v, uint32_t argb);

void PaintChipBody(Gdiplus::Graphics& g, const Gdiplus::RectF& box, int cell, uint32_t fill, bool rank1) {
    const float radius = ChipRadiusPx(cell);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    FillRoundRect(g, box, radius, fill, kChipBorderArgb, kChipBorderWidth);
    if (rank1) {
        Gdiplus::RectF inner = box;
        const float inset = ChipGoldInset();
        if (inner.Width > inset * 2.0f && inner.Height > inset * 2.0f) {
            inner.Inflate(-inset, -inset);
        }
        Gdiplus::GraphicsPath gold_path;
        RoundRectPath(gold_path, inner, radius);
        Gdiplus::Pen gold(Argb(kRankGoldArgb), kGoldWidth);
        gold.SetAlignment(Gdiplus::PenAlignmentInset);
        g.DrawPath(&gold, &gold_path);
    }
}

Gdiplus::RectF ToRectF(const RectI& r) {
    return {static_cast<Gdiplus::REAL>(r.x), static_cast<Gdiplus::REAL>(r.y), static_cast<Gdiplus::REAL>(r.w),
            static_cast<Gdiplus::REAL>(r.h)};
}

void ClipToRoundRect(Gdiplus::Graphics& g, const RectI& r, float radius,
                     Gdiplus::CombineMode mode = Gdiplus::CombineModeReplace) {
    Gdiplus::GraphicsPath path;
    RoundRectPath(path, ToRectF(r), radius);
    g.SetClip(&path, mode);
}

void ClipPlot(Gdiplus::Graphics& g) {
    ClipToRoundRect(g, g_chrome.plot, static_cast<float>(kPlotRadius));
}

void PaintRegionSegment(Gdiplus::Graphics& g, const RectI& slot, const std::string& selected) {
    const auto seg = SplitRegionSegment(slot);
    const int on = RegionIndexFromToken(selected);
    const Gdiplus::RectF outer = ToRectF(slot);
    const float radius = static_cast<float>(kRegionSegmentRadius);
    Gdiplus::GraphicsPath shell;
    RoundRectPath(shell, outer, radius);
    Gdiplus::SolidBrush idle(Argb(kCompanyPanelArgb));
    g.FillPath(&idle, &shell);
    const Gdiplus::GraphicsState saved = g.Save();
    g.SetClip(&shell);
    Gdiplus::SolidBrush active_fill(Argb(kAccentSubtleArgb));
    g.FillRectangle(&active_fill, ToRectF(on == 0 ? seg.nationwide : seg.gyeongbuk));
    g.Restore(saved);
    Gdiplus::Pen border(Argb(kComboStrokeArgb), kComboStrokeW);
    g.DrawPath(&border, &shell);
    const Gdiplus::REAL mid = static_cast<Gdiplus::REAL>(seg.gyeongbuk.x) + 0.5f;
    g.DrawLine(&border, mid, outer.Y + 5.0f, mid, outer.GetBottom() - 5.0f);
    Gdiplus::FontFamily family(kFontFamily);
    auto label = [&](const RectI& box, int index) {
        FillGlyphs(g, RegionLabelAt(index), family, Gdiplus::FontStyleBold, kFontCaption, ToRectF(box),
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter,
                   index == on ? chart_theme::kAccentStrongArgb : kSubtleTextArgb);
    };
    label(seg.nationwide, 0);
    label(seg.gyeongbuk, 1);
}

void DrawOffsetShadow(Gdiplus::Graphics& g, const Gdiplus::RectF& r, float radius, int depth, int blur,
                      uint32_t argb) {
    const int layers = BlurShadowLayerCount(blur);
    for (int i = layers - 1; i >= 0; --i) {
        const float inf = BlurShadowInflate(blur, i, layers);
        FillRoundRect(g,
                      Gdiplus::RectF(r.X - inf + 1.0f, r.Y - inf + static_cast<Gdiplus::REAL>(depth),
                                     r.Width + inf * 2.0f, r.Height + inf * 2.0f),
                      radius + inf, BlurShadowArgb(argb, i, layers), 0, 0);
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

void PaintDesignPill(Gdiplus::Graphics& g, const Gdiplus::FontFamily& family) {
    const Gdiplus::RectF design = ToRectF(g_chrome.design_btn);
    DrawOffsetShadow(g, design, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                     kResetShadowArgb);
    FillRoundRect(g, design, static_cast<float>(kResetBtnRadius),
                  g_hotDesign ? kMapDesignHotArgb : kMapDesignFillArgb, kMapDesignFillArgb, 1.0f);
    FillGlyphs(g, kMapDesignLabel, family, Gdiplus::FontStyleBold, kFontReset, design, Gdiplus::StringAlignmentCenter,
               Gdiplus::StringAlignmentCenter, kMapDesignTextArgb);
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
    RefreshBand();
}

void PaintClosedCombo(HWND hwnd, HDC hdc) {
    RECT rc{};
    GetClientRect(hwnd, &rc);
    const float dip_w = static_cast<float>(ToDip(rc.right - rc.left));
    const float dip_h = static_cast<float>(ToDip(rc.bottom - rc.top));
    const float h = (std::min)(static_cast<float>(kComboH), dip_h);
    const Gdiplus::RectF r(0.5f, 0.5f, dip_w - 1.0f, h - 1.0f);
    wchar_t caption[64] = {};
    const int index = static_cast<int>(SendMessageW(hwnd, CB_GETCURSEL, 0, 0));
    if (index >= 0) {
        SendMessageW(hwnd, CB_GETLBTEXT, index, reinterpret_cast<LPARAM>(caption));
    }
    {
        Gdiplus::Graphics dg(hdc);
        ApplyCrispGraphics(dg);
        ApplyDpiTransform(dg);
        const bool focused = GetFocus() == hwnd;
        FillRoundRect(dg, r, static_cast<float>(kComboRadius), kPlotBgArgb,
                      focused ? chart_theme::kAccentArgb : kComboStrokeArgb, kComboStrokeW);
        Gdiplus::SolidBrush chevron(Argb(kMutedCaptionArgb));
        const Gdiplus::REAL cx = r.X + r.Width - 16.0f;
        const Gdiplus::REAL cy = r.Y + r.Height * 0.5f;
        Gdiplus::PointF pts[3] = {{cx - 5.0f, cy - 3.0f}, {cx + 5.0f, cy - 3.0f}, {cx, cy + 3.0f}};
        dg.FillPolygon(&chevron, pts, 3);
    }
    nara::dwrite_text::ResetQueue();
    nara::dwrite_text::QueueBox(r.X + 10.0f, r.Y, (std::max)(8.0f, r.Width - 32.0f), r.Height, caption, kFontBody,
                                kTextArgb, nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, false,
                                true, true);
    nara::dwrite_text::Flush(hdc, rc.right, rc.bottom, g_dpi_scale);
}

LRESULT CALLBACK ComboProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (msg == WM_PAINT) {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        PaintClosedCombo(hwnd, hdc);
        EndPaint(hwnd, &ps);
        return 0;
    }
    if (msg == WM_PRINTCLIENT) {
        PaintClosedCombo(hwnd, reinterpret_cast<HDC>(wParam));
        return 0;
    }
    if (msg == WM_ERASEBKGND) {
        return 1;
    }
    if (!g_comboOrigProc) {
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    return CallWindowProcW(g_comboOrigProc, hwnd, msg, wParam, lParam);
}

void StyleCombo(HWND combo, int w, int h) {
    if (!combo) {
        return;
    }
    SetWindowTheme(combo, L"", L"");
    SendMessageW(combo, CB_SETITEMHEIGHT, static_cast<WPARAM>(-1), h);
    SendMessageW(combo, CB_SETITEMHEIGHT, 0, 28);
    const int rad = ToPx(kComboRadius * 2);
    HRGN rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, rad, rad);
    SetWindowRgn(combo, rgn, TRUE);
    const auto current = reinterpret_cast<WNDPROC>(GetWindowLongPtrW(combo, GWLP_WNDPROC));
    if (current != ComboProc) {
        const auto prev = reinterpret_cast<WNDPROC>(
            SetWindowLongPtrW(combo, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(ComboProc)));
        if (!g_comboOrigProc) {
            g_comboOrigProc = prev;
        }
    }
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

std::string RowCompanyName(const ParticipantRow& row) {
    return row.company_name.empty() ? row.normalized_company_name : row.company_name;
}

void PaintWatchChipMark(Gdiplus::Graphics& g, const Gdiplus::Rect& rect, const ParticipantRow& chip) {
    if (!chip.adjust_rate || !g_band.has_watch) {
        return;
    }
    const double rate = *chip.adjust_rate;
    if (rate + 1e-12 < g_band.watch_lo || rate >= g_band.home_hi) {
        return;
    }
    const auto name = RowCompanyName(chip);
    if (IsDonggukName(name)) {
        return;
    }
    const bool regular = BandWatchIsRegular(g_band, chip);
    Gdiplus::SolidBrush mark(Argb(regular ? kWatchRegularMarkArgb : kWatchOnceMarkArgb));
    const int bar_h = regular ? 3 : 2;
    g.FillRectangle(&mark, rect.X + 1, rect.Y + 1, rect.Width - 2, bar_h);
}

std::string ChipLabelUtf8(const std::string& name) {
    return DisplayLabelFor(name, g_displayLabels);
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
    SortNoticeKeysOldestFirst(ordered);
    g_chart.notices.clear();
    g_chart.open_dates.clear();
    for (const auto& [notice, open_date] : ordered) {
        g_chart.notices.push_back(BuildScatterNotice(g_chart.rows, notice, g_chart.min_rate, g_chart.max_rate));
        g_chart.open_dates.push_back(open_date);
    }
    bool keep = false;
    for (const auto& scatter : g_chart.notices) {
        if (scatter.notice_number == g_bandNotice) {
            keep = true;
            break;
        }
    }
    if (!keep && !g_chart.notices.empty()) {
        g_bandNotice = g_chart.notices.back().notice_number;
    }
    RefreshBand();
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

void RefreshBand() {
    BandInput in;
    in.notice_id = g_bandNotice;
    if (in.notice_id.empty() && !g_chart.notices.empty()) {
        in.notice_id = g_chart.notices.back().notice_number;
        g_bandNotice = in.notice_id;
    }
    for (size_t i = 0; i < g_chart.notices.size(); ++i) {
        if (g_chart.notices[i].notice_number == in.notice_id) {
            in.open_at = i < g_chart.open_dates.size() ? g_chart.open_dates[i] : std::string{};
            break;
        }
    }
    if (in.open_at.empty()) {
        in.open_at = IsoToday();
    }
    in.region = g_region_token;
    in.today = IsoToday();
    bool any_on = false;
    for (const auto& item : g_companyItems) {
        if (item.checked) {
            any_on = true;
            in.checked_companies.push_back(item.key);
        }
    }
    if (!any_on) {
        in.checked_companies.clear();
    }
    in.year_rows = g_chart.full_rows;
    g_band = g_bandCache.Get(in);
    g_prepareVersion += 1;
    g_hasStatic = false;
}

int RateToX(double rate, int plotL, int scroll_x, int cell) {
    return plotL + ToColumn(rate) * cell - scroll_x;
}

Gdiplus::Color CompanyColor(const std::string& name);

void FillBand(Gdiplus::Graphics& g, double lo, double hi, uint32_t argb, int plotL, int plotT, int plotW, int plotH,
              int scroll_x, int cell) {
    if (hi < lo) {
        return;
    }
    if (!(hi > lo)) {
        hi = lo + 0.01;
    }
    const int x0 = RateToX(lo, plotL, scroll_x, cell);
    const int x1 = RateToX(hi, plotL, scroll_x, cell);
    const int left = (std::max)(plotL, (std::min)(x0, x1));
    const int right = (std::min)(plotL + plotW, (std::max)(x0, x1));
    if (right - left < 1) {
        return;
    }
    Gdiplus::SolidBrush brush(Argb(argb));
    g.FillRectangle(&brush, left, plotT, right - left, plotH);
}

uint32_t ChipFillArgb(const std::string& name, const ParticipantRow& chip) {
    (void)chip;
    if (IsDonggukName(name)) {
        return kDonggukArgb;
    }
    return CompanyColor(name).GetValue();
}

void PaintBandLayer(Gdiplus::Graphics& g, int plotL, int plotT, int plotW, int plotH, int scroll_x) {
    const int cell = CellSize(g_chart.view.scale);
    g.SetClip(Gdiplus::Rect(plotL, plotT, plotW, plotH));
    if (kBandPaintTerritory && g_band.has_dongguk_stats) {
        FillBand(g, g_band.dongguk_q1, g_band.dongguk_q3, kBandTerritoryArgb, plotL, plotT, plotW, plotH, scroll_x,
                 cell);
    }
    if (kBandPaintThreat && g_band.has_threat && g_band.has_dongguk_stats) {
        FillBand(g, g_band.dongguk_q1 - 0.20, g_band.dongguk_q1, kBandThreatFillArgb, plotL, plotT, plotW, plotH,
                 scroll_x, cell);
    }
    if (kBandPaintUseStages && g_band.has_use) {
        for (int i = 0; i < kBandUseStageCount; ++i) {
            double lo = 0;
            double hi = 0;
            BandUseStageInterval(g_band.use_lo, g_band.use_hi, i, lo, hi);
            FillBand(g, lo, hi, BandUseStageArgbAt(i), plotL, plotT, plotW, plotH, scroll_x, cell);
        }
    }
    if (kBandPaintWatch && g_band.has_watch) {
        FillBand(g, g_band.watch_lo, g_band.home_lo, kBandWatchFillArgb, plotL, plotT, plotW, plotH, scroll_x, cell);
    }
    if (kBandPaintHome && g_band.has_watch) {
        FillBand(g, g_band.home_lo, g_band.home_hi, kBandHomeArgb, plotL, plotT, plotW, plotH, scroll_x, cell);
    }
    if (kBandPaintMedian && g_band.has_six_month_median) {
        const int x = RateToX(g_band.six_month_median, plotL, scroll_x, cell);
        Gdiplus::Pen dash(Argb(kBandMedianArgb), kBandMedianWidth);
        dash.SetDashStyle(Gdiplus::DashStyleDash);
        g.DrawLine(&dash, x, plotT, x, plotT + plotH);
    }
}

void PaintSitPulse(Gdiplus::Graphics&) {
    if (!kBandPaintSit || !g_band.has_sit || !ChartShowsScatter(g_chartTab)) {
        return;
    }
    const int cell = CellSize(g_chart.view.scale);
    const int plotL = g_chrome.plot.x;
    const int plotT = g_chrome.plot.y;
    const int plotH = g_chrome.plot.h;
    const int x0 = RateToX(g_band.sit_rate, plotL, g_chart.view.scroll_x, cell);
    const int x1 = RateToX(g_band.sit_rate + 0.01, plotL, g_chart.view.scroll_x, cell);
    int left = (std::min)(x0, x1);
    int right = (std::max)(x0, x1);
    if (right - left < 3) {
        right = left + 3;
    }
    const unsigned elapsed = GetTickCount() - g_bounceOrigin;
    for (int layer = 0; layer < kSitGlowLayerCount; ++layer) {
        const float pad = SitGlowPadDip(layer);
        nara::dwrite_text::QueueGlow(static_cast<float>(left) - pad, static_cast<float>(plotT) - pad * 0.15f,
                                     static_cast<float>(right - left) + pad * 2.0f,
                                     static_cast<float>(plotH) + pad * 0.3f, SitGlowLayerArgb(kBandSitArgb, layer, elapsed),
                                     6.0f + pad * 0.35f);
    }
}

void PaintBandLegend(Gdiplus::Graphics& g) {
    const auto box = BandLegendRect(g_chrome);
    if (box.w < 160 || box.h < 20) {
        return;
    }
    std::string line = FormatBandTerritoryRange(g_band);
    const auto sit = FormatWatchSit(g_band);
    if (!sit.empty()) {
        if (!line.empty()) {
            line += "  ";
        }
        line += sit;
    }
    if (line.empty()) {
        return;
    }
    FillRoundRect(g, ToRectF(box), static_cast<float>(kChartTabRadius), kBandLegendFillArgb, kBandLegendStrokeArgb,
                  1.0f);
    int x = box.x + kBandLegendPadX;
    const int swatch_y = box.y + (box.h - kBandLegendSwatchH) / 2;
    const uint32_t keys[] = {kBandTerritoryArgb, kBandHomeArgb, kBandWatchFillArgb, kBandSitArgb,
                             kBandThreatChipArgb};
    Gdiplus::SolidBrush paper(Argb(kPlotBgArgb));
    Gdiplus::Pen edge(Argb(kBandLegendStrokeArgb), 1.0f);
    for (uint32_t argb : keys) {
        const Gdiplus::Rect sw(x, swatch_y, kBandLegendSwatchW, kBandLegendSwatchH);
        g.FillRectangle(&paper, sw);
        Gdiplus::SolidBrush swatch(Argb(argb));
        g.FillRectangle(&swatch, sw);
        g.DrawRectangle(&edge, sw);
        x += kBandLegendSwatchW + kBandLegendSwatchGap + 2;
    }
    x += kBandLegendTextGap;
    const int text_w = box.Right() - kBandLegendPadX - x;
    if (text_w < 40) {
        return;
    }
    const auto text = Utf8ToWide(line);
    nara::dwrite_text::QueueBox(static_cast<float>(x), static_cast<float>(box.y), static_cast<float>(text_w),
                                static_cast<float>(box.h), text.c_str(), kFontCaption, kTextArgb,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, false, false, false);
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
        SyncWorld(g_chartHwnd);
        g_chart.view = ShowLatestView(g_chart.view);
        UpdateScroll(g_chartHwnd);
        InvalidateRect(g_chartHwnd, nullptr, FALSE);
    }
}

void EnsureUiFont() {
    if (g_uiFont) {
        return;
    }
    g_uiFont = CreateFontW(-ToPx(13), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS,
                           CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, VARIABLE_PITCH | FF_DONTCARE, kFontFamily);
}

void ApplyUiFont(HWND hwnd) {
    if (hwnd && g_uiFont) {
        SendMessageW(hwnd, WM_SETFONT, reinterpret_cast<WPARAM>(g_uiFont), TRUE);
    }
}

void LayoutChartDock(HWND hwnd) {
    SyncDpi(hwnd);
    RECT rc{};
    GetClientRect(hwnd, &rc);
    g_chrome = LayoutChartChrome(ToDip(rc.right), ToDip(rc.bottom), static_cast<int>(g_companyItems.size()));
    const int combo_drop = ToPx(220);
    auto place = [&](HWND combo, const RectI& box) {
        if (!combo) {
            return;
        }
        MoveWindow(combo, ToPx(box.x), ToPx(box.y), ToPx(box.w), combo_drop, TRUE);
        StyleCombo(combo, ToPx(box.w), ToPx(kComboH));
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
    RebuildCompanyItems();
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

void SyncWorld(HWND hwnd) {
    SyncDpi(hwnd);
    RECT client{};
    GetClientRect(hwnd, &client);
    g_chrome = LayoutChartChrome(ToDip(client.right), ToDip(client.bottom),
                                 static_cast<int>(g_companyItems.size()));
    const int cell = CellSize(g_chart.view.scale);
    g_chart.view.plot_w = g_chrome.plot.w;
    g_chart.view.plot_h = g_chrome.plot.h;
    g_chart.view.world_w = ColumnCount() * cell;
    g_chart.view.world_h = ScatterWorldHeight(static_cast<int>(g_chart.notices.size()), cell);
    g_chart.view = ClampView(g_chart.view);
}

void ApplyCrispGraphics(Gdiplus::Graphics& g) {
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.SetPixelOffsetMode(Gdiplus::PixelOffsetModeNone);
    g.SetCompositingQuality(Gdiplus::CompositingQualityAssumeLinear);
    g.SetInterpolationMode(Gdiplus::InterpolationModeNearestNeighbor);
    g.SetTextRenderingHint(static_cast<Gdiplus::TextRenderingHint>(kTextRenderingHintIdeal));
}

bool MapRank1(const ParticipantRow& chip, const ScatterNotice& scatter) {
    return nara::IsMappedRank1(chip, scatter.chips);
}

void PlaceChip(int col, int row_y, int index, int count, int cell, int plotL, int scroll_x, int& x, int& yy,
               int& side) {
    const auto dodge = LayoutChipDodge(index, count, cell);
    side = dodge.side;
    x = plotL + (col * cell) - scroll_x + dodge.dx;
    yy = row_y + dodge.dy;
}

void DrawChipLabel(Gdiplus::Graphics&, const Gdiplus::Rect& rect, const std::wstring& label,
                   const Gdiplus::FontFamily&, int cell) {
    if (label.empty() || !ChipDodgeDrawsLabel(cell) || rect.Width < 6 || rect.Height < 6) {
        return;
    }
    const int chars = (std::max)(1, static_cast<int>(label.size()));
    nara::dwrite_text::QueueBox(static_cast<float>(rect.X), static_cast<float>(rect.Y),
                                static_cast<float>(rect.Width), static_cast<float>(rect.Height), label.c_str(),
                                ChipLabelScreenPx(cell, chars), 0xFFFFFFFFu, nara::dwrite_text::Align::Center,
                                nara::dwrite_text::Align::Center, true, false, true, kChipLabelOutlineArgb,
                                ChipLabelOutlineWidthPx(cell), true);
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

void ReleasePlotCache() {
    if (g_plotCacheDc) {
        SelectObject(g_plotCacheDc, g_plotCacheOld);
        DeleteDC(g_plotCacheDc);
        g_plotCacheDc = nullptr;
        g_plotCacheOld = nullptr;
    }
    if (g_plotCacheBmp) {
        DeleteObject(g_plotCacheBmp);
        g_plotCacheBmp = nullptr;
    }
    g_plotCachePxW = 0;
    g_plotCachePxH = 0;
    g_hasPlotCache = false;
}

void ReleaseLayer() {
    ReleasePlotCache();
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

ScatterWorldCacheKey MakeWorldKey() {
    ScatterWorldCacheKey key;
    key.prepare = g_prepareVersion;
    key.cols = ColumnCount();
    key.rows = static_cast<int>(g_chart.notices.size());
    key.cell_milli = CellSize(g_chart.view.scale) * 1000;
    key.dpi_milli = static_cast<int>(std::lround(g_dpi_scale * 1000.0f));
    key.filter_hash = FilterHash();
    return key;
}

void PaintScatterWorld(Gdiplus::Graphics& g, int plotL, int plotT, int plotW, int plotH, int scroll_x, int scroll_y) {
    const int cell = CellSize(g_chart.view.scale);
    const int cols = ColumnCount();
    const int rows = static_cast<int>(g_chart.notices.size());
    Gdiplus::SolidBrush white(Argb(kPlotBgArgb));
    g.FillRectangle(&white, static_cast<Gdiplus::REAL>(plotL), static_cast<Gdiplus::REAL>(plotT),
                    static_cast<Gdiplus::REAL>(plotW), static_cast<Gdiplus::REAL>(plotH));
    Gdiplus::Pen grid(Argb(kGridArgb), kGridWidth);
    Gdiplus::Pen major(Argb(kMajorGridArgb), kGridWidth);
    g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
    g.SetClip(Gdiplus::Rect(plotL, plotT, plotW, plotH));
    for (int col = 0; col <= cols; ++col) {
        const int x = plotL + (col * cell) - scroll_x;
        if (x < plotL - cell || x > plotL + plotW + cell) {
            continue;
        }
        g.DrawLine(col % 10 == 0 ? &major : &grid, x, plotT, x, plotT + plotH);
    }
    const int grid_rows = rows + kBottomPadCells;
    for (int r = 0; r <= grid_rows; ++r) {
        const int y = plotT + (r * cell) - scroll_y;
        if (y < plotT - cell || y > plotT + plotH + cell) {
            continue;
        }
        g.DrawLine(&grid, plotL, y, plotL + plotW, y);
    }
    PaintBandLayer(g, plotL, plotT, plotW, plotH, scroll_x);
    Gdiplus::FontFamily family(kFontFamily);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    for (int r = 0; r < rows; ++r) {
        const int y = plotT + (r * cell) - scroll_y;
        if (y + cell < plotT || y > plotT + plotH) {
            continue;
        }
        const auto& scatter = g_chart.notices[r];
        std::map<int, std::vector<const ParticipantRow*>> buckets;
        for (const auto& chip : scatter.chips) {
            if (!chip.adjust_rate) {
                continue;
            }
            const auto chip_name = RowCompanyName(chip);
            if (!CompanyVisible(chip_name) || !ChipPassesRank1Filter(g_rank1Only, MapRank1(chip, scatter))) {
                continue;
            }
            buckets[ToColumn(*chip.adjust_rate)].push_back(&chip);
        }
        for (auto& [col, bucket] : buckets) {
            std::sort(bucket.begin(), bucket.end(), [](const ParticipantRow* a, const ParticipantRow* b) {
                const auto name_a = RowCompanyName(*a);
                const auto name_b = RowCompanyName(*b);
                return ChipPaintBefore(IsDonggukName(name_a), a->rank == 1, a->rank, IsDonggukName(name_b),
                                       b->rank == 1, b->rank);
            });
            for (int i = 0; i < static_cast<int>(bucket.size()); ++i) {
                const auto& chip = *bucket[i];
                int side = 2;
                int x = 0;
                int yy = 0;
                PlaceChip(col, y, i, static_cast<int>(bucket.size()), cell, plotL, scroll_x, x, yy, side);
                if (x + side < plotL || x > plotL + plotW || yy + side < plotT || yy > plotT + plotH) {
                    continue;
                }
                const auto color_name = RowCompanyName(chip);
                Gdiplus::Rect rect(x, yy, side, side);
                Gdiplus::RectF box(static_cast<Gdiplus::REAL>(x), static_cast<Gdiplus::REAL>(yy),
                                   static_cast<Gdiplus::REAL>(side), static_cast<Gdiplus::REAL>(side));
                const bool rank1 = MapRank1(chip, scatter);
                PaintChipBody(g, box, cell, ChipFillArgb(color_name, chip), rank1);
                PaintWatchChipMark(g, rect, chip);
                if (rank1) {
                    const int bw = (std::max)(6, static_cast<int>(side * 0.42));
                    const int bh = (std::max)(6, static_cast<int>(side * 0.38));
                    Gdiplus::RectF badge(static_cast<Gdiplus::REAL>(rect.X + side - bw),
                                         static_cast<Gdiplus::REAL>(rect.Y + 1), static_cast<Gdiplus::REAL>(bw),
                                         static_cast<Gdiplus::REAL>(bh));
                    FillRoundRect(g, badge, static_cast<float>(kRankBadgeRadius), kRankBadgeArgb, 0, 0);
                }
                DrawChipLabel(g, rect, Utf8ToWide(ChipLabelUtf8(color_name)), family, side);
            }
        }
    }
    g.ResetClip();
}

bool BuildPlotCache() {
    if (!CanBlitPlotCacheDuringPan()) {
        return false;
    }
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    if (plotW < 8 || plotH < 8) {
        return false;
    }
    const int axis_w = (std::max)(8, g_chrome.plot.x - g_chrome.map_inner.x);
    int over_x = PlotCacheOverscanDip(plotW);
    int over_y = PlotCacheOverscanDip(plotH);
    int cache_w = axis_w + plotW + 2 * over_x;
    int cache_h = plotH + 2 * over_y;
    const int px_w = ToPx(cache_w);
    const int px_h = ToPx(cache_h);
    if (static_cast<long long>(px_w) * px_h > 40'000'000LL) {
        over_x = plotW / 2;
        over_y = plotH / 2;
        cache_w = axis_w + plotW + 2 * over_x;
        cache_h = plotH + 2 * over_y;
    }
    const int origin_x = g_chart.view.scroll_x - over_x;
    const int origin_y = g_chart.view.scroll_y - over_y;
    const auto key = MakeWorldKey();
    if (g_hasPlotCache && CanReuseWorldLayer(g_plotCacheKey, key) && g_plotCacheOriginX == origin_x
        && g_plotCacheOriginY == origin_y && g_plotCachePxW == ToPx(cache_w) && g_plotCachePxH == ToPx(cache_h)) {
        return true;
    }
    ReleasePlotCache();
    g_plotCacheDc = CreateCompatibleDC(g_layerDc ? g_layerDc : nullptr);
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = ToPx(cache_w);
    bmi.bmiHeader.biHeight = -ToPx(cache_h);
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    g_plotCacheBmp = CreateDIBSection(g_plotCacheDc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!g_plotCacheBmp) {
        DeleteDC(g_plotCacheDc);
        g_plotCacheDc = nullptr;
        return false;
    }
    g_plotCacheOld = SelectObject(g_plotCacheDc, g_plotCacheBmp);
    g_plotCachePxW = ToPx(cache_w);
    g_plotCachePxH = ToPx(cache_h);
    {
        Gdiplus::Graphics g(g_plotCacheDc);
        ApplyCrispGraphics(g);
        ApplyDpiTransform(g);
        g.Clear(Argb(kBgArgb));
        const int scatter_l = axis_w;
        const int scatter_w = plotW + 2 * over_x;
        const int scatter_h = plotH + 2 * over_y;
        nara::dwrite_text::ResetQueue();
        nara::dwrite_text::SetPlotClip(static_cast<float>(scatter_l), 0.0f, static_cast<float>(scatter_w),
                                       static_cast<float>(scatter_h));
        PaintScatterWorld(g, scatter_l, 0, scatter_w, scatter_h, origin_x, origin_y);
        const int cell = CellSize(g_chart.view.scale);
        const int rows = static_cast<int>(g_chart.notices.size());
        for (int r = 0; r < rows; ++r) {
            const int y = (r * cell) - origin_y;
            if (y < 0 || y + cell > scatter_h) {
                continue;
            }
            const auto ytext = Utf8ToWide(FormatNoticeYLabel(g_chart.notices[r].notice_number,
                                                            r < static_cast<int>(g_chart.open_dates.size())
                                                                ? g_chart.open_dates[r]
                                                                : ""));
            nara::dwrite_text::QueueBox(0.0f, static_cast<float>(y), static_cast<float>(axis_w - 4),
                                        static_cast<float>(cell), ytext.c_str(), kFontAxis, kSubtleTextArgb,
                                        nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, false, true,
                                        true);
        }
        g.Flush();
        nara::dwrite_text::Flush(g_plotCacheDc, g_plotCachePxW, g_plotCachePxH, g_dpi_scale);
    }
    g_plotCacheOriginX = origin_x;
    g_plotCacheOriginY = origin_y;
    g_plotCacheOverscanX = over_x;
    g_plotCacheOverscanY = over_y;
    g_plotCacheAxisW = axis_w;
    g_plotCachePlotW = plotW;
    g_plotCachePlotH = plotH;
    g_plotCacheKey = key;
    g_hasPlotCache = true;
    return true;
}

bool TryPresentPan(HDC hdc, int pixel_w, int pixel_h) {
    if (!g_chart.panning || !CanBlitPlotCacheDuringPan() || !g_hasStatic || !g_layerDc) {
        return false;
    }
    const int sx = g_chart.view.scroll_x;
    const int sy = g_chart.view.scroll_y;
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    const int scatter_w = plotW + 2 * g_plotCacheOverscanX;
    const int scatter_h = plotH + 2 * g_plotCacheOverscanY;
    if (!g_hasPlotCache || !CanReuseWorldLayer(g_plotCacheKey, MakeWorldKey())
        || !PlotCacheCoversView(g_plotCacheOriginX, scatter_w, sx, plotW)
        || !PlotCacheCoversView(g_plotCacheOriginY, scatter_h, sy, plotH)) {
        if (!BuildPlotCache()) {
            return false;
        }
    }
    EnsureFrame(hdc, pixel_w, pixel_h);
    BitBlt(g_frameDc, 0, 0, pixel_w, pixel_h, g_layerDc, 0, 0, SRCCOPY);
    const int dsx = sx - g_plotCacheOriginX;
    const int dsy = sy - g_plotCacheOriginY;
    const int src_plot_x = ToPx(g_plotCacheAxisW + dsx);
    const int src_plot_y = ToPx(dsy);
    const int src_axis_x = ToPx(0);
    const int dest_axis_x = ToPx(g_chrome.map_inner.x);
    const int dest_plot_x = ToPx(g_chrome.plot.x);
    const int dest_plot_y = ToPx(g_chrome.plot.y);
    const int dest_plot_w = ToPx(plotW);
    const int dest_plot_h = ToPx(plotH);
    const int dest_axis_w = ToPx(g_plotCacheAxisW);
    BitBlt(g_frameDc, dest_axis_x, dest_plot_y, dest_axis_w, dest_plot_h, g_plotCacheDc, src_axis_x, src_plot_y,
           SRCCOPY);
    BitBlt(g_frameDc, dest_plot_x, dest_plot_y, dest_plot_w, dest_plot_h, g_plotCacheDc, src_plot_x, src_plot_y,
           SRCCOPY);
    {
        Gdiplus::Graphics overlay(g_frameDc);
        ApplyCrispGraphics(overlay);
        ApplyDpiTransform(overlay);
        const Gdiplus::RectF toolbar = ToRectF(g_chrome.reset_toolbar);
        const Gdiplus::RectF reset = ToRectF(g_chrome.reset_btn);
        const Gdiplus::RectF rank1 = ToRectF(g_chrome.rank1_btn);
        DrawOffsetShadow(overlay, toolbar, static_cast<float>(kToolbarRadius), kToolbarShadowDepth, kToolbarShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, toolbar, static_cast<float>(kToolbarRadius), kGlassFillArgb, kStrokeSubtleArgb, 1.0f);
        DrawOffsetShadow(overlay, reset, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, reset, static_cast<float>(kResetBtnRadius), kGlassFillArgb,
                      g_hotReset ? kAccentArgb : kStrokeStrongArgb, 1.0f);
        nara::dwrite_text::ResetQueue();
        nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.plot.x), static_cast<float>(g_chrome.plot.y),
                                       static_cast<float>(g_chrome.plot.w), static_cast<float>(g_chrome.plot.h));
        FillGlyphs(overlay, kResetLabel, Gdiplus::FontFamily(kFontFamily), Gdiplus::FontStyleBold, kFontReset, reset,
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter, kTextArgb);
        const uint32_t rank1_fill = g_rank1Only ? chart_theme::kAccentArgb : kGlassFillArgb;
        const uint32_t rank1_text = g_rank1Only ? 0xFFFFFFFFu : kTextArgb;
        DrawOffsetShadow(overlay, rank1, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, rank1, static_cast<float>(kResetBtnRadius), rank1_fill,
                      (g_hotRank1 || g_rank1Only) ? kAccentArgb : kStrokeStrongArgb, 1.0f);
        FillGlyphs(overlay, kRank1Label, Gdiplus::FontFamily(kFontFamily), Gdiplus::FontStyleBold, kFontReset, rank1,
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter, rank1_text);
        PaintDesignPill(overlay, Gdiplus::FontFamily(kFontFamily));
        if (DonggukBounceEnabled()) {
            PaintDonggukChips(overlay);
        }
        PaintSitPulse(overlay);
        PaintHoverOverlay(overlay);
    }
    nara::dwrite_text::Flush(g_frameDc, pixel_w, pixel_h, g_dpi_scale);
    BitBlt(hdc, 0, 0, pixel_w, pixel_h, g_frameDc, 0, 0, SRCCOPY);
    return true;
}

bool TryPresentZoom(HDC hdc, int pixel_w, int pixel_h) {
    if (!g_zooming || !CanStretchPlotCacheDuringZoom() || !g_layerDc) {
        return false;
    }
    const int new_cell = CellSize(g_chart.view.scale);
    int old_cell = 0;
    int origin_x = 0;
    int origin_y = 0;
    int axis_w = g_chrome.plot.x - g_chrome.map_inner.x;
    HDC src = nullptr;
    bool from_cache = false;
    if (g_hasPlotCache && g_plotCacheDc && g_plotCacheKey.cell_milli > 0) {
        old_cell = g_plotCacheKey.cell_milli / 1000;
        origin_x = g_plotCacheOriginX;
        origin_y = g_plotCacheOriginY;
        axis_w = g_plotCacheAxisW;
        src = g_plotCacheDc;
        from_cache = true;
    } else if (g_hasStatic && g_staticCell > 0) {
        old_cell = g_staticCell;
        origin_x = g_staticScrollX;
        origin_y = g_staticScrollY;
        src = g_layerDc;
    }
    if (src == nullptr || old_cell <= 0 || new_cell <= 0) {
        return false;
    }
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    const int sx = g_chart.view.scroll_x;
    const int sy = g_chart.view.scroll_y;
    int src_plot_x = 0;
    int src_plot_y = 0;
    int src_axis_x = 0;
    if (from_cache) {
        src_plot_x = ToPx(ZoomSrcOrigin(axis_w, origin_x, sx, old_cell, new_cell));
        src_plot_y = ToPx(ZoomSrcOrigin(0, origin_y, sy, old_cell, new_cell));
        src_axis_x = ToPx(0);
    } else {
        src_plot_x = ToPx(ZoomSrcOrigin(g_chrome.plot.x, origin_x, sx, old_cell, new_cell));
        src_plot_y = ToPx(ZoomSrcOrigin(g_chrome.plot.y, origin_y, sy, old_cell, new_cell));
        src_axis_x = ToPx(g_chrome.map_inner.x);
    }
    const int src_plot_w = ToPx(ZoomSrcSpan(plotW, old_cell, new_cell));
    const int src_plot_h = ToPx(ZoomSrcSpan(plotH, old_cell, new_cell));
    const int src_axis_w = ToPx(axis_w);
    const int dest_axis_x = ToPx(g_chrome.map_inner.x);
    const int dest_plot_x = ToPx(g_chrome.plot.x);
    const int dest_plot_y = ToPx(g_chrome.plot.y);
    const int dest_plot_w = ToPx(plotW);
    const int dest_plot_h = ToPx(plotH);
    EnsureFrame(hdc, pixel_w, pixel_h);
    BitBlt(g_frameDc, 0, 0, pixel_w, pixel_h, g_layerDc, 0, 0, SRCCOPY);
    {
        Gdiplus::Graphics g(g_frameDc);
        ApplyCrispGraphics(g);
        ApplyDpiTransform(g);
        Gdiplus::SolidBrush bg(Argb(kBgArgb));
        Gdiplus::SolidBrush plot(Argb(kPlotBgArgb));
        g.FillRectangle(&bg, ToRectF(g_chrome.map_inner));
        g.FillRectangle(&plot, ToRectF(g_chrome.plot));
        g.Flush();
    }
    SetStretchBltMode(g_frameDc, COLORONCOLOR);
    StretchBlt(g_frameDc, dest_axis_x, dest_plot_y, src_axis_w, dest_plot_h, src, src_axis_x, src_plot_y, src_axis_w,
               src_plot_h, SRCCOPY);
    StretchBlt(g_frameDc, dest_plot_x, dest_plot_y, dest_plot_w, dest_plot_h, src, src_plot_x, src_plot_y, src_plot_w,
               src_plot_h, SRCCOPY);
    {
        Gdiplus::Graphics overlay(g_frameDc);
        ApplyCrispGraphics(overlay);
        ApplyDpiTransform(overlay);
        const Gdiplus::RectF toolbar = ToRectF(g_chrome.reset_toolbar);
        const Gdiplus::RectF reset = ToRectF(g_chrome.reset_btn);
        const Gdiplus::RectF rank1 = ToRectF(g_chrome.rank1_btn);
        DrawOffsetShadow(overlay, toolbar, static_cast<float>(kToolbarRadius), kToolbarShadowDepth, kToolbarShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, toolbar, static_cast<float>(kToolbarRadius), kGlassFillArgb, kStrokeSubtleArgb, 1.0f);
        DrawOffsetShadow(overlay, reset, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, reset, static_cast<float>(kResetBtnRadius), kGlassFillArgb,
                      g_hotReset ? kAccentArgb : kStrokeStrongArgb, 1.0f);
        nara::dwrite_text::ResetQueue();
        FillGlyphs(overlay, kResetLabel, Gdiplus::FontFamily(kFontFamily), Gdiplus::FontStyleBold, kFontReset, reset,
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter, kTextArgb);
        const uint32_t rank1_fill = g_rank1Only ? chart_theme::kAccentArgb : kGlassFillArgb;
        const uint32_t rank1_text = g_rank1Only ? 0xFFFFFFFFu : kTextArgb;
        DrawOffsetShadow(overlay, rank1, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                         kResetShadowArgb);
        FillRoundRect(overlay, rank1, static_cast<float>(kResetBtnRadius), rank1_fill,
                      (g_hotRank1 || g_rank1Only) ? kAccentArgb : kStrokeStrongArgb, 1.0f);
        FillGlyphs(overlay, kRank1Label, Gdiplus::FontFamily(kFontFamily), Gdiplus::FontStyleBold, kFontReset, rank1,
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter, rank1_text);
        PaintDesignPill(overlay, Gdiplus::FontFamily(kFontFamily));
        PaintChartTabs(overlay, Gdiplus::FontFamily(kFontFamily));
        PaintSitPulse(overlay);
    }
    nara::dwrite_text::Flush(g_frameDc, pixel_w, pixel_h, g_dpi_scale);
    BitBlt(hdc, 0, 0, pixel_w, pixel_h, g_frameDc, 0, 0, SRCCOPY);
    return true;
}

void PresentChart(HDC hdc, int w, int h) {
    EnsureFrame(hdc, w, h);
    BitBlt(g_frameDc, 0, 0, w, h, g_layerDc, 0, 0, SRCCOPY);
    {
        Gdiplus::Graphics overlay(g_frameDc);
        ApplyCrispGraphics(overlay);
        ApplyDpiTransform(overlay);
        nara::dwrite_text::ResetQueue();
        if (ChartPageUsesFullCard(g_chartTab)) {
            nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.hist_plot.x),
                                           static_cast<float>(g_chrome.hist_plot.y),
                                           static_cast<float>(g_chrome.hist_plot.w),
                                           static_cast<float>(g_chrome.hist_plot.h));
        } else {
            nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.plot.x), static_cast<float>(g_chrome.plot.y),
                                           static_cast<float>(g_chrome.plot.w), static_cast<float>(g_chrome.plot.h));
        }
        if (ChartShowsScatter(g_chartTab)) {
            if (DonggukBounceEnabled()) {
                PaintDonggukChips(overlay);
            }
            PaintSitPulse(overlay);
            PaintHoverOverlay(overlay);
        } else if (ChartShowsHistogram(g_chartTab)) {
            PaintHistogramHover(overlay);
        }
    }
    nara::dwrite_text::Flush(g_frameDc, w, h, g_dpi_scale);
    BitBlt(hdc, 0, 0, w, h, g_frameDc, 0, 0, SRCCOPY);
}

int FilterHash() {
    int hash = static_cast<int>(std::lround(g_dpi_scale * 100.0f));
    hash = hash * 131 + g_chartTab + 3;
    for (unsigned char ch : g_bandNotice) {
        hash = hash * 31 + static_cast<int>(ch);
    }
    if (g_rank1Only) {
        hash = hash * 131 + 17;
    }
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
    key.chrome_hot = ChromeHot(g_hotCompany, g_hotReset, g_hotAll, g_hotRank1, g_hotTab);
    return key;
}

void PaintDonggukChips(Gdiplus::Graphics& g) {
    if (!DonggukBounceEnabled()) {
        return;
    }
    const int cell = CellSize(g_chart.view.scale);
    const int plotL = g_chrome.plot.x;
    const int plotT = g_chrome.plot.y;
    const int plotW = g_chrome.plot.w;
    const int plotH = g_chrome.plot.h;
    const int rows = static_cast<int>(g_chart.notices.size());
    const unsigned elapsed = GetTickCount() - g_bounceOrigin;
    const float scale = DonggukBounceScale(elapsed);
    const float lift = DonggukBounceLiftPx(elapsed);
    Gdiplus::FontFamily family(kFontFamily);
    ClipPlot(g);
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
            const auto chip_name = RowCompanyName(chip);
            if (!IsDonggukName(chip_name) || !CompanyVisible(chip_name)
                || !ChipPassesRank1Filter(g_rank1Only, MapRank1(chip, scatter))) {
                continue;
            }
            buckets[ToColumn(*chip.adjust_rate)].push_back(&chip);
        }
        for (auto& [col, bucket] : buckets) {
            for (int i = 0; i < static_cast<int>(bucket.size()); ++i) {
                const auto& chip = *bucket[i];
                int side = 2;
                int x = 0;
                int yy = 0;
                PlaceChip(col, y, i, static_cast<int>(bucket.size()), cell, plotL, g_chart.view.scroll_x, x, yy, side);
                if (x + side < plotL || x > plotL + plotW || yy + side < plotT || yy > plotT + plotH) {
                    continue;
                }
                const float cx = static_cast<float>(x) + static_cast<float>(side) * 0.5f;
                const float cy = static_cast<float>(yy) + static_cast<float>(side) * 0.5f;
                const float ns = static_cast<float>(side) * scale;
                Gdiplus::RectF box(cx - ns * 0.5f, cy - ns * 0.5f + lift, ns, ns);
                Gdiplus::Rect rect(static_cast<INT>(std::floor(box.X)), static_cast<INT>(std::floor(box.Y)),
                                   static_cast<INT>(std::ceil(box.Width)), static_cast<INT>(std::ceil(box.Height)));
                const auto color_name =
                    RowCompanyName(chip);
                PaintChipBody(g, box, cell, ChipFillArgb(color_name, chip),
                              MapRank1(chip, scatter));
                PaintWatchChipMark(g, rect, chip);
                if (!BounceOverlaySkipsLabels() && (!SkipChipLabelsDuringPan() || !g_chart.panning)) {
                    DrawChipLabel(g, rect, Utf8ToWide(ChipLabelUtf8(color_name)), family, side);
                }
                ClipPlot(g);
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
    Gdiplus::Pen pointer(Argb(kPointerArgb), kPointerWidth);
    const Gdiplus::REAL dashes[] = {4.0f, 3.0f};
    pointer.SetDashPattern(dashes, 2);
    ClipPlot(g);
    g.DrawLine(&pointer, g_chart.hover_x, plotT + 1, g_chart.hover_x, plotT + plotH - 1);
    g.DrawLine(&pointer, plotL + 1, g_chart.hover_y, plotL + plotW - 1, g_chart.hover_y);
    wchar_t rate_text[48];
    if (g_chart.step <= 0.0015) {
        swprintf_s(rate_text, L"기초대비사정률 %.3f", g_chart.hover_rate);
    } else {
        swprintf_s(rate_text, L"기초대비사정률 %.2f", g_chart.hover_rate);
    }
    std::wstring hover = rate_text;
    const int cell = CellSize(g_chart.view.scale);
    const int col = HoverColumn(g_chart.hover_x - plotL, g_chart.view.scroll_x, g_chart.view.scale);
    const int row = cell > 0 ? (g_chart.hover_y - plotT + g_chart.view.scroll_y) / cell : -1;
    if (row >= 0 && row < static_cast<int>(g_chart.notices.size())) {
        const auto& scatter = g_chart.notices[static_cast<size_t>(row)];
        for (const auto& chip : scatter.chips) {
            if (!chip.adjust_rate || ToColumn(*chip.adjust_rate) != col) {
                continue;
            }
            const auto name = RowCompanyName(chip);
            if (!CompanyVisible(name) || !ChipPassesRank1Filter(g_rank1Only, MapRank1(chip, scatter))) {
                continue;
            }
            hover += L"  ";
            hover += Utf8ToWide(ChipLabelUtf8(name));
        }
    }
    const int extra = static_cast<int>(hover.size()) > 18 ? static_cast<int>(hover.size() - 18) * 10 : 0;
    const int label_w = (std::min)(360, 168 + extra);
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
    FillGlyphs(g, hover.c_str(), family, Gdiplus::FontStyleBold, kFontHover,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>(tx + 8), static_cast<Gdiplus::REAL>(ty + 4),
                              static_cast<Gdiplus::REAL>(label_w - 12), static_cast<Gdiplus::REAL>(label_h - 6)),
               Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentCenter, kPointerLabelTextArgb);
    g.ResetClip();
}

Gdiplus::PointF IsoPt(const IsoPoint& p) { return {p.x, p.y}; }

void FillIsoQuad(Gdiplus::Graphics& g, const IsoPoint* q, uint32_t fill, uint32_t stroke, float stroke_w) {
    const Gdiplus::PointF pts[4] = {IsoPt(q[0]), IsoPt(q[1]), IsoPt(q[2]), IsoPt(q[3])};
    Gdiplus::SolidBrush brush(Argb(fill));
    g.FillPolygon(&brush, pts, 4);
    if (stroke_w > 0.0f) {
        Gdiplus::Pen pen(Argb(stroke), stroke_w);
        g.DrawPolygon(&pen, pts, 4);
    }
}

void PaintIsoBar(Gdiplus::Graphics& g, const HistBarGeom& bar, uint32_t fill) {
    FillIsoQuad(g, bar.side, DarkenArgb(fill, 0.30f), DarkenArgb(fill, 0.42f), 0.7f);
    FillIsoQuad(g, bar.top, LightenArgb(fill, 0.30f), DarkenArgb(fill, 0.08f), 0.7f);
    const Gdiplus::PointF pts[4] = {IsoPt(bar.front[0]), IsoPt(bar.front[1]), IsoPt(bar.front[2]), IsoPt(bar.front[3])};
    Gdiplus::LinearGradientBrush shine(pts[3], pts[0], Argb(LightenArgb(fill, 0.22f)), Argb(DarkenArgb(fill, 0.14f)));
    g.FillPolygon(&shine, pts, 4);
    Gdiplus::Pen edge(Argb(DarkenArgb(fill, 0.22f)), 0.8f);
    g.DrawPolygon(&edge, pts, 4);
}

void EnsureHistogram() {
    int hash = g_prepareVersion;
    hash = hash * 131 + static_cast<int>(std::lround(g_chart.min_rate * 1000.0));
    hash = hash * 131 + static_cast<int>(std::lround(g_chart.max_rate * 1000.0));
    hash = hash * 131 + static_cast<int>(g_chart.notices.size());
    hash = hash * 131 + (g_rank1Only ? 17 : 3);
    for (const auto& item : g_companyItems) {
        hash = hash * 131 + (item.checked ? 1 : 0);
    }
    if (g_histReady && hash == g_histDataHash) {
        return;
    }
    const auto chips = CollectHistogramChips(g_chart.notices, g_rank1Only);
    std::vector<std::string> keys;
    std::vector<char> flags;
    keys.reserve(g_companyItems.size());
    flags.reserve(g_companyItems.size());
    for (const auto& item : g_companyItems) {
        keys.push_back(item.key);
        flags.push_back(item.checked ? 1 : 0);
    }
    HistogramBinSpec spec;
    spec.min_rate = g_chart.min_rate;
    spec.max_rate = g_chart.max_rate;
    spec.bin_count = kHistogramBinCount;
    g_hist = BuildHistogram(chips, spec, SelectedCompanyKeys(keys, flags), kHistogramMaxSeries);
    for (auto& series : g_hist.companies) {
        series.short_label = DisplayLabelFor(series.key, g_displayLabels);
    }
    g_histDataHash = hash;
    g_histReady = true;
}

void PaintChartTabs(Gdiplus::Graphics& g, const Gdiplus::FontFamily& family) {
    auto paint_tab = [&](const RectI& box, int index) {
        const bool on = g_chartTab == index;
        const bool hot = g_hotTab == index;
        const uint32_t fill = on ? kHistTabOnFillArgb : (hot ? kHoverFillArgb : kHistTabOffFillArgb);
        const uint32_t text = on ? kHistTabOnTextArgb : kHistTabOffTextArgb;
        const uint32_t stroke = (on || hot) ? kAccentArgb : kStrokeStrongArgb;
        FillRoundRect(g, ToRectF(box), static_cast<float>(kChartTabRadius), fill, stroke, 1.0f);
        FillGlyphs(g, ChartTabLabelAt(index), family, Gdiplus::FontStyleBold, kFontCaption, ToRectF(box),
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter, text);
    };
    for (int i = 0; i < kChartTabCount; ++i) {
        paint_tab(ChartTabRect(g_chrome, i), i);
    }
}

void PaintHistogramScene(Gdiplus::Graphics& g, const Gdiplus::FontFamily& family, int plotL, int plotT, int plotW,
                         int plotH) {
    EnsureHistogram();
    ClipToRoundRect(g, g_chrome.hist_plot, static_cast<float>(kPlotRadius));
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    const int company_n = static_cast<int>(g_hist.companies.size());
    const int clusters = HistogramVisibleSeriesCount(g_hist);
    const int axis_max = HistogramAxisMax(g_hist);
    g_histScene = LayoutHistScene(static_cast<float>(plotL), static_cast<float>(plotT), static_cast<float>(plotW),
                                  static_cast<float>(plotH), g_hist.spec.bin_count, clusters);
    const int bins = g_hist.spec.bin_count;
    const float floor_w = HistBinX((std::max)(1, bins), g_histScene) - g_histScene.gap;
    const float z_back = g_histScene.bar_d;
    IsoPoint floor[4] = {
        ProjectIso(0.0f, 0.0f, 0.0f, g_histScene),
        ProjectIso(floor_w, 0.0f, 0.0f, g_histScene),
        ProjectIso(floor_w, 0.0f, z_back, g_histScene),
        ProjectIso(0.0f, 0.0f, z_back, g_histScene),
    };
    FillIsoQuad(g, floor, kHistFloorArgb, kHistGridArgb, 1.0f);
    IsoPoint wall[4] = {
        ProjectIso(0.0f, 0.0f, z_back, g_histScene),
        ProjectIso(floor_w, 0.0f, z_back, g_histScene),
        ProjectIso(floor_w, g_histScene.max_h, z_back, g_histScene),
        ProjectIso(0.0f, g_histScene.max_h, z_back, g_histScene),
    };
    FillIsoQuad(g, wall, WithAlpha(kHistGridArgb, 0x55u), kHistGridArgb, 0.8f);

    Gdiplus::Pen grid(Argb(kHistGridArgb), 0.9f);
    for (int i = 0; i <= bins; i += 5) {
        const float x = HistBinX(i, g_histScene);
        const auto a = ProjectIso(x, 0.0f, 0.0f, g_histScene);
        const auto b = ProjectIso(x, 0.0f, z_back, g_histScene);
        g.DrawLine(&grid, a.x, a.y, b.x, b.y);
    }
    for (int t = 0; t <= 4; ++t) {
        const float y = g_histScene.max_h * (static_cast<float>(t) / 4.0f);
        const auto a = ProjectIso(0.0f, y, 0.0f, g_histScene);
        const auto b = ProjectIso(0.0f, y, z_back, g_histScene);
        g.DrawLine(&grid, a.x, a.y, b.x, b.y);
        const int count = (axis_max * t) / 4;
        wchar_t tick[16];
        swprintf_s(tick, L"%d", count);
        nara::dwrite_text::QueueBox(static_cast<float>(plotL) + 4.0f, a.y - 8.0f, 40.0f, 16.0f, tick, kFontAxis,
                                    kSubtleTextArgb, nara::dwrite_text::Align::Far, nara::dwrite_text::Align::Center,
                                    false, false, true, 0, 0, true);
    }

    auto series_fill = [&](const HistogramSeries& series, int count) -> uint32_t {
        if (!series.key.empty()) {
            return series.argb;
        }
        const float t = static_cast<float>(count) / static_cast<float>((std::max)(1, axis_max));
        const uint32_t density = HistDensityArgb(t);
        return g_rank1Only ? LerpArgb(density, kRankGoldArgb, 0.42f) : density;
    };
    auto draw_series = [&](const HistogramSeries& series, int cluster) {
        const float w = HistClusterBarW(g_histScene);
        for (int i = bins - 1; i >= 0; --i) {
            const int count = series.counts[static_cast<size_t>(i)];
            if (count <= 0) {
                continue;
            }
            const float h = HistogramBarHeight(count, axis_max, g_histScene.max_h);
            const auto geom =
                MakeHistBar(HistClusterX(i, cluster, g_histScene), 0.0f, w, g_histScene.bar_d, h, g_histScene);
            PaintIsoBar(g, geom, series_fill(series, count));
            const bool peak = count >= 2
                              && (i == 0 || count >= series.counts[static_cast<size_t>(i - 1)])
                              && (i + 1 >= bins || count >= series.counts[static_cast<size_t>(i + 1)]);
            if (peak && clusters <= 4) {
                wchar_t nlabel[12];
                swprintf_s(nlabel, L"%d", count);
                nara::dwrite_text::QueueBox(geom.top[0].x, geom.top[3].y - 14.0f, (std::max)(16.0f, w + 8.0f), 14.0f,
                                            nlabel, kFontAxis, kTextArgb, nara::dwrite_text::Align::Center,
                                            nara::dwrite_text::Align::Far, true, false, true, 0, 0, true);
            }
        }
    };

    if (HistogramDrawsMarketBars(g_hist)) {
        draw_series(g_hist.market, 0);
    } else {
        for (int c = clusters - 1; c >= 0; --c) {
            draw_series(g_hist.companies[static_cast<size_t>(c)], c);
        }
    }

    const int visible_total = HistogramDrawsMarketBars(g_hist) ? g_hist.market.total : [&] {
        int sum = 0;
        for (const auto& series : g_hist.companies) {
            sum += series.total;
        }
        return sum;
    }();
    if (visible_total == 0) {
        nara::dwrite_text::QueueBox(static_cast<float>(plotL), static_cast<float>(plotT), static_cast<float>(plotW),
                                    static_cast<float>(plotH), kHistEmpty, kFontEmpty, kSubtleTextArgb,
                                    nara::dwrite_text::Align::Center, nara::dwrite_text::Align::Center, true, false,
                                    false, 0, 0, true);
    }

    float legend_x = static_cast<float>(plotL) + 56.0f;
    const float legend_y = static_cast<float>(plotT) + 8.0f;
    auto legend = [&](uint32_t argb, const wchar_t* label) {
        FillRoundRect(g, Gdiplus::RectF(legend_x, legend_y + 4.0f, 12.0f, 12.0f), 3.0f, argb, DarkenArgb(argb, 0.2f), 1.0f);
        nara::dwrite_text::QueueBox(legend_x + 16.0f, legend_y, 72.0f, 20.0f, label, kFontCaption, kTextArgb,
                                    nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, true, true, true, 0,
                                    0, true);
        legend_x += 92.0f;
    };
    if (HistogramDrawsMarketBars(g_hist)) {
        legend(g_rank1Only ? kRankGoldArgb : kHistMarketMidArgb, g_rank1Only ? kHistRank1Label : kHistMarketLabel);
    } else {
        for (const auto& series : g_hist.companies) {
            legend(series.argb, Utf8ToWide(series.short_label).c_str());
        }
    }

    nara::dwrite_text::QueueBox(static_cast<float>(plotL) + 4.0f, static_cast<float>(plotT) + 8.0f, 48.0f, 40.0f,
                                kHistAxisCount, kFontAxis, kSubtleTextArgb, nara::dwrite_text::Align::Near,
                                nara::dwrite_text::Align::Near, true, false, true, 0, 0, true);
    nara::dwrite_text::QueueBox(static_cast<float>(plotL), static_cast<float>(plotT + plotH - 22),
                                static_cast<float>(plotW), 18.0f, kAxisRate, kFontAxis, kSubtleTextArgb,
                                nara::dwrite_text::Align::Center, nara::dwrite_text::Align::Near, true, false, true, 0, 0,
                                true);
    for (int i = 0; i <= bins; i += 10) {
        const float x = HistBinX(i, g_histScene);
        const auto tick = ProjectIso(x + g_histScene.bar_w * 0.5f, 0.0f, 0.0f, g_histScene);
        wchar_t label[16];
        swprintf_s(label, L"%.2f", HistogramBinStart(i, g_hist.spec));
        nara::dwrite_text::QueueBox(tick.x - 22.0f, tick.y + 6.0f, 44.0f, 14.0f, label, kFontAxis, kSubtleTextArgb,
                                    nara::dwrite_text::Align::Center, nara::dwrite_text::Align::Near, false, false, true,
                                    0, 0, true);
    }
}

void PaintHistogramHover(Gdiplus::Graphics& g) {
    if (!ChartShowsHistogram(g_chartTab) || g_histHoverBin < 0 || g_histHoverBin >= g_hist.spec.bin_count) {
        return;
    }
    EnsureHistogram();
    ClipToRoundRect(g, g_chrome.hist_plot, static_cast<float>(kPlotRadius));
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    const int company_n = static_cast<int>(g_hist.companies.size());
    const int axis_max = HistogramAxisMax(g_hist);
    const int bin = g_histHoverBin;
    const float w = HistClusterBarW(g_histScene);
    auto outline = [&](int cluster, int count) {
        if (count <= 0 || axis_max <= 0) {
            return;
        }
        const float h = HistogramBarHeight(count, axis_max, g_histScene.max_h);
        const auto geom =
            MakeHistBar(HistClusterX(bin, cluster, g_histScene), 0.0f, w, g_histScene.bar_d, h, g_histScene);
        Gdiplus::PointF pts[4] = {IsoPt(geom.front[0]), IsoPt(geom.front[1]), IsoPt(geom.front[2]), IsoPt(geom.front[3])};
        Gdiplus::Pen gold(Argb(kRankGoldArgb), 2.0f);
        g.DrawPolygon(&gold, pts, 4);
    };
    wchar_t callout[192];
    std::wstring text;
    if (HistogramDrawsMarketBars(g_hist)) {
        outline(0, g_hist.market.counts[static_cast<size_t>(bin)]);
        swprintf_s(callout, L"%.3f–%.3f  %s %d건", HistogramBinStart(bin, g_hist.spec),
                   HistogramBinEnd(bin, g_hist.spec), g_rank1Only ? L"1위" : L"전체",
                   g_hist.market.counts[static_cast<size_t>(bin)]);
        text = callout;
    } else {
        swprintf_s(callout, L"%.3f–%.3f", HistogramBinStart(bin, g_hist.spec), HistogramBinEnd(bin, g_hist.spec));
        text = callout;
        for (int c = 0; c < company_n; ++c) {
            const int count = g_hist.companies[static_cast<size_t>(c)].counts[static_cast<size_t>(bin)];
            outline(c, count);
            wchar_t line[64];
            swprintf_s(line, L"  %s %d건", Utf8ToWide(g_hist.companies[static_cast<size_t>(c)].short_label).c_str(),
                       count);
            text += line;
        }
    }
    const int label_w = (std::max)(196, 120 + company_n * 56);
    const int label_h = 28;
    int tx = g_chart.hover_x + 12;
    int ty = g_chart.hover_y - label_h - 10;
    const int plotL = g_chrome.hist_plot.x;
    const int plotT = g_chrome.hist_plot.y;
    const int plotW = g_chrome.hist_plot.w;
    const int plotH = g_chrome.hist_plot.h;
    if (tx + label_w > plotL + plotW - 4) {
        tx = g_chart.hover_x - label_w - 12;
    }
    if (ty < plotT + 4) {
        ty = g_chart.hover_y + 10;
    }
    tx = std::clamp(tx, plotL + 4, plotL + plotW - label_w - 4);
    ty = std::clamp(ty, plotT + 4, plotT + plotH - label_h - 4);
    FillRoundRect(g, Gdiplus::RectF(static_cast<Gdiplus::REAL>(tx), static_cast<Gdiplus::REAL>(ty),
                                    static_cast<Gdiplus::REAL>(label_w), static_cast<Gdiplus::REAL>(label_h)),
                  static_cast<float>(kHoverRadius), kPointerLabelBgArgb, kCalloutPenArgb, 1.2f);
    nara::dwrite_text::QueueBox(static_cast<Gdiplus::REAL>(tx + 8), static_cast<Gdiplus::REAL>(ty + 4),
                                static_cast<Gdiplus::REAL>(label_w - 12), static_cast<Gdiplus::REAL>(label_h - 6),
                                text.c_str(), kFontHover, kPointerLabelTextArgb, nara::dwrite_text::Align::Near,
                                nara::dwrite_text::Align::Center, true, true, true, 0, 0, true);
    g.ResetClip();
}

void PaintChart(HWND hwnd, HDC hdc) {
    RECT client{};
    GetClientRect(hwnd, &client);
    const int width = (std::max)(1, static_cast<int>(client.right));
    const int height = (std::max)(1, static_cast<int>(client.bottom));
    SyncWorld(hwnd);
    const auto view_key = MakeViewKey(width, height);
    if (ChartShowsScatter(g_chartTab)) {
        if (TryPresentZoom(hdc, width, height)) {
            return;
        }
        if (TryPresentPan(hdc, width, height)) {
            return;
        }
    }
    if (IsOverlayOnlyFrame(g_hasStatic, g_staticKey, view_key) && g_layerDc) {
        PresentChart(hdc, width, height);
        return;
    }
    EnsureLayer(hdc, width, height);
    HDC mem = g_layerDc;
    nara::dwrite_text::ResetQueue();
    if (ChartPageUsesFullCard(g_chartTab)) {
        nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.hist_plot.x), static_cast<float>(g_chrome.hist_plot.y),
                                       static_cast<float>(g_chrome.hist_plot.w), static_cast<float>(g_chrome.hist_plot.h));
    } else {
        nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.plot.x), static_cast<float>(g_chrome.plot.y),
                                       static_cast<float>(g_chrome.plot.w), static_cast<float>(g_chrome.plot.h));
    }

    {
    Gdiplus::Graphics g(mem);
    ApplyCrispGraphics(g);
    ApplyDpiTransform(g);
    g.Clear(Argb(kWindowBgArgb));

    Gdiplus::LinearGradientBrush accent_bar(Gdiplus::Point(0, 0), Gdiplus::Point(width, 0),
                                            Argb(chart_theme::kAccentBar0Argb), Argb(chart_theme::kAccentBar1Argb));
    g.FillRectangle(&accent_bar, g_chrome.accent.x, g_chrome.accent.y, g_chrome.accent.w, g_chrome.accent.h);

    Gdiplus::FontFamily family(kFontFamily);
    Gdiplus::Font axis_font(&family, kFontAxis, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::Font company_font(&family, kFontCompany, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::Font empty_font(&family, kFontEmpty, Gdiplus::FontStyleBold, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush muted(Argb(kSubtleTextArgb));
    Gdiplus::SolidBrush ink(Argb(kTextArgb));
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
    const Gdiplus::RectF card = ToRectF(g_chrome.map_card);
    DrawOffsetShadow(g, card, static_cast<float>(kCardRadius), kCardShadowDepth, kCardShadowBlur, kCardShadowArgb);
    FillRoundRect(g, card, static_cast<float>(kCardRadius), kCardFillArgb, kCardStrokeArgb, kCardStrokeW);
    FillRoundRect(g, ToRectF(g_chrome.map_inner), static_cast<float>(kMapInnerRadius), kBgArgb, 0, 0);

    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    if (ChartPageUsesFullCard(g_chartTab)) {
        const RectI hp = g_chrome.hist_plot;
        {
            Gdiplus::GraphicsPath plot_path;
            RoundRectPath(plot_path, ToRectF(hp), static_cast<float>(kPlotRadius));
            g.FillPath(&white, &plot_path);
            g.DrawPath(&axis, &plot_path);
        }
        if (ChartShowsHistogram(g_chartTab)) {
            PaintHistogramScene(g, family, hp.x, hp.y, hp.w, hp.h);
        }
        g.ResetClip();
        g.Flush();
        nara::dwrite_text::Flush(mem, width, height, g_dpi_scale);
        nara::dwrite_text::ResetQueue();
    } else {
    {
        Gdiplus::GraphicsPath plot_path;
        RoundRectPath(plot_path, Gdiplus::RectF(static_cast<Gdiplus::REAL>(plotL), static_cast<Gdiplus::REAL>(plotT),
                                                static_cast<Gdiplus::REAL>(plotW), static_cast<Gdiplus::REAL>(plotH)),
                      static_cast<float>(kPlotRadius));
        g.FillPath(&white, &plot_path);
        g.DrawPath(&axis, &plot_path);
    }
    g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
    ClipPlot(g);
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

    if (kBandPaintOnStaticScatter) {
        PaintBandLayer(g, plotL, plotT, plotW, plotH, g_chart.view.scroll_x);
    }

    if (rows == 0) {
        ClipPlot(g);
        Gdiplus::StringFormat empty_fmt;
        empty_fmt.SetAlignment(Gdiplus::StringAlignmentCenter);
        empty_fmt.SetLineAlignment(Gdiplus::StringAlignmentCenter);
        wchar_t empty[160];
        swprintf_s(empty, L"동국 참여 낙찰공고가 없습니다. 지역을 %s에서 바꿔 보세요.",
                   RegionLabelAt(RegionIndexFromToken(g_region_token)));
        nara::dwrite_text::QueueBox(static_cast<float>(plotL), static_cast<float>(plotT),
                                    static_cast<float>(plotW), static_cast<float>(plotH), empty, kFontEmpty,
                                    kSubtleTextArgb, nara::dwrite_text::Align::Center,
                                    nara::dwrite_text::Align::Center, true, false, false, 0, 0, true);
    }

    ClipPlot(g);
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
            const auto chip_name = RowCompanyName(chip);
            if (!CompanyVisible(chip_name) || !ChipPassesRank1Filter(g_rank1Only, MapRank1(chip, scatter))) {
                continue;
            }
            buckets[ToColumn(*chip.adjust_rate)].push_back(&chip);
        }
        for (auto& [col, bucket] : buckets) {
            std::sort(bucket.begin(), bucket.end(), [](const ParticipantRow* a, const ParticipantRow* b) {
                const auto name_a = RowCompanyName(*a);
                const auto name_b = RowCompanyName(*b);
                return ChipPaintBefore(IsDonggukName(name_a), a->rank == 1, a->rank, IsDonggukName(name_b),
                                       b->rank == 1, b->rank);
            });
            for (int i = 0; i < static_cast<int>(bucket.size()); ++i) {
                const auto& chip = *bucket[i];
                int side = 2;
                int x = 0;
                int yy = 0;
                PlaceChip(col, y, i, static_cast<int>(bucket.size()), cell, plotL, g_chart.view.scroll_x, x, yy, side);
                if (x + side < plotL || x > plotL + plotW || yy + side < plotT || yy > plotT + plotH) {
                    continue;
                }
                const auto color_name = RowCompanyName(chip);
                Gdiplus::Rect rect(x, yy, side, side);
                Gdiplus::RectF box(static_cast<Gdiplus::REAL>(x), static_cast<Gdiplus::REAL>(yy),
                                   static_cast<Gdiplus::REAL>(side), static_cast<Gdiplus::REAL>(side));
                ClipPlot(g);
                const bool rank1 = MapRank1(chip, scatter);
                PaintChipBody(g, box, cell, ChipFillArgb(color_name, chip), rank1);
                PaintWatchChipMark(g, rect, chip);
                if (rank1) {
                    const int bw = (std::max)(6, static_cast<int>(side * 0.42));
                    const int bh = (std::max)(6, static_cast<int>(side * 0.38));
                    Gdiplus::RectF badge(static_cast<Gdiplus::REAL>(rect.X + side - bw),
                                         static_cast<Gdiplus::REAL>(rect.Y + 1), static_cast<Gdiplus::REAL>(bw),
                                         static_cast<Gdiplus::REAL>(bh));
                    FillRoundRect(g, badge, static_cast<float>(kRankBadgeRadius), kRankBadgeArgb, 0, 0);
                }
                if (!SkipChipLabelsDuringPan() || !g_chart.panning) {
                    const auto short_name = ChipLabelUtf8(color_name);
                    DrawChipLabel(g, rect, Utf8ToWide(short_name), family, side);
                }
                ClipPlot(g);
            }
        }
    }

    g.ResetClip();
    g.Flush();
    nara::dwrite_text::Flush(mem, width, height, g_dpi_scale);
    nara::dwrite_text::ResetQueue();
    }
    {
        Gdiplus::SolidBrush gutter(Argb(kBgArgb));
        const Gdiplus::RectF inner = ToRectF(g_chrome.map_inner);
        if (ChartPageUsesFullCard(g_chartTab)) {
            const RectI hp = g_chrome.hist_plot;
            g.FillRectangle(&gutter, inner.X, inner.Y, inner.Width, static_cast<Gdiplus::REAL>(hp.y) - inner.Y);
            g.FillRectangle(&gutter, inner.X, static_cast<Gdiplus::REAL>(hp.Bottom()), inner.Width,
                            inner.GetBottom() - static_cast<Gdiplus::REAL>(hp.Bottom()));
            g.FillRectangle(&gutter, inner.X, static_cast<Gdiplus::REAL>(hp.y), static_cast<Gdiplus::REAL>(hp.x) - inner.X,
                            static_cast<Gdiplus::REAL>(hp.h));
            g.FillRectangle(&gutter, static_cast<Gdiplus::REAL>(hp.Right()), static_cast<Gdiplus::REAL>(hp.y),
                            inner.GetRight() - static_cast<Gdiplus::REAL>(hp.Right()), static_cast<Gdiplus::REAL>(hp.h));
        } else {
            g.FillRectangle(&gutter, inner.X, inner.Y, static_cast<Gdiplus::REAL>(plotL) - inner.X, inner.Height);
            g.FillRectangle(&gutter, static_cast<Gdiplus::REAL>(plotL), inner.Y, static_cast<Gdiplus::REAL>(plotW),
                            static_cast<Gdiplus::REAL>(plotT) - inner.Y);
            g.FillRectangle(&gutter, static_cast<Gdiplus::REAL>(plotL + plotW), inner.Y,
                            inner.GetRight() - static_cast<Gdiplus::REAL>(plotL + plotW), inner.Height);
            g.FillRectangle(&gutter, static_cast<Gdiplus::REAL>(plotL), static_cast<Gdiplus::REAL>(plotT + plotH),
                            static_cast<Gdiplus::REAL>(plotW),
                            inner.GetBottom() - static_cast<Gdiplus::REAL>(plotT + plotH));
        }
    }
    if (ChartShowsScatter(g_chartTab))
    for (int r = 0; r < rows; ++r) {
        const int y = plotT + (r * cell) - g_chart.view.scroll_y;
        if (!NoticeYLabelFits(y, cell, plotT, plotT + plotH, g_chrome.tab_strip.Bottom())) {
            continue;
        }
        const auto& scatter = g_chart.notices[r];
        const std::string open_date = r < static_cast<int>(g_chart.open_dates.size()) ? g_chart.open_dates[r] : "";
        const auto ytext = Utf8ToWide(FormatNoticeYLabel(scatter.notice_number, open_date));
        const int y0 = (std::max)(y, (std::max)(plotT, g_chrome.tab_strip.Bottom()));
        const int y1 = (std::min)(y + cell, plotT + plotH);
        if (y1 - y0 < 8) {
            continue;
        }
        nara::dwrite_text::QueueBox(static_cast<float>(g_chrome.map_inner.x + 6), static_cast<float>(y0),
                                    static_cast<float>(plotL - g_chrome.map_inner.x - 12),
                                    static_cast<float>(y1 - y0), ytext.c_str(), kFontAxis, kSubtleTextArgb,
                                    nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, false, true,
                                    true);
    }
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    {
        const RectI stroke_plot = ChartPageUsesFullCard(g_chartTab) ? g_chrome.hist_plot : g_chrome.plot;
        Gdiplus::GraphicsPath plot_path;
        RoundRectPath(plot_path, ToRectF(stroke_plot), static_cast<float>(kPlotRadius));
        g.DrawPath(&axis, &plot_path);
    }
    if (ChartShowsScatter(g_chartTab)) {
    FillGlyphs(g, kAxisNotice, family, Gdiplus::FontStyleBold, kFontAxis,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>(g_chrome.map_inner.x + 6),
                              static_cast<Gdiplus::REAL>(
                                  AxisNoticeTitleY(g_chrome.tab_strip.Bottom(), plotT)),
                              200.0f, 16.0f),
               Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentNear, kSubtleTextArgb);
    FillGlyphs(g, kAxisRate, family, Gdiplus::FontStyleBold, kFontAxis,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>((std::max)(plotL + 4, plotL + plotW - 116)),
                              static_cast<Gdiplus::REAL>(plotT + plotH + 38), 120.0f, 16.0f),
               Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentNear, kSubtleTextArgb);
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
        FillGlyphs(g, tick, family, Gdiplus::FontStyleRegular, kFontAxis,
                   Gdiplus::RectF(static_cast<Gdiplus::REAL>(x - 18), static_cast<Gdiplus::REAL>(plotT + plotH + 6),
                                  36.0f, 14.0f),
                   Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentNear, kSubtleTextArgb);
    }
    FillGlyphs(g, kAxisRank1Pct, family, Gdiplus::FontStyleBold, kFontAxis,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>(plotL - 72), static_cast<Gdiplus::REAL>(plotT + plotH + 20),
                              68.0f, 14.0f),
               Gdiplus::StringAlignmentFar, Gdiplus::StringAlignmentNear, kSubtleTextArgb);
    for (int bin = 0; bin < kBandWinBinCount; ++bin) {
        const auto pct = FormatBandWinBinPct(g_band, bin);
        if (pct.empty()) {
            continue;
        }
        const int x = RateToX(BandWinBinCenterRate(bin), plotL, g_chart.view.scroll_x, cell) + cell / 2;
        if (x < plotL - 8 || x > plotL + plotW) {
            continue;
        }
        const auto pct_w = Utf8ToWide(pct);
        nara::dwrite_text::QueueBox(static_cast<float>(x - 18), static_cast<float>(plotT + plotH + 20), 36.0f, 14.0f,
                                    pct_w.c_str(), kFontAxis, kTextArgb, nara::dwrite_text::Align::Center,
                                    nara::dwrite_text::Align::Near, false, false, false);
    }
    }

    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    PaintChartTabs(g, family);
    if (ChartShowsScatter(g_chartTab)) {
        PaintBandLegend(g);
    }
    if (ChartShowsMapToolbar(g_chartTab)) {
    const Gdiplus::RectF toolbar = ToRectF(g_chrome.reset_toolbar);
    const Gdiplus::RectF reset = ToRectF(g_chrome.reset_btn);
    DrawOffsetShadow(g, toolbar, static_cast<float>(kToolbarRadius), kToolbarShadowDepth, kToolbarShadowBlur,
                     kResetShadowArgb);
    FillRoundRect(g, toolbar, static_cast<float>(kToolbarRadius), kGlassFillArgb, kStrokeSubtleArgb, 1.0f);
    DrawOffsetShadow(g, reset, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                     kResetShadowArgb);
    FillRoundRect(g, reset, static_cast<float>(kResetBtnRadius), kGlassFillArgb,
                  g_hotReset ? kAccentArgb : kStrokeStrongArgb, 1.0f);
    FillGlyphs(g, kResetLabel, family, Gdiplus::FontStyleBold, kFontReset, reset, Gdiplus::StringAlignmentCenter,
               Gdiplus::StringAlignmentCenter, kTextArgb);

    const Gdiplus::RectF rank1 = ToRectF(g_chrome.rank1_btn);
    DrawOffsetShadow(g, rank1, static_cast<float>(kResetBtnRadius), kResetShadowDepth, kResetShadowBlur,
                     kResetShadowArgb);
    const uint32_t rank1_fill = g_rank1Only ? chart_theme::kAccentArgb : kGlassFillArgb;
    const uint32_t rank1_text = g_rank1Only ? 0xFFFFFFFFu : kTextArgb;
    FillRoundRect(g, rank1, static_cast<float>(kResetBtnRadius), rank1_fill,
                  (g_hotRank1 || g_rank1Only) ? kAccentArgb : kStrokeStrongArgb, 1.0f);
    FillGlyphs(g, kRank1Label, family, Gdiplus::FontStyleBold, kFontReset, rank1, Gdiplus::StringAlignmentCenter,
               Gdiplus::StringAlignmentCenter, rank1_text);
    PaintDesignPill(g, family);
    }

    const Gdiplus::RectF dock = ToRectF(g_chrome.dock);
    DrawOffsetShadow(g, dock, static_cast<float>(kDockRadius), kCardShadowDepth, kCardShadowBlur, kCardShadowArgb);
    FillRoundRect(g, dock, static_cast<float>(kDockRadius), kDockFillArgb, kStrokeSubtleArgb, 1.0f);
    Gdiplus::Pen divider(Argb(kStrokeSubtleArgb), 1.0f);
    g.DrawLine(&divider, static_cast<Gdiplus::REAL>(g_chrome.divider.x), static_cast<Gdiplus::REAL>(g_chrome.divider.y),
               static_cast<Gdiplus::REAL>(g_chrome.divider.x), static_cast<Gdiplus::REAL>(g_chrome.divider.Bottom()));

    auto caption_at = [&](const RectI& combo, const wchar_t* label) {
        FillGlyphs(g, label, family, Gdiplus::FontStyleRegular, kFontCaption,
                   Gdiplus::RectF(static_cast<Gdiplus::REAL>(combo.x),
                                  static_cast<Gdiplus::REAL>(combo.y - kCaptionH),
                                  static_cast<Gdiplus::REAL>((std::max)(48, combo.w)),
                                  static_cast<Gdiplus::REAL>(kCaptionH)),
                   Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentCenter, kMutedCaptionArgb);
    };
    FillGlyphs(g, kRangeLabel, family, Gdiplus::FontStyleBold, kFontEyebrow,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>(g_chrome.range_pane.x),
                              static_cast<Gdiplus::REAL>(g_chrome.range_pane.y), 120.0f, 16.0f),
               Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentNear, chart_theme::kAccentStrongArgb);
    caption_at(g_chrome.combo_period, kPeriodLabel);
    caption_at(g_chrome.combo_precision, kPrecisionLabel);
    caption_at(g_chrome.combo_region, kRegionLabel);
    PaintRegionSegment(g, g_chrome.combo_region, g_region_token);
    caption_at(g_chrome.combo_min, kMinLabel);
    caption_at(g_chrome.combo_max, kMaxLabel);

    FillGlyphs(g, kCompanyLabel, family, Gdiplus::FontStyleBold, kFontEyebrow,
               Gdiplus::RectF(static_cast<Gdiplus::REAL>(g_chrome.company_pane.x),
                              static_cast<Gdiplus::REAL>(CompanyHeaderLabelY(g_chrome.company_pane.y)), 80.0f, 16.0f),
               Gdiplus::StringAlignmentNear, Gdiplus::StringAlignmentNear, chart_theme::kAccentStrongArgb);
    const auto all_look = MainButtonLook(MainButtonKind::Ghost);
    if (g_hotAll) {
        FillRoundRect(g, ToRectF(g_chrome.company_all), 8.0f, kHoverFillArgb, 0, 0);
    }
    FillGlyphs(g, kCompanyAllLabel, family, Gdiplus::FontStyleBold, kFontCompanyAll, ToRectF(g_chrome.company_all),
               Gdiplus::StringAlignmentCenter, Gdiplus::StringAlignmentCenter,
               g_hotAll ? kAccentArgb : all_look.text);

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
            nara::dwrite_text::QueueBox(static_cast<float>(box.x), static_cast<float>(box.y),
                                        static_cast<float>(box.w), static_cast<float>(box.h), L"✓", 11.0f,
                                        kCheckMarkArgb, nara::dwrite_text::Align::Center,
                                        nara::dwrite_text::Align::Center, true);
        }
        Gdiplus::RectF label_box(static_cast<Gdiplus::REAL>(box.Right() + kCheckGap), static_cast<Gdiplus::REAL>(cell.y),
                                 static_cast<Gdiplus::REAL>((std::max)(8, cell.Right() - box.Right() - kCheckGap)),
                                 static_cast<Gdiplus::REAL>(cell.h));
        nara::dwrite_text::QueueBox(label_box.X, label_box.Y, label_box.Width, label_box.Height,
                                    g_companyItems[static_cast<size_t>(i)].display.c_str(), kFontCompany, kTextArgb,
                                    nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, true, true,
                                    true);
    }
    }

    if (CanCacheStaticLayer(g_chart.panning)) {
        g_staticKey = view_key;
        g_hasStatic = true;
        g_staticScrollX = g_chart.view.scroll_x;
        g_staticScrollY = g_chart.view.scroll_y;
        g_staticCell = CellSize(g_chart.view.scale);
    } else {
        g_hasStatic = false;
    }
    nara::dwrite_text::Flush(mem, width, height, g_dpi_scale);
    PresentChart(hdc, width, height);
}

void UpdateScroll(HWND hwnd) {
    SyncWorld(hwnd);
    if (ChartShowsHistogram(g_chartTab)) {
        SCROLLINFO si{sizeof(SCROLLINFO), SIF_RANGE | SIF_PAGE | SIF_POS};
        si.nMin = 0;
        si.nMax = 0;
        si.nPage = 1;
        si.nPos = 0;
        SetScrollInfo(hwnd, SB_HORZ, &si, TRUE);
        SetScrollInfo(hwnd, SB_VERT, &si, TRUE);
        return;
    }
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
    const int line = CellSize(g_chart.view.scale);
    switch (code) {
    case SB_LINELEFT:
        next -= line;
        break;
    case SB_LINERIGHT:
        next += line;
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
    const RECT& rc = item->rcItem;
    const bool edit = (item->itemState & ODS_COMBOBOXEDIT) != 0;
    if (edit) {
        return;
    }
    const int pw = rc.right - rc.left;
    const int ph = rc.bottom - rc.top;
    const float dip_w = static_cast<float>(ToDip(pw));
    const float dip_h = static_cast<float>(ToDip(ph));
    wchar_t caption[64] = {};
    if (item->itemID != 0xFFFFFFFFu) {
        SendMessageW(item->hwndItem, CB_GETLBTEXT, item->itemID, reinterpret_cast<LPARAM>(caption));
    }
    {
        Gdiplus::Graphics dg(item->hDC);
        ApplyCrispGraphics(dg);
        ApplyDpiTransform(dg);
        const Gdiplus::RectF r(static_cast<Gdiplus::REAL>(ToDip(rc.left)) + 0.5f,
                               static_cast<Gdiplus::REAL>(ToDip(rc.top)) + 0.5f, dip_w - 1.0f, dip_h - 1.0f);
        const bool selected = (item->itemState & ODS_SELECTED) != 0;
        Gdiplus::SolidBrush brush(Argb(selected ? kAccentSubtleArgb : kPlotBgArgb));
        dg.FillRectangle(&brush, r);
    }
    nara::dwrite_text::ResetQueue();
    nara::dwrite_text::QueueBox(10.0f, 0.0f, (std::max)(8.0f, dip_w - 16.0f), dip_h, caption, kFontBody, kTextArgb,
                                nara::dwrite_text::Align::Near, nara::dwrite_text::Align::Center, false, true, true);
    nara::dwrite_text::Flush(item->hDC, pw, ph, g_dpi_scale, rc.left, rc.top);
}

void ResetMapView(HWND hwnd) {
    ApplyCompanyFlags(-1, true);
    g_chart.view.scale = 1.0;
    SyncWorld(hwnd);
    g_chart.view = ShowLatestView(g_chart.view);
    g_hasStatic = false;
    g_hasPlotCache = false;
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
        PaintChart(hwnd, hdc);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_TIMER:
        if (wParam == kBounceTimer) {
            if (ChartShowsScatter(g_chartTab)) {
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;
        }
        if (wParam == kSitPulseTimer) {
            if (ChartShowsScatter(g_chartTab) && g_band.has_sit) {
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;
        }
        if (wParam == kZoomSettleTimer) {
            KillTimer(hwnd, kZoomSettleTimer);
            g_zooming = false;
            g_hasStatic = false;
            g_hasPlotCache = false;
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
        SyncDpi(hwnd);
        auto* info = reinterpret_cast<MINMAXINFO*>(lParam);
        info->ptMinTrackSize.x = ToPx(kWindowMinW);
        info->ptMinTrackSize.y = ToPx(kWindowMinH);
        return 0;
    }
    case WM_DPICHANGED: {
        g_hasStatic = false;
        ReleaseLayer();
        if (g_uiFont) {
            DeleteObject(g_uiFont);
            g_uiFont = nullptr;
        }
        const auto* rec = reinterpret_cast<const RECT*>(lParam);
        SetWindowPos(hwnd, nullptr, rec->left, rec->top, rec->right - rec->left, rec->bottom - rec->top,
                     SWP_NOZORDER | SWP_NOACTIVATE);
        LayoutChartDock(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_MEASUREITEM: {
        auto* measure = reinterpret_cast<MEASUREITEMSTRUCT*>(lParam);
        if (measure->CtlType == ODT_COMBOBOX) {
            measure->itemHeight = static_cast<UINT>(ToPx(28));
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
        const int hx = ToDip(pt.x);
        const int hy = ToDip(pt.y);
        const bool over_toolbar = ChartShowsMapToolbar(g_chartTab)
            && (g_chrome.reset_btn.Contains(hx, hy) || g_chrome.reset_toolbar.Contains(hx, hy)
                || g_chrome.rank1_btn.Contains(hx, hy) || g_chrome.design_btn.Contains(hx, hy));
        if (over_toolbar || g_chrome.company_all.Contains(hx, hy) || HitChartTab(g_chrome, hx, hy) >= 0
            || HitCompanyIndex(hx, hy) >= 0) {
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
        const int mx = ToDip(GET_X_LPARAM(lParam));
        const int my = ToDip(GET_Y_LPARAM(lParam));
        SyncWorld(hwnd);
        const auto region_seg = SplitRegionSegment(g_chrome.combo_region);
        const int region_hit = HitRegionSegment(region_seg, mx, my);
        if (region_hit >= 0) {
            if (g_region_handler) {
                g_region_handler(region_hit == 1 ? "경북" : "전국");
            }
            return 0;
        }
        const int tab = HitChartTab(g_chrome, mx, my);
        if (tab >= 0) {
            if (g_chartTab != tab) {
                g_chartTab = tab;
                g_hasStatic = false;
                g_hasPlotCache = false;
                g_histHoverBin = -1;
                UpdateScroll(hwnd);
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;
        }
        if (ChartShowsMapToolbar(g_chartTab) && g_chrome.design_btn.Contains(mx, my)) {
            OpenDesignPrinciplesWindow(GetModuleHandleW(nullptr), hwnd);
            return 0;
        }
        if (ChartShowsMapToolbar(g_chartTab) && g_chrome.rank1_btn.Contains(mx, my)) {
            g_rank1Only = !g_rank1Only;
            g_hasStatic = false;
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        if (ChartShowsMapToolbar(g_chartTab)
            && (g_chrome.reset_btn.Contains(mx, my)
                || (g_chrome.reset_toolbar.Contains(mx, my) && !g_chrome.design_btn.Contains(mx, my)))) {
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
        if (!ChartShowsScatter(g_chartTab) || !g_chrome.plot.Contains(mx, my)) {
            return 0;
        }
        {
            const int cell = CellSize(g_chart.view.scale);
            g_bandClickRow = cell > 0 ? (my - g_chrome.plot.y + g_chart.view.scroll_y) / cell : -1;
        }
        g_chart.drag_armed = true;
        g_chart.panning = false;
        g_chart.last_pt.x = mx;
        g_chart.last_pt.y = my;
        SetCapture(hwnd);
        return 0;
    }
    case WM_MOUSEMOVE: {
        const int mx = ToDip(GET_X_LPARAM(lParam));
        const int my = ToDip(GET_Y_LPARAM(lParam));
        if (g_chart.drag_armed || g_chart.panning) {
            const int dx = mx - g_chart.last_pt.x;
            const int dy = my - g_chart.last_pt.y;
            if (!g_chart.panning && (std::abs(dx) + std::abs(dy) >= 4)) {
                g_chart.panning = true;
                SetCursor(LoadCursor(nullptr, IDC_SIZEALL));
                BuildPlotCache();
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
        SyncWorld(hwnd);
        g_hotReset = ChartShowsMapToolbar(g_chartTab)
            && (g_chrome.reset_btn.Contains(mx, my)
                || (g_chrome.reset_toolbar.Contains(mx, my) && !g_chrome.design_btn.Contains(mx, my)
                    && !g_chrome.rank1_btn.Contains(mx, my)));
        g_hotRank1 = ChartShowsMapToolbar(g_chartTab) && g_chrome.rank1_btn.Contains(mx, my);
        g_hotDesign = ChartShowsMapToolbar(g_chartTab) && g_chrome.design_btn.Contains(mx, my);
        g_hotAll = g_chrome.company_all.Contains(mx, my);
        g_hotCompany = HitCompanyIndex(mx, my);
        g_hotTab = HitChartTab(g_chrome, mx, my);
        const bool over_plot = ChartPageUsesFullCard(g_chartTab) ? g_chrome.hist_plot.Contains(mx, my)
                                                                : g_chrome.plot.Contains(mx, my);
        if (over_plot && ChartShowsHistogram(g_chartTab)) {
            g_chart.hover_x = mx;
            g_chart.hover_y = my;
            EnsureHistogram();
            g_histHoverBin = HistogramHitBin(static_cast<float>(mx), static_cast<float>(my), g_histScene,
                                             g_hist.spec.bin_count);
            g_chart.hover_valid = g_histHoverBin >= 0;
        } else if (over_plot && ChartShowsScatter(g_chartTab)) {
            g_chart.hover_x = mx;
            g_chart.hover_y = my;
            const int col = HoverColumn(mx - g_chrome.plot.x, g_chart.view.scroll_x, g_chart.view.scale);
            g_chart.hover_valid = true;
            g_chart.hover_rate = ColumnToRate(col, g_chart.min_rate, g_chart.step);
            g_histHoverBin = -1;
        } else {
            g_chart.hover_valid = false;
            g_histHoverBin = -1;
        }
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_LBUTTONUP:
        if (!g_chart.panning && g_bandClickRow >= 0
            && g_bandClickRow < static_cast<int>(g_chart.notices.size())) {
            g_bandNotice = g_chart.notices[static_cast<size_t>(g_bandClickRow)].notice_number;
            RefreshBand();
        }
        g_bandClickRow = -1;
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
        if (ChartShowsHistogram(g_chartTab)) {
            return 0;
        }
        POINT pt{GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
        ScreenToClient(hwnd, &pt);
        const bool zoom_in = GET_WHEEL_DELTA_WPARAM(wParam) > 0;
        const int zx = ToDip(pt.x) - g_chrome.plot.x;
        const int zy = ToDip(pt.y) - g_chrome.plot.y;
        if (!g_zooming) {
            if (!g_hasPlotCache) {
                BuildPlotCache();
            }
            g_zooming = true;
        }
        g_chart.view = ZoomAt(g_chart.view, zx, zy, zoom_in);
        SetTimer(hwnd, kZoomSettleTimer, static_cast<UINT>(ZoomSettleMs()), nullptr);
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_DESTROY:
        KillTimer(hwnd, kBounceTimer);
        KillTimer(hwnd, kZoomSettleTimer);
        KillTimer(hwnd, kSitPulseTimer);
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
        const auto canon = CompanyIdentity(row);
        if (!canon.empty()) {
            names.insert(canon);
        }
    }
    std::vector<std::string> keys(names.begin(), names.end());
    g_displayLabels = BuildDisplayMap(keys);
    g_companyItems.clear();
    for (const auto& name : names) {
        g_companyItems.push_back({name, Utf8ToWide(DisplayLabelFor(name, g_displayLabels)), false});
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
    RefreshBand();
    g_prepareVersion += 1;
    g_hasStatic = false;
    UpdateChartTitle();
}

}  // namespace

void SetChartRegionHandler(ChartRegionHandler handler) {
    g_region_handler = handler;
}

void SetChartFocusNotice(const std::string& notice_id) {
    g_bandNotice = notice_id;
    RefreshBand();
    if (g_chartHwnd && IsWindow(g_chartHwnd)) {
        InvalidateRect(g_chartHwnd, nullptr, FALSE);
    }
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
        KillTimer(g_chartHwnd, kBounceTimer);
        SetTimer(g_chartHwnd, kSitPulseTimer, kSitPulseTimerMs, nullptr);
        SyncWorld(g_chartHwnd);
        g_chart.view = ShowLatestView(g_chart.view);
        UpdateScroll(g_chartHwnd);
        InvalidateRect(g_chartHwnd, nullptr, TRUE);
        ShowWindow(g_chartHwnd, SW_SHOW);
        SetForegroundWindow(g_chartHwnd);
        return;
    }
    g_chartHwnd = CreateWindowExW(0, L"NaraDesktopChart", kWindowTitle,
                                  WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_CLIPCHILDREN, CW_USEDEFAULT, CW_USEDEFAULT,
                                  1400, 900, owner, nullptr, instance, nullptr);
    CreateFilterDock(g_chartHwnd, instance);
    UpdateChartTitle();
    RECT work{};
    SystemParametersInfoW(SPI_GETWORKAREA, 0, &work, 0);
    SetWindowPos(g_chartHwnd, HWND_TOP, work.left, work.top, work.right - work.left, work.bottom - work.top,
                 SWP_SHOWWINDOW);
    SyncWorld(g_chartHwnd);
    g_chart.view = ShowLatestView(g_chart.view);
    UpdateScroll(g_chartHwnd);
    SetTimer(g_chartHwnd, kSitPulseTimer, kSitPulseTimerMs, nullptr);
    InvalidateRect(g_chartHwnd, nullptr, FALSE);
}

}  // namespace nara
