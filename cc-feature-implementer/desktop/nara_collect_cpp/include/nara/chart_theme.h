#pragma once

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <string>
#include <vector>

namespace nara {
namespace chart_theme {

// Geometry — ChartPopupWindow.xaml + NativeBidChartControl.DrawScatter (step 0.01).
constexpr int kCellPx = 20;
constexpr int kPlotLeft = 220;
constexpr int kPlotTop = 16;
constexpr int kBandRank1GutterW = 0;
constexpr int kPlotRight = 22;
constexpr int kPlotBottom = 62;
constexpr int kDockMinH = 134;
constexpr int kDockGap = 10;
constexpr int kDockPadX = 14;
constexpr int kDockPadY = 10;
constexpr int kContentML = 16;
constexpr int kContentMT = 12;
constexpr int kContentMR = 16;
constexpr int kContentMB = 14;
constexpr int kCardPad = 10;
constexpr int kCardInner = 2;
constexpr int kResetH = 40;
constexpr int kResetMinW = 124;
constexpr int kRank1BtnW = 72;
constexpr int kRank1BtnH = 40;
constexpr int kMapDesignW = 124;
constexpr int kResetMarginTop = 12;
constexpr int kResetMarginRight = 14;
constexpr int kToolbarPad = 8;
constexpr int kLeftPanelW = 320;
constexpr int kColGap = 14;
constexpr int kFieldGap = 8;
constexpr int kCaptionGap = 4;
constexpr int kCaptionH = 12;
constexpr int kEyebrowGap = 6;
constexpr int kComboH = 32;
constexpr int kCheckSize = 18;
constexpr int kCheckGap = 10;
constexpr int kCompanyAllW = 64;
constexpr int kCompanyAllH = 26;
constexpr int kCompanyHeaderH = 26;
constexpr float kFontCompanyAll = 13.0f;

// MainWindow.xaml table-card toolbar (PreviewCard Padding 16,14).
constexpr int kMainCardInset = 20;
constexpr int kTableCardY = 306;
constexpr int kMainIntelY = 128;
constexpr int kMainIntelH = 168;
constexpr float kMainToolbarShift = 0.0f;
constexpr int kMainKpiY = 8;
constexpr int kMainKpiH = 36;
constexpr int kMainKpiGap = 6;
constexpr int kMainTopY = 8;
constexpr int kMainTopH = 40;
constexpr int kMainBrandW = 168;
constexpr int kMainMosaicGap = 8;
constexpr int kMainTableFracNum = 20;
constexpr int kMainTableFracDen = 100;
constexpr int kMainTableMinH = 140;
constexpr int kMainReplayH = 28;
constexpr int kMainWindowMinW = 1280;
constexpr int kMainWindowMinH = 800;
constexpr int kMainFirmMinCellW = 108;
constexpr int kMainFirmMinCellH = 40;
constexpr int kMainFirmCellH = 52;
constexpr int kMainFirmDonut = 22;
constexpr int kMainChartShareNum = 46;
constexpr int kMainChartShareDen = 100;
constexpr int kMosaicYTickN = 4;
constexpr int kMosaicAxisPad = 28;
constexpr int kMosaicXLabelH = 14;
constexpr uint32_t kMosaicGridArgb = 0xFFE2E8F0;
constexpr uint32_t kMosaicAxisArgb = 0xFFCBD5E1;

inline double MosaicYTick(double ymin, double ymax, int i, int n = kMosaicYTickN) {
    if (n < 2) {
        return ymax;
    }
    const int idx = (std::max)(0, (std::min)(i, n - 1));
    return ymax - (ymax - ymin) * static_cast<double>(idx) / static_cast<double>(n - 1);
}
constexpr int kMainTablePadX = 16;
constexpr int kMainTablePadTop = 14;
constexpr int kMainHeaderGap = 12;
constexpr int kMainStatusH = 28;
constexpr int kMainStatusGap = 8;
constexpr int kMainBtnH = 40;
constexpr int kMainUpdateW = 124;
constexpr int kMainStopW = 108;
constexpr int kMainChartW = 132;
constexpr int kMainDesignW = 0;
constexpr int kMainRegionComboW = 148;
constexpr int kMainBtnGap = 8;
constexpr int kMainComboGap = 12;
constexpr int kMainRegionLabelGap = 8;
constexpr int kMainRegionLabelW = 24;
constexpr int kMainBtnRadius = 14;
constexpr int kCompanyWrapRows = 5;
constexpr int kCompanyPadX = 8;
constexpr int kCompanyPadY = 6;
constexpr int kCompanyWrapMinH = 102;
constexpr int kCompanyMinCellW = 84;
constexpr int kCompanyRowH = 20;
constexpr int kDividerW = 1;
constexpr int kWindowMinW = 1320;
constexpr int kWindowMinH = 820;
constexpr int kCardShadowDepth = 4;
constexpr int kCardShadowBlur = 22;
constexpr int kToolbarShadowDepth = 6;
constexpr int kToolbarShadowBlur = 22;
constexpr int kResetShadowDepth = 4;
constexpr int kResetShadowBlur = 16;

// Radii — DesignTokens.xaml + MapResetButtonStyle / hover callout / rank badge.
constexpr int kCardRadius = 22;
constexpr int kMapInnerRadius = 16;
constexpr int kPlotRadius = 16;
constexpr int kDockRadius = 16;
constexpr int kToolbarRadius = 16;
constexpr int kResetBtnRadius = 14;
constexpr int kComboRadius = 12;
constexpr int kRegionSegmentRadius = kComboRadius;
constexpr int kCompanyPanelRadius = 12;
constexpr int kHoverRadius = 6;
constexpr int kRankBadgeRadius = 2;
constexpr int kChipRadius = 0;
constexpr int kCheckRadius = 6;
// WPF TextFormattingMode.Ideal is DirectWrite NATURAL_SYMMETRIC + grayscale, not GDI+.
constexpr int kTextRenderingHintIdeal = 4;
constexpr bool kTextUsesDirectWrite = true;

// Fonts (px) — Malgun Gothic, matching ScatterLabelTypeface / DesignTokens.
constexpr float kFontAxis = 10.0f;
constexpr float kFontChipMin = 4.0f;
constexpr float kFontChipMax = 11.0f;
constexpr float kFontEyebrow = 11.0f;
constexpr float kFontCaption = 12.0f;
constexpr float kFontReset = 13.0f;
constexpr float kFontCompany = 11.0f;
constexpr float kFontHover = 9.8f;
constexpr float kFontEmpty = 14.0f;
constexpr float kFontBody = 13.0f;

// Stroke widths — NativeBidChartControl pens.
constexpr float kGridWidth = 1.0f;
constexpr float kAxisWidth = 1.2f;
constexpr float kChipBorderWidth = 0.8f;
constexpr float kChipLabelOutlineWidth = 0.4f;
constexpr float kGoldWidth = 2.3f;
constexpr float kGoldOuterWidth = 3.2f;
constexpr float kHoverChipWidth = 1.8f;
constexpr float kPointerWidth = 1.2f;
constexpr float kAccentBarH = 3.0f;
constexpr float kCardStrokeW = 1.0f;
constexpr float kComboStrokeW = 1.2f;

// Colors (AARRGGBB) — NativeBidChartControl + DesignTokens + bidder_colors.json.
constexpr uint32_t kBgArgb = 0xFFF6F8FB;
constexpr uint32_t kPlotBgArgb = 0xFFFFFFFF;
constexpr uint32_t kGridArgb = 0xFFD9DEE7;
constexpr uint32_t kMajorGridArgb = 0xFFBFC7D4;
constexpr uint32_t kAxisArgb = 0xFF5E6A7D;
constexpr uint32_t kTextArgb = 0xFF111111;
constexpr uint32_t kSubtleTextArgb = 0xFF5A6473;
constexpr uint32_t kMutedCaptionArgb = 0xFF4B5563;
constexpr uint32_t kAccentArgb = 0xFFD97706;
constexpr uint32_t kAccentStrongArgb = 0xFFB45309;
constexpr uint32_t kAccentBar0Argb = 0xFFF59E0B;
constexpr uint32_t kAccentBar1Argb = 0xFFB45309;
constexpr uint32_t kDonggukArgb = 0xFFFF0000;
constexpr uint32_t kRankGoldArgb = 0xFFFACC15;
constexpr uint32_t kRankGoldOuterArgb = 0xFFCA8A04;
constexpr uint32_t kRankBadgeArgb = 0xFFC62828;
constexpr uint32_t kChipBorderArgb = 0xFF21252B;
constexpr uint32_t kChipLabelOutlineArgb = 0x59000000;
constexpr uint32_t kPointerArgb = 0xFF2563EB;
constexpr uint32_t kPointerLabelBgArgb = 0xFFEFF6FF;
constexpr uint32_t kPointerLabelTextArgb = 0xFF1D4ED8;
constexpr uint32_t kCalloutPenArgb = 0xFFFDBA74;
constexpr uint32_t kStrokeSubtleArgb = 0xFFD8E0EA;
constexpr uint32_t kStrokeStrongArgb = 0xFFBAC6D5;
constexpr uint32_t kComboStrokeArgb = kStrokeStrongArgb;
constexpr uint32_t kGlassFillArgb = 0xD6FFFFFF;
constexpr uint32_t kDockFillArgb = 0xF2FFFFFF;
constexpr uint32_t kCompanyPanelArgb = 0xFFFFFFFF;
constexpr uint32_t kShadowArgb = 0x2E081018;
constexpr uint32_t kWindowBgArgb = 0xFFF3F5F8;
constexpr uint32_t kCardFillArgb = 0xFFFFFFFF;
constexpr uint32_t kCardStrokeArgb = 0xFFD8E0EA;
constexpr uint32_t kCardShadowArgb = 0x14000000;
constexpr uint32_t kResetShadowArgb = 0x2E081018;
constexpr uint32_t kCheckFillArgb = 0xFFFFFFFF;
constexpr uint32_t kCheckMarkArgb = 0xFFFFFFFF;
constexpr uint32_t kAccentSubtleArgb = 0xFFFFF4E5;
constexpr uint32_t kHoverFillArgb = 0xFFEEF3F8;
constexpr uint32_t kMapDesignFillArgb = 0xFF2563EBu;
constexpr uint32_t kMapDesignHotArgb = 0xFF3B82F6u;
constexpr uint32_t kMapDesignTextArgb = 0xFFFFFFFFu;

constexpr int kChartTabH = 34;
constexpr int kChartTabW = 112;
constexpr int kChartTabGap = 6;
constexpr int kChartTabPad = 12;
constexpr int kChartTabCount = 2;
constexpr int kChartTabMap = 0;
constexpr int kChartTabHist = 1;
constexpr int kChartTabRadius = 12;
constexpr int kPrincipleMinW = 840;
constexpr int kPrincipleMinH = 960;
constexpr int kPrincipleFigureW = 0;
constexpr int kPrincipleFigureH = 168;
constexpr int kPrincipleStackedFigureH = 100;
constexpr int kPrinciplePad = 12;
constexpr int kPrincipleGapX = 20;
constexpr int kPrincipleBlockGap = 16;
constexpr int kPrincipleTitleH = 24;
constexpr int kPrincipleLineH = 20;
constexpr float kPrincipleTitleSize = 16.0f;
constexpr float kPrincipleBodySize = 13.0f;
constexpr int kDesignPadX = 28;
constexpr int kDesignPadY = 22;
constexpr int kDesignMeasureMax = 720;
constexpr int kDesignSectionGap = 22;
constexpr int kDesignParaGap = 12;
constexpr int kDesignQuotePad = 14;
constexpr int kDesignRuleH = 1;
constexpr int kDesignLeadBarW = 3;
constexpr int kDesignWheelStep = 48;
constexpr float kDesignLeadSize = 18.0f;
constexpr float kDesignHeadingSize = 15.0f;
constexpr float kDesignBodySize = 13.0f;
constexpr float kDesignLineHeight = 1.55f;
constexpr uint32_t kDesignLeadArgb = kAccentStrongArgb;
constexpr uint32_t kDesignHeadingArgb = kTextArgb;
constexpr uint32_t kDesignBodyArgb = kTextArgb;
constexpr uint32_t kDesignMutedArgb = kSubtleTextArgb;
constexpr uint32_t kDesignQuoteFillArgb = kAccentSubtleArgb;
constexpr uint32_t kDesignRuleArgb = kStrokeSubtleArgb;
constexpr int kEpilogueLeadBarW = 3;
constexpr int kEpilogueQuotePad = 12;
constexpr uint32_t kEpilogueAccentArgb = kMapDesignFillArgb;
constexpr uint32_t kEpilogueQuoteFillArgb = 0x152563EBu;
constexpr wchar_t kEpilogueEyebrow[] = L"설계원칙";
constexpr int kHistogramBinCount = 40;
constexpr int kHistogramMaxSeries = 8;
constexpr int kHistPadL = 52;
constexpr int kHistPadR = 28;
constexpr int kHistPadT = 28;
constexpr int kHistPadB = 36;
constexpr float kHistDepthX = 0.55f;
constexpr float kHistDepthY = 0.32f;
constexpr float kHistBarGap = 0.18f;
constexpr float kHistSeriesGap = 6.0f;
constexpr uint32_t kHistFloorArgb = 0xFFF3F6FB;
constexpr uint32_t kHistGridArgb = 0xFFD5DDE8;
constexpr uint32_t kHistMarketLowArgb = 0xFF22D3EE;
constexpr uint32_t kHistMarketMidArgb = 0xFF818CF8;
constexpr uint32_t kHistMarketHighArgb = 0xFFF43F5E;
constexpr uint32_t kHistMarketPeakArgb = 0xFFFBBF24;
constexpr uint32_t kHistTabOnFillArgb = kAccentArgb;
constexpr uint32_t kHistTabOnTextArgb = 0xFFFFFFFFu;
constexpr uint32_t kHistTabOffFillArgb = kGlassFillArgb;
constexpr uint32_t kHistTabOffTextArgb = kSubtleTextArgb;
constexpr uint32_t kHistMarketGhostA = 0x66u;

constexpr bool kBandPaintSelfHouse = false;
constexpr bool kBandPaintRivalHouse = false;
constexpr bool kBandPaintCling = false;
constexpr bool kBandPaintTerritory = true;
constexpr bool kBandPaintUseStages = true;
constexpr bool kBandPaintPreyChips = false;
constexpr bool kBandPaintThreatChips = false;
constexpr bool kBandPaintThreat = true;
constexpr bool kBandPaintMedian = false;
constexpr bool kBandPaintOnStaticScatter = true;
constexpr bool kBandPaintWatch = true;
constexpr bool kBandPaintHome = true;
constexpr bool kBandPaintSit = true;
constexpr uint32_t kBandTerritoryArgb = 0x182563EBu;
constexpr uint32_t kBandPreyChipArgb = 0xFFEA580Cu;
constexpr uint32_t kBandThreatChipArgb = 0xFF7C3AEDu;
constexpr uint32_t kBandThreatFillArgb = 0x227C3AEDu;
constexpr int kBandUseStageCount = 7;
constexpr uint32_t kBandUseArgb = 0x1822C55Eu;
constexpr uint32_t kBandWatchFillArgb = 0x28475569u;
constexpr uint32_t kBandHomeArgb = 0x28F59E0Bu;
constexpr uint32_t kBandSitArgb = 0x5522D3EEu;
constexpr uint32_t kWatchRegularMarkArgb = 0xFF1E293Bu;
constexpr uint32_t kWatchOnceMarkArgb = 0xFF64748Bu;
constexpr unsigned kSitPulsePeriodMs = 1400;
constexpr unsigned kSitPulseTimerMs = 16;
constexpr int kSitGlowLayerCount = 3;

inline uint32_t SitPulseAlpha(unsigned elapsed_ms) {
    const unsigned period = (std::max)(1u, kSitPulsePeriodMs);
    const double t = static_cast<double>(elapsed_ms % period) / static_cast<double>(period);
    const double wave = 0.5 + 0.5 * std::sin(t * 6.283185307179586);
    return static_cast<uint32_t>(0x38 + static_cast<int>(wave * 0x61));
}

inline uint32_t SitPulseArgb(uint32_t base, unsigned elapsed_ms) {
    return (SitPulseAlpha(elapsed_ms) << 24) | (base & 0x00FFFFFFu);
}

inline float SitGlowPadDip(int layer) {
    if (layer <= 0) {
        return 10.0f;
    }
    if (layer == 1) {
        return 5.0f;
    }
    return 1.0f;
}

inline uint32_t SitGlowLayerArgb(uint32_t core, int layer, unsigned elapsed_ms) {
    const uint32_t pulse = SitPulseAlpha(elapsed_ms);
    unsigned weight = 40u;
    if (layer == 1) {
        weight = 100u;
    } else if (layer >= 2) {
        weight = 200u;
    }
    const uint32_t a = (pulse * weight) / 255u;
    return (a << 24) | (core & 0x00FFFFFFu);
}
constexpr std::array<uint32_t, kBandUseStageCount> kBandUseStageArgb = {
    0x1822C55Eu, 0x1822C55Eu, 0x1822C55Eu, 0x1822C55Eu, 0x1822C55Eu, 0x1822C55Eu, 0x1822C55Eu,
};
constexpr uint32_t kBandSelfHouseArgb = 0x00DC2626u;
constexpr uint32_t kBandHouseArgb = 0x003256EBu;
constexpr uint32_t kBandClingArgb = 0x00B45309u;
constexpr uint32_t kBandMedianArgb = 0xFF111827u;
constexpr float kBandMedianWidth = 1.2f;
constexpr int kBandStatusPad = 12;
constexpr int kBandLegendSwatchW = 12;
constexpr int kBandLegendSwatchH = 10;
constexpr int kBandLegendSwatchGap = 0;
constexpr int kBandLegendPadX = 8;
constexpr int kBandLegendTextGap = 8;
constexpr uint32_t kBandLegendFillArgb = kGlassFillArgb;
constexpr uint32_t kBandLegendStrokeArgb = kStrokeSubtleArgb;
constexpr wchar_t kBandLegendTitle[] = L"동국구간";
constexpr wchar_t kBandRank1Label[] = L"1위";

inline uint32_t BandUseStageArgbAt(int i) {
    int idx = i;
    if (idx < 0) {
        idx = 0;
    }
    if (idx >= kBandUseStageCount) {
        idx = kBandUseStageCount - 1;
    }
    return kBandUseStageArgb[static_cast<size_t>(idx)];
}

inline void BandUseStageInterval(double lo, double hi, int i, double& s_lo, double& s_hi) {
    const int n = kBandUseStageCount;
    int idx = i;
    if (idx < 0) {
        idx = 0;
    }
    if (idx >= n) {
        idx = n - 1;
    }
    const double w = (hi - lo) / static_cast<double>(n);
    s_lo = lo + w * static_cast<double>(idx);
    s_hi = lo + w * static_cast<double>(idx + 1);
}

constexpr wchar_t kFontFamily[] = L"Malgun Gothic";
constexpr wchar_t kResetLabel[] = L"맵 초기화";
constexpr wchar_t kRank1Label[] = L"1위";
constexpr wchar_t kRangeLabel[] = L"분석 범위";
constexpr wchar_t kPeriodLabel[] = L"기간";
constexpr wchar_t kPrecisionLabel[] = L"정밀도";
constexpr wchar_t kRegionLabel[] = L"지역";
constexpr wchar_t kRegionNationwide[] = L"전국";
constexpr wchar_t kRegionGyeongbuk[] = L"경북";
constexpr int kRegionSegmentCount = 2;
constexpr wchar_t kMinLabel[] = L"최소";
constexpr wchar_t kMaxLabel[] = L"최대";
constexpr wchar_t kCompanyLabel[] = L"업체";
constexpr wchar_t kCompanyAllLabel[] = L"전체";
constexpr wchar_t kAxisNotice[] = L"공고(개찰일시)";
constexpr wchar_t kAxisRate[] = L"기초대비사정률(%)";
constexpr wchar_t kAxisRank1Pct[] = L"1위 (%)";
constexpr wchar_t kChartTabMapLabel[] = L"전장맵";
constexpr wchar_t kChartTabHistLabel[] = L"히스토그램";
constexpr wchar_t kMainDesignLabel[] = L"";
constexpr wchar_t kMapDesignLabel[] = L"설계원칙";
constexpr wchar_t kDesignPrinciplesTitle[] = L"동국문화유산연구원 낙찰분석프로그램 - 설계원칙";
constexpr wchar_t kDesignPrinciplesClass[] = L"NaraDesignPrinciples";
constexpr wchar_t kHistAxisCount[] = L"투찰 건수";
constexpr wchar_t kHistMarketLabel[] = L"전체";
constexpr wchar_t kHistRank1Label[] = L"1위";
constexpr wchar_t kHistEmpty[] = L"표시할 투찰이 없습니다. 기간·지역을 조정하세요.";
constexpr wchar_t kEmptyMessage[] = L"동국 참여 낙찰공고가 없습니다. 기간·지역을 조정하세요.";
constexpr wchar_t kWindowTitle[] = L"동국문화유산연구원 낙찰분석프로그램 - 분석 창";

constexpr std::array<uint32_t, 32> kCompanyPalette = {
    0xFF1F77B4, 0xFF2CA02C, 0xFF9467BD, 0xFF17BECF, 0xFFBCBD22, 0xFFE377C2, 0xFF8C564B, 0xFF397DF6,
    0xFF2AA84A, 0xFF7C3AED, 0xFF0E9FA8, 0xFF9A34B8, 0xFF0F7667, 0xFF462ABA, 0xFF1D4E89, 0xFF108C57,
    0xFF6D28D9, 0xFF0E7490, 0xFF65A30D, 0xFFB45309, 0xFF7E22CE, 0xFF2563EB, 0xFF05958B, 0xFF433D8B,
    0xFF0D9488, 0xFF54750A, 0xFF2B6CB0, 0xFF4D7C0F, 0xFF0F76A8, 0xFF5B21B6, 0xFF15893F, 0xFF3B82F6,
};

// WPF layout is DIP (96dpi). C++ must ScaleTransform(dpi/96), not treat 20 as physical px.
inline float DpiScaleFromDpi(unsigned dpi) {
    const unsigned clamped = dpi < 96 ? 96 : (dpi > 384 ? 384 : dpi);
    return static_cast<float>(clamped) / 96.0f;
}

inline int DipToPx(int dip, float scale) {
    const float s = scale > 0.0f ? scale : 1.0f;
    return static_cast<int>(std::lround(static_cast<double>(dip) * static_cast<double>(s)));
}

inline int PxToDip(int px, float scale) {
    const float s = scale > 0.0f ? scale : 1.0f;
    return static_cast<int>(std::lround(static_cast<double>(px) / static_cast<double>(s)));
}

inline int ScatterColumnCount(double min_rate, double max_rate, double step) {
    if (!(step > 0.0)) {
        return 1;
    }
    const int cols = static_cast<int>(std::llround((max_rate - min_rate) / step)) + 1;
    return (std::max)(1, cols);
}

inline uint32_t DarkenArgb(uint32_t argb, float amount) {
    const float keep = 1.0f - amount;
    const uint32_t a = (argb >> 24) & 0xFFu;
    const auto channel = [keep](uint32_t value) -> uint32_t {
        return static_cast<uint32_t>(std::lround(static_cast<double>(value) * static_cast<double>(keep)));
    };
    return (a << 24) | (channel((argb >> 16) & 0xFFu) << 16) | (channel((argb >> 8) & 0xFFu) << 8)
           | channel(argb & 0xFFu);
}

inline uint32_t LightenArgb(uint32_t argb, float amount) {
    const float t = amount < 0.0f ? 0.0f : (amount > 1.0f ? 1.0f : amount);
    const uint32_t a = (argb >> 24) & 0xFFu;
    const auto channel = [t](uint32_t value) -> uint32_t {
        const double lifted = static_cast<double>(value) + (255.0 - static_cast<double>(value)) * static_cast<double>(t);
        return static_cast<uint32_t>(std::lround(lifted));
    };
    return (a << 24) | (channel((argb >> 16) & 0xFFu) << 16) | (channel((argb >> 8) & 0xFFu) << 8)
           | channel(argb & 0xFFu);
}

inline uint32_t LerpArgb(uint32_t a, uint32_t b, float t) {
    const float u = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
    const auto mix = [u](uint32_t lo, uint32_t hi) -> uint32_t {
        return static_cast<uint32_t>(
            std::lround(static_cast<double>(lo) + (static_cast<double>(hi) - static_cast<double>(lo)) * static_cast<double>(u)));
    };
    return (mix((a >> 24) & 0xFFu, (b >> 24) & 0xFFu) << 24) | (mix((a >> 16) & 0xFFu, (b >> 16) & 0xFFu) << 16)
           | (mix((a >> 8) & 0xFFu, (b >> 8) & 0xFFu) << 8) | mix(a & 0xFFu, b & 0xFFu);
}

inline uint32_t WithAlpha(uint32_t argb, uint32_t alpha) {
    return ((alpha & 0xFFu) << 24) | (argb & 0x00FFFFFFu);
}

inline uint32_t HistDensityArgb(float t) {
    const float u = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
    if (u < 0.35f) {
        return LerpArgb(kHistMarketLowArgb, kHistMarketMidArgb, u / 0.35f);
    }
    if (u < 0.70f) {
        return LerpArgb(kHistMarketMidArgb, kHistMarketPeakArgb, (u - 0.35f) / 0.35f);
    }
    return LerpArgb(kHistMarketPeakArgb, kHistMarketHighArgb, (u - 0.70f) / 0.30f);
}

inline uint32_t ChipOutlineArgb() { return kChipBorderArgb; }

// GDI+ sub-1px pens rasterize fat. Stroke first, then fill, so Hangul stays C#-clean.
inline bool ChipLabelStrokeBehindFill() { return true; }

inline int BlurShadowLayerCount(int blur) {
    if (blur <= 0) {
        return 1;
    }
    return (std::min)(6, (std::max)(3, (blur + 3) / 4));
}

inline float BlurShadowInflate(int blur, int layer, int layers) {
    const int safe_layers = (std::max)(1, layers);
    const float t = static_cast<float>(layer + 1) / static_cast<float>(safe_layers);
    return static_cast<float>((std::max)(0, blur)) * t * 0.32f;
}

inline uint32_t BlurShadowArgb(uint32_t argb, int layer, int layers) {
    const int safe_layers = (std::max)(1, layers);
    const uint32_t a = (argb >> 24) & 0xFFu;
    const double falloff = 1.0 - (static_cast<double>(layer) / static_cast<double>(safe_layers));
    const uint32_t na = static_cast<uint32_t>(
        std::lround(static_cast<double>(a) * falloff / (static_cast<double>(safe_layers) * 0.62)));
    return ((std::min)(255u, na) << 24) | (argb & 0x00FFFFFFu);
}

inline float ChipRadiusPx(int cell) {
    return static_cast<float>(kChipRadius) * (static_cast<float>((std::max)(1, cell)) / static_cast<float>(kCellPx));
}

inline float ChipLabelFontPx(int cell, int char_count) {
    const float side = (std::max)(2.0f, static_cast<float>(cell) - 2.0f);
    const float max_w = (std::max)(2.0f, side - 2.0f);
    const float max_h = (std::max)(2.0f, side - 2.0f);
    const float denom = (std::max)(1.05f, static_cast<float>((std::max)(1, char_count)));
    const float raw = (std::min)(max_w / denom, max_h * 0.62f);
    if (raw < kFontChipMin) {
        return kFontChipMin;
    }
    if (raw > kFontChipMax) {
        return kFontChipMax;
    }
    return raw;
}

// C# DrawShortText is 4–11px in 20px world space, then ScaleTransform(scale).
// Screen size = world font * (cell / 20). Do not recap at 11px or zoom-in looks smaller.
inline float ChipLabelScreenPx(int cell, int char_count) {
    const float base = ChipLabelFontPx(kCellPx, char_count);
    const float scaled = base * (static_cast<float>((std::max)(1, cell)) / static_cast<float>(kCellPx));
    if (scaled < kFontChipMin) {
        return kFontChipMin;
    }
    return scaled;
}

inline float ChipLabelOutlineWidthPx(int cell) {
    return kChipLabelOutlineWidth * (static_cast<float>((std::max)(1, cell)) / static_cast<float>(kCellPx));
}

struct ChipDodge {
    int dx = 1;
    int dy = 1;
    int side = 18;
};

inline ChipDodge LayoutChipDodge(int index, int count, int cell) {
    ChipDodge d;
    const int n = (std::max)(1, count);
    const int safe_cell = (std::max)(4, cell);
    if (n <= 1) {
        d.side = (std::max)(2, safe_cell - 2);
        d.dx = (safe_cell - d.side) / 2;
        d.dy = d.dx;
        return d;
    }
    int cols = 2;
    if (n > 4) {
        cols = 3;
    }
    if (n > 9) {
        cols = 4;
    }
    const int rows = (n + cols - 1) / cols;
    const int col = ((std::max)(0, index)) % cols;
    const int row = ((std::max)(0, index)) / cols;
    const int pad = 1;
    const int inner = (std::max)(2, safe_cell - pad * 2);
    const int cw = (std::max)(2, inner / cols);
    const int rh = (std::max)(2, inner / rows);
    d.side = (std::max)(2, (std::min)(cw, rh) - 2);
    d.dx = pad + col * cw + (std::max)(0, (cw - d.side) / 2);
    d.dy = pad + row * rh + (std::max)(0, (rh - d.side) / 2);
    return d;
}

inline bool ChipDodgeRectsOverlap(const ChipDodge& a, const ChipDodge& b) {
    return a.dx < b.dx + b.side && b.dx < a.dx + a.side && a.dy < b.dy + b.side && b.dy < a.dy + a.side;
}

inline bool ChipDodgeDrawsLabel(int side) {
    return side >= 6;
}

inline float ChipGoldInset() { return kGoldWidth * 0.5f; }

inline bool NoticeYLabelFits(int label_y, int cell, int plot_y, int plot_bottom, int tab_bottom) {
    const int vis_top = (std::max)(plot_y, tab_bottom);
    const int lab_bot = label_y + (std::max)(1, cell);
    return lab_bot > vis_top && label_y < plot_bottom;
}

inline int AxisNoticeTitleY(int tab_bottom, int plot_y) {
    const int under_tab = tab_bottom + 2;
    if (under_tab + static_cast<int>(kFontAxis) <= plot_y) {
        return under_tab;
    }
    return plot_y;
}

inline std::string FormatNoticeYLabel(const std::string& notice, const std::string& open_date) {
    if (open_date.size() >= 10 && open_date[4] == '-' && open_date[7] == '-') {
        const std::string date = open_date.substr(2, 8);
        std::string time = "00:00";
        if (open_date.size() >= 16) {
            time = open_date.substr(11, 5);
        }
        return notice + " (" + date + " " + time + ")";
    }
    if (!open_date.empty()) {
        return notice + " (" + open_date + ")";
    }
    return notice;
}

inline void UniformGridCell(int index, int count, int rows, int& col, int& row, int& cols) {
    const int safe_rows = (std::max)(1, rows);
    cols = (std::max)(1, (count + safe_rows - 1) / safe_rows);
    col = index % cols;
    row = index / cols;
}

inline int CompanyWrapColCount(int count, int inner_w) {
    const int max_cols = (std::max)(1, inner_w / (std::max)(1, kCompanyMinCellW));
    if (count <= 0) {
        return 1;
    }
    const int cols_for_min_rows = (count + kCompanyWrapRows - 1) / kCompanyWrapRows;
    if (cols_for_min_rows <= max_cols) {
        return (std::max)(1, cols_for_min_rows);
    }
    return max_cols;
}

inline int CompanyWrapRowCount(int count, int inner_w) {
    if (count <= 0) {
        return kCompanyWrapRows;
    }
    const int cols = CompanyWrapColCount(count, inner_w);
    return (std::max)(kCompanyWrapRows, (count + cols - 1) / cols);
}

inline bool ChipPaintBefore(bool a_dongguk, bool a_rank1, int a_rank, bool b_dongguk, bool b_rank1,
                            int b_rank) {
    if (a_dongguk != b_dongguk) {
        return !a_dongguk;
    }
    if (a_rank1 != b_rank1) {
        return !a_rank1;
    }
    const int ra = a_rank > 0 ? a_rank : 1'000'000;
    const int rb = b_rank > 0 ? b_rank : 1'000'000;
    return ra < rb;
}

struct RectI {
    int x = 0;
    int y = 0;
    int w = 0;
    int h = 0;

    int Right() const { return x + w; }
    int Bottom() const { return y + h; }
    bool Contains(int px, int py) const { return px >= x && py >= y && px < Right() && py < Bottom(); }
    bool Intersects(const RectI& o) const {
        return w > 0 && h > 0 && o.w > 0 && o.h > 0 && x < o.Right() && Right() > o.x && y < o.Bottom()
               && Bottom() > o.y;
    }
};

enum class MainButtonKind { Primary, Secondary, Ghost, Danger };

struct MainButtonColors {
    uint32_t fill0 = 0;
    uint32_t fill1 = 0;
    uint32_t fill2 = 0;
    uint32_t border = 0;
    uint32_t text = 0;
    bool gradient = false;
    bool transparent = false;
};

inline MainButtonColors MainButtonLook(MainButtonKind kind) {
    switch (kind) {
    case MainButtonKind::Primary:
        return {0xFFF59E0B, 0xFFD97706, 0xFFB45309, 0xFFB45309, 0xFFFFFFFF, true, false};
    case MainButtonKind::Secondary:
        return {0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFD8E0EA, 0xFF111111, false, false};
    case MainButtonKind::Ghost:
        return {0x00FFFFFF, 0x00FFFFFF, 0x00FFFFFF, 0x00000000, 0xFF4B5563, false, true};
    case MainButtonKind::Danger:
        return {0xFFFEF2F2, 0xFFFEF2F2, 0xFFFEF2F2, 0xFFF5C2C7, 0xFFDC2626, false, false};
    }
    return {};
}

inline MainButtonKind MainStopKind(bool collecting) {
    return collecting ? MainButtonKind::Danger : MainButtonKind::Ghost;
}

struct MainToolbar {
    RectI region_label;
    RectI region_combo;
    RectI update;
    RectI stop;
    RectI design;
    RectI chart;
};

inline int MainToolbarY(int card_y) { return card_y + kMainTablePadTop; }

inline int MainStatusY(int card_y) { return MainToolbarY(card_y) + kMainBtnH + kMainStatusGap; }

inline RectI LayoutMainStatus(int client_w, int card_y) {
    return {kMainCardInset + kMainTablePadX, MainStatusY(card_y),
            (std::max)(80, client_w - 2 * (kMainCardInset + kMainTablePadX)), kMainStatusH};
}

inline int MainTableGridY(int card_y) { return MainStatusY(card_y) + kMainStatusH + kMainHeaderGap; }

struct RegionSegment {
    RectI nationwide;
    RectI gyeongbuk;
};

inline RegionSegment SplitRegionSegment(const RectI& slot) {
    const int left_w = (std::max)(1, slot.w / 2);
    return {{slot.x, slot.y, left_w, slot.h}, {slot.x + left_w, slot.y, slot.w - left_w, slot.h}};
}

inline int HitRegionSegment(const RegionSegment& seg, int x, int y) {
    if (seg.nationwide.Contains(x, y)) {
        return 0;
    }
    if (seg.gyeongbuk.Contains(x, y)) {
        return 1;
    }
    return -1;
}

inline const wchar_t* RegionLabelAt(int index) {
    return index == 1 ? kRegionGyeongbuk : kRegionNationwide;
}

inline const wchar_t* ChartTabLabelAt(int index) {
    if (index == kChartTabHist) {
        return kChartTabHistLabel;
    }
    return kChartTabMapLabel;
}

inline bool ChartShowsScatter(int tab) { return tab == kChartTabMap; }
inline bool ChartShowsHistogram(int tab) { return tab == kChartTabHist; }
inline bool ChartShowsDesign(int tab) {
    (void)tab;
    return false;
}
inline bool ChartShowsMapToolbar(int tab) { return tab == kChartTabMap || tab == kChartTabHist; }
inline bool ChartPageUsesFullCard(int tab) { return tab == kChartTabHist; }
inline bool ChartDesignOpensWindow() { return true; }

inline int RegionIndexFromToken(const std::string& token) {
    return token == "경북" ? 1 : 0;
}

inline MainToolbar LayoutMainToolbar(int client_w, int card_y) {
    MainToolbar t;
    const int y = MainToolbarY(card_y);
    const int shift = static_cast<int>(std::lround(static_cast<double>(client_w) * static_cast<double>(kMainToolbarShift)));
    const int right = client_w - kMainCardInset - kMainTablePadX - shift;
    t.chart = {right - kMainChartW, y, kMainChartW, kMainBtnH};
    t.design = {0, 0, 0, 0};
    t.stop = {t.chart.x - kMainBtnGap - kMainStopW, y, kMainStopW, kMainBtnH};
    t.update = {t.stop.x - kMainBtnGap - kMainUpdateW, y, kMainUpdateW, kMainBtnH};
    t.region_combo = {t.update.x - kMainComboGap - kMainRegionComboW, y, kMainRegionComboW, kMainBtnH};
    t.region_label = {t.region_combo.x - kMainRegionLabelGap - kMainRegionLabelW, y, kMainRegionLabelW, kMainBtnH};
    return t;
}

struct MainKpiRow {
    RectI notices;
    RectI latest;
    RectI dong_wins;
    RectI dong_median;
};

inline MainKpiRow LayoutMainKpis(int client_w) {
    MainKpiRow row;
    const int x = kMainCardInset + kMainTablePadX;
    const int inner = (std::max)(160, client_w - 2 * x);
    const int w = (std::max)(72, (inner - 3 * kMainKpiGap) / 4);
    row.notices = {x, kMainKpiY, w, kMainKpiH};
    row.latest = {row.notices.Right() + kMainKpiGap, kMainKpiY, w, kMainKpiH};
    row.dong_wins = {row.latest.Right() + kMainKpiGap, kMainKpiY, w, kMainKpiH};
    row.dong_median = {row.dong_wins.Right() + kMainKpiGap, kMainKpiY, w, kMainKpiH};
    return row;
}

struct MainIntel {
    RectI win_bars;
    RectI houses;
    RectI dong;
};

inline MainIntel LayoutMainIntel(int client_w) {
    MainIntel row;
    const int x = kMainCardInset + kMainTablePadX;
    const int inner = (std::max)(240, client_w - 2 * x);
    const int left = (inner * 54) / 100;
    const int right = (inner * 22) / 100;
    const int mid = inner - left - right - 2 * kMainKpiGap;
    row.win_bars = {x, kMainIntelY, left, kMainIntelH};
    row.houses = {row.win_bars.Right() + kMainKpiGap, kMainIntelY, mid, kMainIntelH};
    row.dong = {row.houses.Right() + kMainKpiGap, kMainIntelY, right, kMainIntelH};
    return row;
}

inline int MainTableSlotH(int client_h) {
    return (std::max)(kMainTableMinH, client_h * kMainTableFracNum / kMainTableFracDen);
}

struct MainMosaic {
    RectI brand;
    MainKpiRow kpis;
    MainToolbar toolbar;
    RectI hist;
    RectI rank;
    RectI month;
    RectI donut;
    RectI line;
    RectI gap;
    RectI watch;
    RectI winners;
    RectI firms;
    RectI table;
    RectI table_grid;
    RectI replay;
};

inline int FirmBoardHeaderH() { return 50; }

struct FirmGrid {
    RectI box;
    int n = 0;
    int cols = 0;
    int rows = 0;
    int cell_w = 0;
    int cell_h = 0;
};

inline FirmGrid LayoutFirmGrid(const RectI& box, int n) {
    FirmGrid g;
    g.box = box;
    g.n = (std::max)(0, n);
    if (g.n <= 0 || box.w < 8 || box.h < 8) {
        return g;
    }
    int cols = 6;
    if (g.n > 18) {
        cols = 8;
    }
    if (g.n > 40) {
        cols = 10;
    }
    if (g.n > 70) {
        cols = 12;
    }
    int rows = (g.n + cols - 1) / cols;
    int cw = box.w / cols;
    int rh = kMainFirmCellH;
    while (rows * rh > box.h && cols < 16) {
        cols += 1;
        rows = (g.n + cols - 1) / cols;
        cw = box.w / cols;
    }
    if (rows * rh > box.h) {
        rh = (std::max)(1, box.h / rows);
    }
    if (cw < 1) {
        cw = 1;
    }
    g.cols = cols;
    g.rows = rows;
    g.cell_w = cw;
    g.cell_h = rh;
    return g;
}

inline RectI FirmCellRect(const FirmGrid& g, int i) {
    if (g.cols <= 0 || i < 0 || i >= g.n) {
        return {};
    }
    const int r = i / g.cols;
    const int c = i % g.cols;
    return {g.box.x + c * g.cell_w, g.box.y + r * g.cell_h, g.cell_w, g.cell_h};
}

inline MainMosaic LayoutMainMosaic(int client_w, int client_h, int firm_n = 0) {
    MainMosaic m;
    const int x = kMainCardInset + kMainTablePadX;
    const int inner = (std::max)(240, client_w - 2 * x);
    m.toolbar = LayoutMainToolbar(client_w, kMainTopY - kMainTablePadTop);
    m.brand = {x, kMainTopY, kMainBrandW, kMainTopH};
    const int kpi_right = m.toolbar.region_label.x - kMainKpiGap;
    const int kpi_left = m.brand.Right() + kMainKpiGap;
    const int kpi_inner = (std::max)(80, kpi_right - kpi_left);
    const int kpi_w = (std::max)(64, (kpi_inner - 3 * kMainKpiGap) / 4);
    m.kpis.notices = {kpi_left, kMainTopY + 2, kpi_w, kMainKpiH};
    m.kpis.latest = {m.kpis.notices.Right() + kMainKpiGap, kMainTopY + 2, kpi_w, kMainKpiH};
    m.kpis.dong_wins = {m.kpis.latest.Right() + kMainKpiGap, kMainTopY + 2, kpi_w, kMainKpiH};
    m.kpis.dong_median = {m.kpis.dong_wins.Right() + kMainKpiGap, kMainTopY + 2, kpi_w, kMainKpiH};
    const int firms_y = kMainTopY + kMainTopH + kMainMosaicGap;
    const int max_firms_h =
        (std::max)(FirmBoardHeaderH(), client_h - kMainCardInset - kMainTableMinH - kMainMosaicGap - firms_y);
    const int board_w = (std::max)(8, inner - 16);
    const int board_max = (std::max)(8, max_firms_h - FirmBoardHeaderH());
    const auto grid = LayoutFirmGrid({0, 0, board_w, board_max}, (std::max)(0, firm_n));
    int firms_h = FirmBoardHeaderH();
    if (firm_n > 0 && grid.rows > 0 && grid.cell_h > 0) {
        firms_h = FirmBoardHeaderH() + grid.rows * grid.cell_h + 8;
    }
    if (firms_h > max_firms_h) {
        firms_h = max_firms_h;
    }
    m.hist = {};
    m.rank = {};
    m.month = {};
    m.donut = {};
    m.line = {};
    m.gap = {};
    m.watch = {};
    m.winners = {};
    m.firms = {x, firms_y, inner, firms_h};
    const int table_y = m.firms.Bottom() + kMainMosaicGap;
    const int table_h = (std::max)(kMainTableMinH, client_h - kMainCardInset - table_y);
    m.table = {x, table_y, inner, table_h};
    m.replay = {m.table.x + 8, m.table.Bottom() - kMainReplayH - 6, m.table.w - 16, kMainReplayH};
    m.table_grid = {m.table.x + 8, m.table.y + 34, m.table.w - 16,
                    (std::max)(24, m.replay.y - 4 - (m.table.y + 34))};
    return m;
}

inline RectI CompanyAllButtonRect(const RectI& company_pane) {
    return {company_pane.Right() - kCompanyAllW, company_pane.y + (kCompanyHeaderH - kCompanyAllH) / 2, kCompanyAllW,
            kCompanyAllH};
}

inline int CompanyHeaderLabelY(int pane_y) {
    return pane_y + (kCompanyHeaderH - static_cast<int>(kFontEyebrow)) / 2;
}

struct ChartChrome {
    RectI window;
    RectI accent;
    RectI content;
    RectI map_card;
    RectI map_inner;
    RectI tab_strip;
    RectI tab_map;
    RectI tab_design;
    RectI tab_hist;
    RectI plot;
    RectI hist_plot;
    RectI reset_toolbar;
    RectI reset_btn;
    RectI rank1_btn;
    RectI design_btn;
    RectI dock;
    RectI dock_inner;
    RectI range_pane;
    RectI divider;
    RectI company_pane;
    RectI company_all;
    RectI company_wrap;
    RectI combo_period;
    RectI combo_precision;
    RectI combo_region;
    RectI combo_min;
    RectI combo_max;
};

inline int RangeFieldsH() {
    const int field = kCaptionH + kComboH;
    return static_cast<int>(kFontEyebrow) + kEyebrowGap + field * 3 + kFieldGap * 2 + 8;
}

inline ChartChrome LayoutChartChrome(int client_w, int client_h, int company_count = 0) {
    ChartChrome c;
    c.window = {0, 0, client_w, client_h};
    c.accent = {0, 0, client_w, static_cast<int>(kAccentBarH)};
    c.content.x = kContentML;
    c.content.y = c.accent.h + kContentMT;
    c.content.w = (std::max)(1, client_w - kContentML - kContentMR);
    c.content.h = (std::max)(1, client_h - c.content.y - kContentMB);

    const int company_w = (std::max)(80, c.content.w - kLeftPanelW - kColGap - kDividerW - kColGap);
    const int wrap_inner = (std::max)(1, company_w - 2 * kCompanyPadX);
    const int wrap_rows = CompanyWrapRowCount(company_count, wrap_inner);
    const int wrap_h = (std::max)(kCompanyWrapMinH, wrap_rows * kCompanyRowH);
    const int inner_h =
        (std::max)(kDockMinH, (std::max)(RangeFieldsH(), kCompanyHeaderH + kFieldGap + wrap_h));
    c.dock.h = kDockPadY + inner_h + kDockPadY;
    c.dock.w = c.content.w;
    c.dock.x = c.content.x;
    c.dock.y = c.content.y + c.content.h - c.dock.h;

    c.map_card.x = c.content.x;
    c.map_card.y = c.content.y;
    c.map_card.w = c.content.w;
    c.map_card.h = (std::max)(80, c.dock.y - kDockGap - c.map_card.y);

    c.map_inner.x = c.map_card.x + kCardPad + kCardInner;
    c.map_inner.y = c.map_card.y + kCardPad + kCardInner;
    c.map_inner.w = (std::max)(40, c.map_card.w - 2 * (kCardPad + kCardInner));
    c.map_inner.h = (std::max)(40, c.map_card.h - 2 * (kCardPad + kCardInner));

    c.tab_map = {c.map_inner.x + kChartTabPad, c.map_inner.y + kChartTabPad, kChartTabW, kChartTabH};
    c.tab_design = {0, 0, 0, 0};
    c.tab_hist = {c.tab_map.Right() + kChartTabGap, c.tab_map.y, kChartTabW, kChartTabH};
    c.tab_strip = {c.tab_map.x, c.tab_map.y, c.tab_hist.Right() - c.tab_map.x, kChartTabH};

    c.plot.x = c.map_inner.x + kPlotLeft;
    c.plot.y = c.tab_map.Bottom() + kPlotTop;
    c.plot.w = (std::max)(120, c.map_inner.w - kPlotLeft - kPlotRight);
    c.plot.h = (std::max)(120, c.map_inner.h - kPlotBottom - (c.plot.y - c.map_inner.y));
    c.hist_plot = {c.map_inner.x + 12, c.plot.y, (std::max)(120, c.map_inner.w - 24), c.plot.h};

    c.reset_toolbar.w = kToolbarPad * 2 + kRank1BtnW + kFieldGap + kResetMinW + kFieldGap + kMapDesignW;
    c.reset_toolbar.h = kToolbarPad * 2 + kResetH;
    c.reset_toolbar.x = c.map_inner.x + c.map_inner.w - kResetMarginRight - c.reset_toolbar.w;
    c.reset_toolbar.y = c.map_inner.y + kResetMarginTop;
    c.rank1_btn.x = c.reset_toolbar.x + kToolbarPad;
    c.rank1_btn.y = c.reset_toolbar.y + kToolbarPad;
    c.rank1_btn.w = kRank1BtnW;
    c.rank1_btn.h = kRank1BtnH;
    c.reset_btn.x = c.rank1_btn.Right() + kFieldGap;
    c.reset_btn.y = c.reset_toolbar.y + kToolbarPad;
    c.reset_btn.w = kResetMinW;
    c.reset_btn.h = kResetH;
    c.design_btn.x = c.reset_btn.Right() + kFieldGap;
    c.design_btn.y = c.reset_toolbar.y + kToolbarPad;
    c.design_btn.w = kMapDesignW;
    c.design_btn.h = kResetH;

    c.dock_inner.x = c.dock.x + kDockPadX;
    c.dock_inner.y = c.dock.y + kDockPadY;
    c.dock_inner.w = (std::max)(1, c.dock.w - 2 * kDockPadX);
    c.dock_inner.h = inner_h;

    c.range_pane = {c.dock_inner.x, c.dock_inner.y, kLeftPanelW, c.dock_inner.h};
    c.divider = {c.range_pane.Right() + kColGap, c.dock_inner.y + 6, kDividerW, (std::max)(1, c.dock_inner.h - 12)};
    c.company_pane.x = c.divider.Right() + kColGap;
    c.company_pane.y = c.dock_inner.y;
    c.company_pane.w = (std::max)(80, c.dock_inner.Right() - c.company_pane.x);
    c.company_pane.h = c.dock_inner.h;

    c.company_all = CompanyAllButtonRect(c.company_pane);
    c.company_wrap.x = c.company_pane.x;
    c.company_wrap.y = c.company_pane.y + kCompanyHeaderH + kFieldGap;
    c.company_wrap.w = c.company_pane.w;
    c.company_wrap.h = (std::max)(kCompanyWrapMinH, c.company_pane.Bottom() - c.company_wrap.y);

    const int half = (kLeftPanelW - kFieldGap) / 2;
    int fy = c.range_pane.y + static_cast<int>(kFontEyebrow) + kEyebrowGap;
    c.combo_period = {c.range_pane.x, fy + kCaptionH, half, kComboH};
    c.combo_precision = {c.range_pane.x + half + kFieldGap, fy + kCaptionH, half, kComboH};
    fy += kCaptionH + kComboH + kFieldGap;
    c.combo_region = {c.range_pane.x, fy + kCaptionH, kLeftPanelW, kComboH};
    fy += kCaptionH + kComboH + kFieldGap + 8;
    c.combo_min = {c.range_pane.x, fy + kCaptionH, half, kComboH};
    c.combo_max = {c.range_pane.x + half + kFieldGap, fy + kCaptionH, half, kComboH};
    return c;
}

struct DesignPrinciplesChrome {
    RectI window;
    RectI accent;
    RectI content;
    RectI card;
    RectI page;
    RectI dock;
    RectI company_pane;
    RectI range_pane;
};

inline DesignPrinciplesChrome LayoutDesignPrinciplesChrome(int client_w, int client_h) {
    DesignPrinciplesChrome c;
    c.window = {0, 0, client_w, client_h};
    c.accent = {0, 0, client_w, static_cast<int>(kAccentBarH)};
    c.card = {0, c.accent.h, client_w, (std::max)(1, client_h - c.accent.h)};
    c.content = c.card;
    c.dock = {0, 0, 0, 0};
    c.company_pane = {0, 0, 0, 0};
    c.range_pane = {0, 0, 0, 0};
    c.page.x = kPrinciplePad;
    c.page.y = c.accent.h + 4;
    c.page.w = (std::max)(40, client_w - 2 * kPrinciplePad);
    c.page.h = (std::max)(40, client_h - c.page.y - kPrinciplePad);
    return c;
}

inline RectI BandLegendRect(const ChartChrome& c) {
    const int pad = kBandStatusPad;
    const int x = c.tab_hist.Right() + pad;
    const int right = c.reset_toolbar.x - pad;
    const int w = (std::max)(0, right - x);
    return {x, c.tab_map.y, w, c.tab_map.h};
}

inline RectI BandRank1GutterRect(const ChartChrome& c) {
    const int y = (std::max)(c.plot.y, c.reset_toolbar.Bottom() + 4);
    const int h = (std::max)(0, c.plot.Bottom() - y);
    return {c.plot.Right(), y, kBandRank1GutterW, h};
}

inline RectI CompanyCheckBoxRect(const RectI& wrap, int index, int count) {
    int col = 0;
    int row = 0;
    int cols = 1;
    const int inner_w = (std::max)(1, wrap.w - 2 * kCompanyPadX);
    const int rows = CompanyWrapRowCount(count, inner_w);
    UniformGridCell(index, count, rows, col, row, cols);
    const int inner_h = (std::max)(1, wrap.h - 2 * kCompanyPadY);
    const int cw = inner_w / (std::max)(1, cols);
    const int rh = inner_h / (std::max)(1, rows);
    const RectI cell{wrap.x + kCompanyPadX + col * cw + 1, wrap.y + kCompanyPadY + row * rh + 1, (std::max)(kCheckSize, cw - 7),
                     (std::max)(kCheckSize, rh - 2)};
    return {cell.x, cell.y + (cell.h - kCheckSize) / 2, kCheckSize, kCheckSize};
}

inline bool CompanyChipVisible(bool any_checked, bool this_checked) {
    if (!any_checked) {
        return true;
    }
    return this_checked;
}

inline bool ChipPassesRank1Filter(bool rank1_only, bool is_rank1) {
    if (!rank1_only) {
        return true;
    }
    return is_rank1;
}

inline void ApplyCompanyClick(std::vector<char>& checked, int index, bool click_all) {
    if (checked.empty()) {
        return;
    }
    if (click_all) {
        std::fill(checked.begin(), checked.end(), 0);
        return;
    }
    if (index < 0 || index >= static_cast<int>(checked.size())) {
        return;
    }
    checked[static_cast<size_t>(index)] = checked[static_cast<size_t>(index)] ? 0 : 1;
}

inline void ResetMapFilters(std::vector<char>& checked) { ApplyCompanyClick(checked, -1, true); }

inline RectI CompanyCheckCellRect(const RectI& wrap, int index, int count) {
    int col = 0;
    int row = 0;
    int cols = 1;
    const int inner_w = (std::max)(1, wrap.w - 2 * kCompanyPadX);
    const int rows = CompanyWrapRowCount(count, inner_w);
    UniformGridCell(index, count, rows, col, row, cols);
    const int inner_h = (std::max)(1, wrap.h - 2 * kCompanyPadY);
    const int cw = inner_w / (std::max)(1, cols);
    const int rh = inner_h / (std::max)(1, rows);
    return {wrap.x + kCompanyPadX + col * cw + 1, wrap.y + kCompanyPadY + row * rh + 1, (std::max)(8, cw - 7),
            (std::max)(8, rh - 2)};
}

inline RectI ChartTabRect(const ChartChrome& chrome, int index) {
    if (index == kChartTabHist) {
        return chrome.tab_hist;
    }
    return chrome.tab_map;
}

inline int HitChartTab(const ChartChrome& chrome, int x, int y) {
    if (chrome.tab_map.Contains(x, y)) {
        return kChartTabMap;
    }
    if (chrome.tab_hist.Contains(x, y)) {
        return kChartTabHist;
    }
    return -1;
}

}  // namespace chart_theme
}  // namespace nara
