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
    if (ScatterColumnCount(99.0, 101.0, 0.01) != 201) {
        return Fail("99.0-101.0 step 0.01 must be 201 columns");
    }
    if (kPlotLeft != 220 || kPlotTop != 16 || kPlotRight != 22 || kPlotBottom != 46) {
        return Fail("plot inset must be left 220 top 16 right 22 bottom 46");
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
    if (kHoverRadius != 6 || kRankBadgeRadius != 2 || kCompanyPanelRadius != 12) {
        return Fail("hover 6 / rank badge 2 / company panel 12");
    }
    if (kChipRadius != 3) {
        return Fail("전장 맵 chip is slightly rounded: 3px at 20px cell");
    }
    if (std::fabs(ChipRadiusPx(20) - 3.0f) > 0.01f) {
        return Fail("20px cell chip radius is 3px");
    }
    if (std::fabs(ChipRadiusPx(40) - 6.0f) > 0.01f) {
        return Fail("chip radius scales with cell, same as chip font");
    }
    if (std::fabs(kChipOutlineDarken - 0.10f) > 0.001f) {
        return Fail("업체명 바깥라인 is 10% darker");
    }
    if (DarkenArgb(0xFF21252Bu, 0.10f) != 0xFF1E2127u) {
        return Fail("#21252B * 0.90 = #1E2127");
    }
    if (ChipOutlineArgb() != 0xFF1E2127u) {
        return Fail("chip outline is 10% darker than #21252B");
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
    if (kResetH != 40 || kResetMinW != 124 || kDockMinH != 168) {
        return Fail("reset 40x124 and dock min 168");
    }
    if (kComboH != 40 || kComboRadius != 12) {
        return Fail("분석 범위 combo is 40px radius 12");
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
    if (kDockPadX != 14 || kDockPadY != 12 || kDockRadius != 16 || kEyebrowGap != 8) {
        return Fail("dock pad 14,12 radius 16 eyebrow gap 8");
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
    if (kCompanyPadX != 8 || kCompanyPadY != 6 || kCompanyWrapMinH != 128 || kCompanyPanelRadius != 12) {
        return Fail("업체 wrap pad 8,6 minHeight 128 radius 12");
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
        return Fail("dock inner minHeight 168");
    }
    if (chrome.combo_period.h != 40 || chrome.combo_precision.h != 40 || chrome.combo_region.h != 40
        || chrome.combo_min.h != 40 || chrome.combo_max.h != 40) {
        return Fail("dock combos are 40px");
    }
    if (chrome.reset_btn.w != kResetMinW || chrome.reset_btn.h != kResetH) {
        return Fail("맵 초기화 overlay pill is 124x40");
    }
    if (chrome.reset_toolbar.y < chrome.map_inner.y || chrome.reset_toolbar.Right() > chrome.map_inner.Right()
        || chrome.reset_toolbar.x < chrome.map_inner.x) {
        return Fail("맵 초기화 overlay stays inside the map inner card");
    }
    if (chrome.reset_btn.x != chrome.reset_toolbar.x + kToolbarPad
        || chrome.reset_btn.y != chrome.reset_toolbar.y + kToolbarPad) {
        return Fail("reset pill sits in toolbar pad 8");
    }
    if (chrome.company_wrap.h < kCompanyWrapMinH) {
        return Fail("업체 wrap minHeight 128");
    }
    if (chrome.plot.x != chrome.map_inner.x + kPlotLeft || chrome.plot.y != chrome.map_inner.y + kPlotTop) {
        return Fail("plot is the inner host rect, not the full popup");
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
        || kMainRegionComboW != 148 || kMainBtnGap != 8 || kMainComboGap != 12
        || kMainRegionLabelGap != 8 || kMainBtnRadius != 14 || kMainTablePadTop != 14
        || kMainTablePadX != 16 || kMainCardInset != 20 || kMainHeaderGap != 12) {
        return Fail("main toolbar sizes must match MainWindow.xaml 40px / 124 / 108 / 132 / 148");
    }
    if (kCompanyAllW != 64 || kCompanyAllH != 32 || kCompanyHeaderH != 32) {
        return Fail("전체 is GhostButton 64x32 in the 32px 업체 header");
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
    if (MainTableGridY(348) != 414) {
        return Fail("grid starts after pad 14 + 40px buttons + 12px header gap");
    }
    const auto tb = LayoutMainToolbar(1600, 348);
    if (tb.chart.y != 362 || tb.stop.y != 362 || tb.update.y != 362 || tb.region_combo.y != 362
        || tb.region_label.y != 362) {
        return Fail("지역 combo and all three buttons share the same centered Y");
    }
    if (tb.chart.h != 40 || tb.stop.h != 40 || tb.update.h != 40 || tb.region_combo.h != 40
        || tb.region_label.h != 40) {
        return Fail("toolbar controls are 40px tall");
    }
    if (tb.update.w != 124 || tb.stop.w != 108 || tb.chart.w != 132 || tb.region_combo.w != 148) {
        return Fail("toolbar widths 124/108/132/148");
    }
    if (tb.chart.Right() != 1600 - kMainCardInset - kMainTablePadX) {
        return Fail("차트 화면 is right-aligned inside card pad 16");
    }
    if (tb.stop.Right() + kMainBtnGap != tb.chart.x || tb.update.Right() + kMainBtnGap != tb.stop.x
        || tb.region_combo.Right() + kMainComboGap != tb.update.x
        || tb.region_label.Right() + kMainRegionLabelGap != tb.region_combo.x) {
        return Fail("toolbar gaps: label 8, combo 12, buttons 8");
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
    if (chrome.company_all.w != 64 || chrome.company_all.h != 32) {
        return Fail("전체 hit target is 64x32");
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
    std::cerr << "ok chart theme matches NativeBidChartControl\n";
    return 0;
}
