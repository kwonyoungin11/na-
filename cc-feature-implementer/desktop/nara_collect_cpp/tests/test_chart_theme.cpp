#include "nara/chart_theme.h"

#include <cmath>
#include <iostream>
#include <string>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    using namespace nara::chart_theme;

    if (kCellPx != 20) {
        return Fail("C# GetScatterCellSize step 0.01 is 20px");
    }
    if (std::fabs(DpiScaleFromDpi(96) - 1.0f) > 0.001f || std::fabs(DpiScaleFromDpi(144) - 1.5f) > 0.001f) {
        return Fail("WPF PixelsPerDip: 96->1.0, 144->1.5");
    }
    if (DipToPx(20, 1.5f) != 30 || DipToPx(8, 1.5f) != 12 || PxToDip(30, 1.5f) != 20) {
        return Fail("150% DPI: 20dip cell is 30px, 8dip Hangul is 12px");
    }
    if (ScatterColumnCount(99.0, 101.0, 0.01) != 201) {
        return Fail("99.0-101.0 step 0.01 must be 201 columns");
    }
    if (kPlotLeft != 220 || kPlotTop != 16 || kPlotRight != 22 || kPlotBottom != 62) {
        return Fail("plot inset must be left 220 top 16 right 22 bottom 62 (1위 % between ticks)");
    }
    if (std::wstring(kAxisRank1Pct) != L"1위 (%)") {
        return Fail("axis row label is 1위 (%)");
    }
    if (kDonggukArgb != 0xFFFF0000u) {
        return Fail("동국 fill is bidder_colors #FF0000");
    }
    if (kRankGoldArgb != 0xFFFACC15u) {
        return Fail("rank-1 gold border is #FACC15");
    }
    if (kRankGoldOuterArgb != 0xFFCA8A04u) {
        return Fail("rank-1 outer gold is #CA8A04");
    }
    if (kRankBadgeArgb != 0xFFC62828u) {
        return Fail("rank badge is #C62828");
    }
    if (kGridArgb != 0xFFD9DEE7u || kMajorGridArgb != 0xFFBFC7D4u || kAxisArgb != 0xFF5E6A7Du) {
        return Fail("grid/axis pens must match NativeBidChartControl");
    }
    if (kBgArgb != 0xFFF6F8FBu || kPlotBgArgb != 0xFFFFFFFFu) {
        return Fail("chart background #F6F8FB plot #FFFFFF");
    }
    if (kCardRadius != 22 || kDockRadius != 16 || kToolbarRadius != 16 || kResetBtnRadius != 14) {
        return Fail("card 22 / dock 16 / toolbar 16 / reset 14");
    }
    if (kMapInnerRadius != 16 || kPlotRadius != 16) {
        return Fail("전장 맵 외각 map_inner/plot radius 16");
    }
    if (kMapInnerRadius > kCardRadius || kPlotRadius > kMapInnerRadius) {
        return Fail("plot 16 sits inside map_inner 16 inside card 22");
    }
    if (kHoverRadius != 6 || kRankBadgeRadius != 2 || kCompanyPanelRadius != 12) {
        return Fail("hover 6 / rank badge 2 / company panel 12");
    }
    if (kChipRadius != 0) {
        return Fail("C# DrawScatter chips are DrawRectangle, not rounded");
    }
    if (std::fabs(ChipRadiusPx(20)) > 0.01f || std::fabs(ChipRadiusPx(40)) > 0.01f) {
        return Fail("chip radius stays 0 at every zoom like C# DrawRectangle");
    }
    if (DarkenArgb(0xFF21252Bu, 0.10f) != 0xFF1E2127u) {
        return Fail("#21252B * 0.90 = #1E2127");
    }
    if (ChipOutlineArgb() != kChipBorderArgb || kChipBorderArgb != 0xFF21252Bu) {
        return Fail("C# BlockBorderPen is #21252B");
    }
    if (!ChipLabelStrokeBehindFill()) {
        return Fail("GDI+ chip glyphs fill over outline so 0.4px pen does not eat Hangul");
    }
    if (kTextRenderingHintIdeal != 4) {
        return Fail("WPF Ideal maps to GDI+ TextRenderingHintAntiAlias");
    }
    if (!kTextUsesDirectWrite) {
        return Fail("C# FormattedText is DirectWrite; chip/UI text must not stay on GDI+ AddString");
    }
    if (BlurShadowLayerCount(22) < 4 || BlurShadowLayerCount(16) < 3) {
        return Fail("C# DropShadowEffect blur is layered, not a hard offset");
    }
    if (kChipLabelOutlineArgb != 0x59000000u) {
        return Fail("chip label outline matches C# #59000000");
    }
    if (std::fabs(kChipLabelOutlineWidth - 0.4f) > 0.01f) {
        return Fail("chip label outline is 0.4px at 20px cell like C#");
    }
    if (std::fabs(ChipLabelOutlineWidthPx(20) - 0.4f) > 0.01f) {
        return Fail("20px cell chip label outline is 0.4px");
    }
    if (std::fabs(ChipLabelOutlineWidthPx(40) - 0.8f) > 0.01f) {
        return Fail("chip label outline scales with zoom like chip font");
    }
    if (kRank1BtnW != 72 || kRank1BtnH != 40) {
        return Fail("1위 overlay is 72x40 in plot center");
    }
    if (std::wstring(kRank1Label) != L"1위") {
        return Fail("center overlay label is 1위");
    }
    if (ChipPassesRank1Filter(false, false) != true || ChipPassesRank1Filter(true, false) != false
        || ChipPassesRank1Filter(true, true) != true) {
        return Fail("1위 toggle hides non-rank1 chips only when on");
    }
    if (std::fabs(kFontAxis - 10.0f) > 0.01f || std::fabs(kFontEyebrow - 11.0f) > 0.01f
        || std::fabs(kFontCaption - 12.0f) > 0.01f || std::fabs(kFontReset - 13.0f) > 0.01f
        || std::fabs(kFontCompany - 11.0f) > 0.01f || std::fabs(kFontHover - 9.8f) > 0.01f
        || std::fabs(kFontEmpty - 14.0f) > 0.01f) {
        return Fail("font sizes must match NativeBidChartControl / DesignTokens");
    }
    if (std::fabs(ChipLabelFontPx(20, 2) - 8.0f) > 0.05f) {
        return Fail("20px cell 2-char chip font clamps to 8px");
    }
    if (ChipLabelFontPx(20, 2) < kFontChipMin || ChipLabelFontPx(20, 2) > kFontChipMax) {
        return Fail("chip font stays in 4-11");
    }
    if (std::fabs(ChipLabelScreenPx(20, 2) - ChipLabelFontPx(20, 2)) > 0.05f) {
        return Fail("scale 1 chip screen font equals the 20px world font");
    }
    if (std::fabs(ChipLabelScreenPx(40, 2) - 2.0f * ChipLabelFontPx(20, 2)) > 0.05f) {
        return Fail("C# ScaleTransform: zoom 2x must double chip font, not cap at 11px");
    }
    if (ChipLabelScreenPx(40, 2) <= ChipLabelScreenPx(20, 2)) {
        return Fail("줌인 must not shrink chip font");
    }
    const auto y = FormatNoticeYLabel("R26BK01673928-000", "2026-02-20 11:00:00");
    if (y != "R26BK01673928-000 (26-02-20 11:00)") {
        std::cerr << "FAIL: Y label got " << y << "\n";
        return 1;
    }
    const auto y_midnight = FormatNoticeYLabel("N-1", "2026-08-01");
    if (y_midnight != "N-1 (26-08-01 00:00)") {
        std::cerr << "FAIL: date-only Y label got " << y_midnight << "\n";
        return 1;
    }
    int col = 0;
    int row = 0;
    int cols = 0;
    UniformGridCell(5, 20, 5, col, row, cols);
    if (cols != 4 || col != 1 || row != 1) {
        return Fail("업체 wrap is UniformGrid Rows=5 row-major");
    }
    if (kCompanyMinCellW < 80) {
        return Fail("업체 cell must be wide enough for 2+ hangul");
    }
    if (CompanyWrapRowCount(20, 1200) != 5) {
        return Fail("few companies stay on 5 wrap rows");
    }
    if (CompanyWrapRowCount(120, 900) <= 5) {
        return Fail("many companies add wrap rows instead of clipping names to 1 glyph");
    }
    const RectI wrap{0, 0, 1100, 400};
    const auto crowded = CompanyCheckCellRect(wrap, 0, 120);
    if (crowded.w - kCheckSize - kCheckGap < 28) {
        return Fail("crowded 업체 wrap must keep room for 2+ hangul names");
    }
    if (std::wstring(kResetLabel) != L"맵 초기화" || std::wstring(kRangeLabel) != L"분석 범위"
        || std::wstring(kPeriodLabel) != L"기간" || std::wstring(kPrecisionLabel) != L"정밀도"
        || std::wstring(kRegionLabel) != L"지역" || std::wstring(kMinLabel) != L"최소"
        || std::wstring(kMaxLabel) != L"최대" || std::wstring(kCompanyLabel) != L"업체"
        || std::wstring(kCompanyAllLabel) != L"전체") {
        return Fail("Korean dock/reset labels must match ChartPopupWindow.xaml");
    }
    if (kResetH != 40 || kResetMinW != 124 || kDockMinH != 134) {
        return Fail("reset 40x124 and dock min 134 (20% shorter so the map is larger)");
    }
    if (kComboH != 32 || kComboRadius != 12) {
        return Fail("분석 범위 combo is 32px (20% shorter dock) radius 12");
    }
    if (kRegionSegmentRadius != kComboRadius) {
        return Fail("전국|경북 is one 12px shell matching combo, not two overlapping pills");
    }
    if (std::fabs(kComboStrokeW - 1.2f) > 0.01f || kComboStrokeArgb != kStrokeStrongArgb) {
        return Fail("분석 범위 cell stroke is 1.2 #BAC6D5 so the 칸 edge is visible");
    }
    if (kCheckSize != 18 || kCheckRadius != 6 || kCheckGap != 10) {
        return Fail("업체 checkbox is 18x18 radius 6 gap 10");
    }
    if (kCardPad != 10 || kCardInner != 2 || kCardRadius != 22 || kCardStrokeW != 1.0f) {
        return Fail("map card pad 10 inner 2 radius 22 stroke 1");
    }
    if (kCardFillArgb != 0xFFFFFFFFu || kCardStrokeArgb != 0xFFD8E0EAu || kCardShadowArgb != 0x14000000u
        || kCardShadowDepth != 4 || kCardShadowBlur != 22) {
        return Fail("map card fill #FFFFFF stroke #D8E0EA shadow 8% black depth 4 blur 22");
    }
    if (kDockPadX != 14 || kDockPadY != 10 || kDockRadius != 16 || kEyebrowGap != 6) {
        return Fail("dock pad 14,10 radius 16 eyebrow gap 6");
    }
    if (kAccentStrongArgb != 0xFFB45309u || std::fabs(kFontEyebrow - 11.0f) > 0.01f) {
        return Fail("eyebrow 분석 범위 is 11px #B45309");
    }
    if (kResetShadowDepth != 4 || kResetShadowBlur != 16 || kToolbarShadowDepth != 6 || kToolbarShadowBlur != 22
        || kToolbarPad != 8 || kResetBtnRadius != 14 || kToolbarRadius != 16) {
        return Fail("reset overlay pill shadow/radius must match MapResetButtonStyle");
    }
    if (kGlassFillArgb != 0xD6FFFFFFu || kResetShadowArgb != 0x2E081018u) {
        return Fail("reset glass #D6FFFFFF shadow #081018 @ 18%");
    }
    if (kCompanyPadX != 8 || kCompanyPadY != 6 || kCompanyWrapMinH != 102 || kCompanyPanelRadius != 12) {
        return Fail("업체 wrap pad 8,6 minHeight 102 radius 12");
    }
    if (!ChipPaintBefore(false, false, 8, true, false, 54)) {
        return Fail("동국 paints last so non-동국 comes before");
    }

    const auto chrome = LayoutChartChrome(1920, 1080);
    if (chrome.map_card.w <= 0 || chrome.map_card.h <= 0) {
        return Fail("map card must have size");
    }
    if (chrome.map_card.Bottom() + kDockGap != chrome.dock.y) {
        return Fail("map card and dock are stacked with gap 10, no overlap");
    }
    if (chrome.dock_inner.h < kDockMinH) {
        return Fail("dock inner minHeight 134");
    }
    if (chrome.combo_period.h != 32 || chrome.combo_precision.h != 32 || chrome.combo_region.h != 32
        || chrome.combo_min.h != 32 || chrome.combo_max.h != 32) {
        return Fail("dock combos are 32px");
    }
    if (chrome.dock.h > 200) {
        return Fail("dock height must drop ~20% so the scatter map is larger");
    }
    if (chrome.reset_btn.w != kResetMinW || chrome.reset_btn.h != kResetH) {
        return Fail("맵 초기화 overlay pill is 124x40");
    }
    if (chrome.reset_toolbar.y < chrome.map_inner.y || chrome.reset_toolbar.Right() > chrome.map_inner.Right()
        || chrome.reset_toolbar.x < chrome.map_inner.x) {
        return Fail("맵 초기화 overlay stays inside the map inner card");
    }
    if (chrome.reset_btn.y != chrome.reset_toolbar.y + kToolbarPad) {
        return Fail("reset pill sits in toolbar pad 8");
    }
    if (chrome.rank1_btn.w != kRank1BtnW || chrome.rank1_btn.h != kRank1BtnH) {
        return Fail("1위 button size");
    }
    if (chrome.reset_toolbar.w
        != kToolbarPad * 2 + kRank1BtnW + kFieldGap + kResetMinW + kFieldGap + kMapDesignW) {
        return Fail("toolbar holds 1위, 맵 초기화, then 설계원칙");
    }
    if (chrome.rank1_btn.x != chrome.reset_toolbar.x + kToolbarPad
        || chrome.rank1_btn.y != chrome.reset_toolbar.y + kToolbarPad) {
        return Fail("1위 pill sits in the window toolbar, not on the plot");
    }
    if (chrome.reset_btn.x != chrome.rank1_btn.Right() + kFieldGap) {
        return Fail("맵 초기화 sits to the right of 1위");
    }
    if (chrome.design_btn.w != kMapDesignW || chrome.design_btn.h != kResetH
        || chrome.design_btn.x != chrome.reset_btn.Right() + kFieldGap
        || chrome.design_btn.y != chrome.reset_btn.y || chrome.design_btn.Right() <= chrome.reset_btn.Right()
        || chrome.reset_toolbar.Right() - kToolbarPad != chrome.design_btn.Right()) {
        return Fail("설계원칙 is the rightmost map pill, after 맵 초기화");
    }
    if (std::wstring(kMapDesignLabel) != L"설계원칙" || kMapDesignFillArgb != 0xFF2563EBu
        || kMapDesignTextArgb != 0xFFFFFFFFu) {
        return Fail("rightmost map pill is blue 설계원칙");
    }
    if (!chrome.map_inner.Contains(chrome.rank1_btn.x + 1, chrome.rank1_btn.y + 1)) {
        return Fail("1위 stays inside the map window card");
    }
    if (chrome.rank1_btn.x == chrome.plot.x + (chrome.plot.w - kRank1BtnW) / 2
        && chrome.rank1_btn.y == chrome.plot.y + (chrome.plot.h - kRank1BtnH) / 2) {
        return Fail("1위 must not sit on the scatter center");
    }
    if (chrome.company_wrap.h < kCompanyWrapMinH) {
        return Fail("업체 wrap minHeight 102");
    }
    if (chrome.plot.x != chrome.map_inner.x + kPlotLeft) {
        return Fail("plot is the inner host rect, not the full popup");
    }
    if (chrome.plot.y != chrome.tab_map.Bottom() + kPlotTop) {
        return Fail("plot sits under the tabs with the original 16dip title inset");
    }
    if (chrome.plot.y < chrome.tab_strip.Bottom() + static_cast<int>(kFontAxis)) {
        return Fail("plot must leave room so 공고(개찰일시) does not run through 전장맵");
    }
    const int title_y = AxisNoticeTitleY(chrome.tab_strip.Bottom(), chrome.plot.y);
    if (title_y < chrome.tab_strip.Bottom() || title_y + static_cast<int>(kFontAxis) > chrome.plot.y) {
        return Fail("axis title sits between the tabs and the plot");
    }
    if (NoticeYLabelFits(chrome.tab_map.y, kCellPx, chrome.plot.y, chrome.plot.Bottom(), chrome.tab_strip.Bottom())) {
        return Fail("Y labels that cross 전장맵 must be skipped");
    }
    if (!NoticeYLabelFits(chrome.plot.y, kCellPx, chrome.plot.y, chrome.plot.Bottom(), chrome.tab_strip.Bottom())) {
        return Fail("Y labels starting at the plot top are visible");
    }
    const int newest_y = chrome.plot.Bottom() - (5 + 1) * kCellPx;
    if (!NoticeYLabelFits(newest_y, kCellPx, chrome.plot.y, chrome.plot.Bottom(), chrome.tab_strip.Bottom())) {
        return Fail("newest notice Y label at the bottom of the latest view must stay");
    }
    const auto one = LayoutChipDodge(0, 1, 20);
    if (one.side != 18) {
        return Fail("single chip stays cell-2");
    }
    const auto a = LayoutChipDodge(0, 2, 20);
    const auto b = LayoutChipDodge(1, 2, 20);
    if (ChipDodgeRectsOverlap(a, b)) {
        return Fail("two chips in one 0.01 cell sit side by side, not stacked on 재/륜");
    }
    if (ChipGoldInset() < 1.0f || a.side <= static_cast<int>(ChipGoldInset() * 2.0f)) {
        return Fail("1위 gold stroke stays inside the chip so 국장/국강 do not melt");
    }
    if (!ChipDodgeDrawsLabel(8) || !ChipDodgeDrawsLabel(18)) {
        return Fail("dodged chips keep 2 hangul clipped in the cell; selecting must not drop labels");
    }
    std::vector<char> reset_flags = {1, 0, 1};
    ResetMapFilters(reset_flags);
    if (reset_flags.size() != 3 || reset_flags[0] || reset_flags[1] || reset_flags[2]) {
        return Fail("맵 초기화 clears every 업체 check");
    }
    if (chrome.hist_plot.y != chrome.plot.y || chrome.hist_plot.w <= chrome.plot.w
        || chrome.hist_plot.x < chrome.map_inner.x || chrome.hist_plot.Right() > chrome.map_inner.Right()) {
        return Fail("histogram uses the full map card under the tabs");
    }
    if (kChartTabH != 34 || kChartTabW != 112 || kChartTabGap != 6 || kChartTabPad != 12 || kChartTabCount != 2
        || kChartTabRadius != 12) {
        return Fail("chart tabs are 112x34 radius 12, two beside each other");
    }
    if (kChartTabMap != 0 || kChartTabHist != 1) {
        return Fail("tab indices are 전장맵 0, 히스토그램 1");
    }
    if (std::wstring(kChartTabMapLabel) != L"전장맵" || std::wstring(kChartTabHistLabel) != L"히스토그램"
        || std::wstring(ChartTabLabelAt(0)) != L"전장맵" || std::wstring(ChartTabLabelAt(1)) != L"히스토그램") {
        return Fail("tabs next to the map are 전장맵 then 히스토그램");
    }
    for (int i = 0; i < kChartTabCount; ++i) {
        if (std::wstring(ChartTabLabelAt(i)).find(L"설계원칙") != std::wstring::npos) {
            return Fail("chart popup has no 설계원칙 tab");
        }
    }
    if (ChartShowsScatter(1) || ChartShowsHistogram(0) || ChartShowsDesign(0) || ChartShowsDesign(1)
        || !ChartShowsMapToolbar(1) || !ChartShowsMapToolbar(0) || !ChartPageUsesFullCard(1)
        || !ChartDesignOpensWindow()) {
        return Fail("tabs stay 전장맵 and 히스토그램; the blue 설계원칙 pill opens its window");
    }
    if (std::wstring(kHistRank1Label) != L"1위" || std::wstring(kHistMarketLabel) != L"전체") {
        return Fail("histogram 1위 toggle reuses the 1위 chip filter, not a second tab");
    }
    const int hist_x = chrome.tab_map.Right() + kChartTabGap;
    if (chrome.tab_map.x != chrome.map_inner.x + kChartTabPad || chrome.tab_hist.x != hist_x
        || chrome.tab_map.y != chrome.tab_hist.y || chrome.tab_map.h != kChartTabH
        || ChartTabRect(chrome, 1).x != chrome.tab_hist.x || chrome.tab_design.w != 0) {
        return Fail("히스토그램 sits immediately to the right of 전장맵; no 설계원칙 tab slot");
    }
    if (chrome.tab_strip.w != kChartTabW * 2 + kChartTabGap) {
        return Fail("tab strip spans 전장맵, 히스토그램");
    }
    if (!chrome.map_inner.Contains(chrome.tab_map.x + 1, chrome.tab_map.y + 1)
        || !chrome.map_inner.Contains(chrome.tab_hist.Right() - 1, chrome.tab_hist.Bottom() - 1)) {
        return Fail("chart tabs stay inside the map card");
    }
    if (chrome.tab_hist.Right() >= chrome.reset_toolbar.x) {
        return Fail("tabs must not collide with 1위 / 맵 초기화");
    }
    const auto compact = LayoutChartChrome(kWindowMinW, kWindowMinH);
    if (compact.tab_hist.Right() >= compact.reset_toolbar.x) {
        return Fail("two tabs still clear 1위 / 맵 초기화 at min window");
    }
    if (HitChartTab(chrome, chrome.tab_map.x + 2, chrome.tab_map.y + 2) != 0
        || HitChartTab(chrome, hist_x + 2, chrome.tab_map.y + 2) != 1
        || HitChartTab(chrome, chrome.plot.x + 8, chrome.plot.y + 8) != -1) {
        return Fail("tab hit targets are 전장맵 then 히스토그램");
    }
    if (std::wstring(kWindowTitle).find(L"설계원칙") != std::wstring::npos
        || std::wstring(kMainDesignLabel).find(L"설계원칙") != std::wstring::npos) {
        return Fail("neither window title nor main button names 설계원칙");
    }
    if (kHistogramBinCount != 40 || kHistogramMaxSeries != 8) {
        return Fail("histogram is 40 bins with at most 8 company rows");
    }
    if (kHistMarketLowArgb != 0xFF22D3EEu || kHistMarketMidArgb != 0xFF818CF8u
        || kHistMarketPeakArgb != 0xFFFBBF24u || kHistMarketHighArgb != 0xFFF43F5Eu) {
        return Fail("histogram density is cyan→indigo→amber→rose");
    }
    if (std::fabs(kHistDepthX - 0.55f) > 0.001f || std::fabs(kHistDepthY - 0.32f) > 0.001f) {
        return Fail("3D cabinet depth is 0.55 / 0.32");
    }
    if (LightenArgb(0xFF21252Bu, 0.10f) != 0xFF373B40u) {
        return Fail("#21252B lighten 0.10 is #373B40");
    }
    if (WithAlpha(0xFF818CF8u, 0x66u) != 0x66818CF8u) {
        return Fail("ghost market bars keep the hue and drop alpha");
    }
    if (HistDensityArgb(0.0f) != kHistMarketLowArgb || HistDensityArgb(1.0f) != kHistMarketHighArgb) {
        return Fail("density 0 is cyan and 1 is rose");
    }
    const auto check = CompanyCheckBoxRect(chrome.company_wrap, 5, 20);
    if (check.w != 18 || check.h != 18) {
        return Fail("업체 checkbox cell is 18x18");
    }
    std::vector<char> flags = {0, 0, 0};
    if (!CompanyChipVisible(false, false)) {
        return Fail("no checks must show every chip");
    }
    ApplyCompanyClick(flags, 1, false);
    if (flags[0] || !flags[1] || flags[2]) {
        return Fail("checking one company selects only that company");
    }
    if (CompanyChipVisible(true, false)) {
        return Fail("unchecked company chip must hide when any check exists");
    }
    if (!CompanyChipVisible(true, true)) {
        return Fail("checked company chip must show");
    }
    ApplyCompanyClick(flags, 2, false);
    if (!flags[1] || !flags[2] || flags[0]) {
        return Fail("checking a second company adds it");
    }
    ApplyCompanyClick(flags, -1, true);
    if (flags[0] || flags[1] || flags[2]) {
        return Fail("전체 clears checks so the map shows every chip");
    }

    if (kMainBtnH != 40 || kMainUpdateW != 124 || kMainStopW != 108 || kMainChartW != 132
        || kMainDesignW != 0 || kMainRegionComboW != 148 || kMainBtnGap != 8 || kMainComboGap != 12
        || kMainRegionLabelGap != 8 || kMainBtnRadius != 14 || kMainTablePadTop != 14
        || kMainTablePadX != 16 || kMainCardInset != 20 || kMainHeaderGap != 12) {
        return Fail("main toolbar sizes must match MainWindow.xaml 40px / 124 / 108 / 132 / 148 with no 설계원칙 button");
    }
    if (kCompanyAllW != 64 || kCompanyAllH != 26 || kCompanyHeaderH != 26) {
        return Fail("전체 is GhostButton 64x26 in the 26px 업체 header");
    }
    if (std::fabs(kFontCompanyAll - 13.0f) > 0.01f) {
        return Fail("전체 uses ButtonBaseStyle 13px");
    }
    const auto primary = MainButtonLook(MainButtonKind::Primary);
    if (!primary.gradient || primary.fill0 != 0xFFF59E0Bu || primary.fill2 != 0xFFB45309u
        || primary.text != 0xFFFFFFFFu || primary.border != 0xFFB45309u) {
        return Fail("업데이트 is AccentGradient #F59E0B→#B45309 white text");
    }
    const auto secondary = MainButtonLook(MainButtonKind::Secondary);
    if (secondary.gradient || secondary.fill0 != 0xFFFFFFFFu || secondary.border != 0xFFD8E0EAu
        || secondary.text != 0xFF111111u || secondary.transparent) {
        return Fail("차트 화면 is Secondary white / #D8E0EA / #111111");
    }
    const auto ghost = MainButtonLook(MainButtonKind::Ghost);
    if (!ghost.transparent || ghost.border != 0x00000000u || ghost.text != 0xFF4B5563u) {
        return Fail("중지 idle and 전체 are Ghost: no border, TextMuted #4B5563");
    }
    const auto danger = MainButtonLook(MainButtonKind::Danger);
    if (danger.fill0 != 0xFFFEF2F2u || danger.text != 0xFFDC2626u || danger.border != 0xFFF5C2C7u) {
        return Fail("중지 while collecting is StopCollect danger");
    }
    if (MainStopKind(false) != MainButtonKind::Ghost || MainStopKind(true) != MainButtonKind::Danger) {
        return Fail("중지 is ghost when idle and danger only while collecting");
    }
    if (MainToolbarY(348) != 362) {
        return Fail("toolbar buttons sit on PreviewCard pad-top 14, vertically with the title row");
    }
    if (kMainStatusH != 28 || kMainStatusGap != 8) {
        return Fail("update status banner is 28px under the toolbar");
    }
    if (MainStatusY(348) != 410 || MainTableGridY(348) != 450) {
        return Fail("grid starts after toolbar + status banner so 수집 중 is visible");
    }
    const auto status = LayoutMainStatus(1600, 348);
    if (status.y != 410 || status.h != 28 || status.x != kMainCardInset + kMainTablePadX) {
        return Fail("status banner sits under 업데이트");
    }
    if (kTableCardY != 128 || std::fabs(kMainToolbarShift - 0.20f) > 0.001f || kMainKpiH != 56) {
        return Fail("first screen is a compact KPI strip, table at 128, toolbar shifted 20% left");
    }
    const auto tb1600 = LayoutMainToolbar(1600, kTableCardY);
    if (tb1600.chart.Right() != 1600 - kMainCardInset - kMainTablePadX - 320) {
        return Fail("지역/업데이트 cluster moves 20% of width left");
    }
    const auto kpis = LayoutMainKpis(1600);
    if (kpis.notices.y != 60 || kpis.latest.x <= kpis.notices.x || kpis.period.x <= kpis.latest.x
        || kpis.notices.h != 56) {
        return Fail("three KPI tiles sit above the 낙찰공고 table");
    }
    const auto tb = LayoutMainToolbar(1600, 348);
    if (tb.chart.y != 362 || tb.stop.y != 362 || tb.update.y != 362 || tb.region_combo.y != 362
        || tb.region_label.y != 362) {
        return Fail("지역 combo and 업데이트/중지/차트 화면 share the same centered Y");
    }
    if (tb.chart.h != 40 || tb.stop.h != 40 || tb.update.h != 40 || tb.region_combo.h != 40
        || tb.region_label.h != 40) {
        return Fail("toolbar controls are 40px tall");
    }
    if (tb.update.w != 124 || tb.stop.w != 108 || tb.chart.w != 132 || tb.design.w != 0
        || tb.region_combo.w != 148) {
        return Fail("toolbar widths 124/108/132/148 with empty 설계원칙 slot");
    }
    if (tb.chart.Right() != 1600 - kMainCardInset - kMainTablePadX - 320) {
        return Fail("차트 화면 sits 20% of width left of the card's right pad");
    }
    if (tb.stop.Right() + kMainBtnGap != tb.chart.x || tb.update.Right() + kMainBtnGap != tb.stop.x
        || tb.region_combo.Right() + kMainComboGap != tb.update.x
        || tb.region_label.Right() + kMainRegionLabelGap != tb.region_combo.x) {
        return Fail("toolbar gaps: label 8, combo 12, buttons 8, 중지 left of 차트 화면");
    }
    if (std::wstring(kMainDesignLabel).find(L"설계원칙") != std::wstring::npos) {
        return Fail("main window has no 설계원칙 button");
    }
    if (kRegionSegmentCount != 2 || std::wstring(kRegionNationwide) != L"전국"
        || std::wstring(kRegionGyeongbuk) != L"경북") {
        return Fail("region options are only 전국 and 경북");
    }
    if (std::wstring(kRegionNationwide).find(L"대구") != std::wstring::npos
        || std::wstring(kRegionGyeongbuk).find(L"대구") != std::wstring::npos
        || std::wstring(kRegionGyeongbuk) == L"경상북도") {
        return Fail("no 대구 or 경상북도 labels");
    }
    const auto seg = SplitRegionSegment(tb.region_combo);
    if (seg.nationwide.x != tb.region_combo.x || seg.gyeongbuk.Right() != tb.region_combo.Right()
        || seg.nationwide.y != tb.region_combo.y || seg.gyeongbuk.h != tb.region_combo.h
        || seg.nationwide.Right() != seg.gyeongbuk.x) {
        return Fail("region segment splits the toolbar region_combo slot in half");
    }
    if (HitRegionSegment(seg, seg.nationwide.x + 2, seg.nationwide.y + 2) != 0
        || HitRegionSegment(seg, seg.gyeongbuk.x + 2, seg.gyeongbuk.y + 2) != 1
        || HitRegionSegment(seg, tb.region_combo.x - 4, tb.region_combo.y) != -1) {
        return Fail("region segment hit targets are 전국 then 경북");
    }
    if (std::wstring(RegionLabelAt(0)) != L"전국" || std::wstring(RegionLabelAt(1)) != L"경북"
        || RegionIndexFromToken("경북") != 1 || RegionIndexFromToken("전국") != 0) {
        return Fail("region labels/tokens are 전국|경북");
    }
    const auto dock_seg = SplitRegionSegment(chrome.combo_region);
    if (dock_seg.nationwide.Right() != dock_seg.gyeongbuk.x
        || dock_seg.gyeongbuk.Right() != chrome.combo_region.Right()) {
        return Fail("chart dock region slot also splits 전국|경북");
    }
    if (dock_seg.nationwide.Contains(dock_seg.gyeongbuk.x, dock_seg.gyeongbuk.y + 1)
        || dock_seg.nationwide.w + dock_seg.gyeongbuk.w != chrome.combo_region.w) {
        return Fail("전국|경북 halves share one divider and must not overlap");
    }
    if (chrome.company_all.w != 64 || chrome.company_all.h != 26) {
        return Fail("전체 hit target is 64x26");
    }
    if (chrome.company_all.Right() != chrome.company_pane.Right()) {
        return Fail("전체 is right-aligned in the 업체 pane");
    }
    if (chrome.company_all.y != chrome.company_pane.y + (kCompanyHeaderH - kCompanyAllH) / 2) {
        return Fail("전체 is vertically centered in the 업체 header");
    }
    if (CompanyHeaderLabelY(chrome.company_pane.y)
        != chrome.company_pane.y + (kCompanyHeaderH - static_cast<int>(kFontEyebrow)) / 2) {
        return Fail("업체 eyebrow is vertically centered with 전체");
    }
    if (kBandPaintSelfHouse || kBandPaintRivalHouse || kBandPaintCling) {
        return Fail("자사집·경쟁집·부착 fills stay off; T is 동국구간 not a forbidden red");
    }
    if (!kBandPaintTerritory || !kBandPaintUseStages || kBandPaintPreyChips || kBandPaintThreatChips
        || !kBandPaintThreat || !kBandPaintOnStaticScatter || kBandUseStageCount < 5) {
        return Fail("idle map paints T/use/threat washes; chips keep company colors");
    }
    if ((kBandTerritoryArgb >> 24) == 0 || (kBandTerritoryArgb >> 24) >= 0x30u) {
        return Fail("territory fill is a light wash, not opaque");
    }
    if ((kBandPreyChipArgb >> 24) != 0xFFu || kBandPreyChipArgb == kDonggukArgb
        || kBandPreyChipArgb == kRankGoldArgb) {
        return Fail("prey chip fill is opaque and not 동국/1위 gold");
    }
    if ((kBandThreatChipArgb >> 24) != 0xFFu || kBandThreatChipArgb == kBandPreyChipArgb
        || kBandThreatChipArgb == kDonggukArgb) {
        return Fail("threat chip fill is distinct from prey and 동국");
    }
    if ((kBandUseArgb >> 24) >= 0x30u) {
        return Fail("use fill alpha is lighter than 0x30");
    }
    {
        const uint32_t flat_a = kBandUseArgb >> 24;
        for (int i = 0; i < kBandUseStageCount; ++i) {
            const uint32_t stage = BandUseStageArgbAt(i);
            const uint32_t a = stage >> 24;
            if (a == 0 || a >= 0x30u) {
                return Fail("each use stage is visible but lighter than 0x30 alpha");
            }
            if (stage != kBandUseArgb || a != flat_a) {
                return Fail("use stages are a flat interval, not a center peak");
            }
        }
        if (kBandPaintMedian) {
            return Fail("6-month median is not a bid mark on the map");
        }
        double s_lo = 0;
        double s_hi = 0;
        BandUseStageInterval(99.45, 99.60, 0, s_lo, s_hi);
        if (std::fabs(s_lo - 99.45) > 1e-9 || s_hi <= s_lo || s_hi > 99.60 + 1e-9) {
            return Fail("first use stage starts at use_lo");
        }
        BandUseStageInterval(99.45, 99.60, kBandUseStageCount - 1, s_lo, s_hi);
        if (std::fabs(s_hi - 99.60) > 1e-9 || s_lo >= s_hi) {
            return Fail("last use stage ends at use_hi");
        }
    }
    {
        const auto chrome = LayoutChartChrome(1600, 900);
        const auto legend = BandLegendRect(chrome);
        if (legend.w < 160 || legend.h < 20) {
            return Fail("legend has room for swatches and 동국구간 range");
        }
        if (legend.Intersects(chrome.plot) || legend.Intersects(chrome.tab_map)
            || legend.Intersects(chrome.tab_design) || legend.Intersects(chrome.tab_hist)
            || legend.Intersects(chrome.reset_toolbar) || legend.Intersects(chrome.rank1_btn)
            || legend.Intersects(chrome.reset_btn) || legend.Intersects(chrome.design_btn)) {
            return Fail("legend must not overlap plot, tabs, or 1위/맵 초기화/설계원칙");
        }
        if (legend.y < chrome.map_inner.y || legend.Right() > chrome.map_inner.Right()
            || legend.Bottom() > chrome.plot.y) {
            return Fail("legend sits in the tab chrome above the plot");
        }
        if (std::wstring(kBandLegendTitle) != L"동국구간") {
            return Fail("legend title is 동국구간, not 추천");
        }
    }
    if (!kBandPaintWatch || !kBandPaintHome || !kBandPaintSit) {
        return Fail("watch/home/sit overlay is on");
    }
    if (kBandHomeArgb == 0 || kBandWatchFillArgb == 0 || kBandSitArgb == 0 || kWatchRegularMarkArgb == 0) {
        return Fail("home/watch/sit/고정 mark colors are set");
    }
    if (kSitPulseTimerMs != 16 || kSitPulseTimerMs == 50) {
        return Fail("sit glow is vsync-ish 16ms overlay, not 50ms bounce");
    }
    if (kSitGlowLayerCount != 3 || SitGlowPadDip(0) <= SitGlowPadDip(2)) {
        return Fail("sit glow is 3 D2D layers, outer pad bigger than core");
    }
    if (kSitPulsePeriodMs < 800) {
        return Fail("sit pulse period is a slow breath, not a flicker");
    }
    if (SitPulseAlpha(0) == SitPulseAlpha(kSitPulsePeriodMs / 4)) {
        return Fail("sit pulse alpha changes over the period");
    }
    if ((SitPulseArgb(kBandSitArgb, 0) & 0x00FFFFFFu) != (kBandSitArgb & 0x00FFFFFFu)) {
        return Fail("pulse keeps sit rgb");
    }
    std::cerr << "ok chart theme matches NativeBidChartControl\n";
    return 0;
}
