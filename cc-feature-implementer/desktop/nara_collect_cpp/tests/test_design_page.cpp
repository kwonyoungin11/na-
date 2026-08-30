#include "nara/chart_theme.h"
#include "nara/design_principles.h"

#include <iostream>
#include <string>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    using nara::chart_theme::ChartDesignOpensWindow;
    using nara::chart_theme::ChartShowsDesign;
    using nara::chart_theme::ChartTabLabelAt;
    using nara::chart_theme::kChartTabCount;
    using nara::chart_theme::kMainDesignLabel;
    using nara::chart_theme::kEpilogueAccentArgb;
    using nara::chart_theme::kEpilogueEyebrow;
    using nara::chart_theme::kEpilogueLeadBarW;
    using nara::chart_theme::kEpilogueQuoteFillArgb;
    using nara::chart_theme::kMapDesignFillArgb;
    using nara::chart_theme::kMapDesignLabel;
    using nara::chart_theme::kPrincipleFigureW;
    using nara::chart_theme::kPrinciplePad;
    using nara::chart_theme::LayoutChartChrome;
    using nara::chart_theme::LayoutDesignPrinciplesChrome;
    using nara::chart_theme::LayoutMainToolbar;
    using nara::design_principles::EpilogueKind;
    using nara::design_principles::EpilogueParagraphCount;
    using nara::design_principles::EpiloguePlain;
    using nara::design_principles::kEpilogueParagraphs;
    using nara::design_principles::LayoutEpilogue;

    if (kChartTabCount != 2 || std::wstring(ChartTabLabelAt(0)) == L"설계원칙"
        || std::wstring(ChartTabLabelAt(1)) == L"설계원칙" || ChartShowsDesign(0) || ChartShowsDesign(1)) {
        return Fail("설계원칙 is not a chart tab");
    }
    if (std::wstring(kMainDesignLabel).find(L"설계원칙") != std::wstring::npos) {
        return Fail("main window has no 설계원칙 button");
    }
    if (std::wstring(kMapDesignLabel) != L"설계원칙" || kMapDesignFillArgb != 0xFF2563EBu
        || !ChartDesignOpensWindow()) {
        return Fail("map-right pill is blue 설계원칙 and opens the epilogue window");
    }

    const auto chrome = LayoutChartChrome(1600, 900);
    if (chrome.design_btn.Right() <= chrome.reset_btn.Right() || chrome.tab_design.w != 0) {
        return Fail("설계원칙 sits at the map's right edge, not in the tab strip");
    }
    const auto tb = LayoutMainToolbar(1600, 128);
    if (tb.design.w != 0) {
        return Fail("main toolbar keeps 설계원칙 off");
    }

    const std::wstring plain = EpiloguePlain();
    if (plain.find(L"이 앱을 만든 까닭은 동국의 다음 한 장을 지키기 위해서다.") == std::wstring::npos) {
        return Fail("epilogue starts from the supplied lead sentence");
    }
    if (plain.find(L"숨기지 않는 것이 동국에게 최선을 다하는 일이다.") == std::wstring::npos) {
        return Fail("epilogue keeps the supplied closing sentence");
    }
    if (plain.find(L"T에 들어오면 먹이가 되게 보여 T를 비우고") == std::wstring::npos) {
        return Fail("epilogue keeps the one-line demand");
    }
    if (plain.find(L"시중의 사정률 추천") != std::wstring::npos || plain.find(L"1.") == 0) {
        return Fail("epilogue is not numbered sections");
    }
    if (EpilogueParagraphCount() < 8) {
        return Fail("epilogue is flowing paragraphs, not a single dumped block");
    }
    for (int i = 0; i < EpilogueParagraphCount(); ++i) {
        const std::wstring p = kEpilogueParagraphs[static_cast<size_t>(i)];
        if (p.size() >= 2 && p[0] >= L'0' && p[0] <= L'9' && (p[1] == L'.' || p[1] == L')')) {
            return Fail("no numbered headings in the epilogue");
        }
    }
    if (kPrincipleFigureW != 0) {
        return Fail("epilogue has no figure column");
    }
    if (kPrinciplePad > 12) {
        return Fail("epilogue does not leave a wide empty margin");
    }

    const auto page = LayoutDesignPrinciplesChrome(840, 960);
    if (page.page.w * 10 < 840 * 9 || page.page.h * 10 < 960 * 8) {
        return Fail("reading page fills the window instead of sitting in a padded card");
    }
    const auto laid = LayoutEpilogue(page.page.w, page.page.h);
    if (laid.lines.empty() || laid.has_figure || laid.numbered) {
        return Fail("layout is text-only epilogue lines");
    }
    if (std::wstring(kEpilogueEyebrow) != L"설계원칙" || kEpilogueLeadBarW != 3
        || kEpilogueAccentArgb != kMapDesignFillArgb || kEpilogueQuoteFillArgb == 0) {
        return Fail("epilogue accent is the same blue as the map pill");
    }
    if (!laid.has_eyebrow || !laid.has_lead_bar || !laid.has_quote) {
        return Fail("page has eyebrow, lead bar, and a quote; not a flat dump of type");
    }
    bool saw_eyebrow = false;
    bool saw_lead = false;
    bool saw_quote = false;
    bool saw_close = false;
    bool saw_rule = false;
    for (const auto& line : laid.lines) {
        if (line.kind == EpilogueKind::Eyebrow && line.text == L"설계원칙") {
            saw_eyebrow = true;
        }
        if (line.kind == EpilogueKind::Lead && line.bg_w == kEpilogueLeadBarW && line.size >= 18.0f) {
            saw_lead = true;
        }
        if (line.kind == EpilogueKind::Quote && line.bg_w > 100
            && line.text.find(L"T에 들어오면") != std::wstring::npos) {
            saw_quote = true;
        }
        if (line.kind == EpilogueKind::Close
            && line.text.find(L"숨기지 않는 것이 동국에게 최선을 다하는 일이다.") != std::wstring::npos) {
            saw_close = true;
        }
        if (line.kind == EpilogueKind::Rule && line.bg_h >= 1 && line.bg_w > 100) {
            saw_rule = true;
        }
    }
    if (!saw_eyebrow || !saw_lead || !saw_quote || !saw_close || !saw_rule) {
        return Fail("eyebrow, lead bar, quote, hairline, and coda must all be laid");
    }
    if (laid.lines.front().y > page.page.h / 8) {
        return Fail("text starts at the top of the page, not after leftover blank");
    }
    const int last_bottom = laid.lines.back().y + laid.lines.back().h;
    if (last_bottom + 36 < page.page.h) {
        return Fail("text fills the page instead of leaving a blank lower half");
    }
    if (last_bottom > page.page.h) {
        return Fail("epilogue stays on one page");
    }

    std::cerr << "ok design page\n";
    return 0;
}
