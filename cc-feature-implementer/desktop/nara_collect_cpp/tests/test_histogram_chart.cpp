#include "nara/histogram_chart.h"

#include "nara/chart_theme.h"
#include "nara/igunsul_convert.h"

#include <cmath>
#include <iostream>
#include <string>
#include <vector>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

nara::ParticipantRow Chip(const std::string& company, double rate, const std::string& notice = "N1") {
    nara::ParticipantRow row;
    row.notice_number = notice;
    row.company_name = company;
    row.normalized_company_name = nara::CanonicalizeCompany(company);
    row.adjust_rate = rate;
    row.open_date = "2026-06-01";
    return row;
}

}  // namespace

int main() {
    using nara::BuildHistogram;
    using nara::HistogramBinIndex;
    using nara::HistogramBinSpec;
    using nara::HistogramCompanyArgb;
    using nara::HistogramHitBin;
    using nara::HistogramIsDongguk;
    using nara::LayoutHistScene;
    using nara::MakeHistBar;
    using nara::ProjectIso;
    using nara::SelectedCompanyKeys;
    using nara::chart_theme::kDonggukArgb;
    using nara::chart_theme::kHistogramBinCount;
    using nara::chart_theme::kHistogramMaxSeries;

    HistogramBinSpec spec;
    spec.min_rate = 99.0;
    spec.max_rate = 101.0;
    spec.bin_count = 40;
    if (std::fabs(nara::HistogramBinStep(spec) - 0.05) > 0.0001) {
        return Fail("99-101 over 40 bins is 0.05");
    }
    if (HistogramBinIndex(98.9, spec) >= 0 || HistogramBinIndex(101.1, spec) >= 0) {
        return Fail("rates outside 99-101 must drop like scatter chips");
    }
    if (HistogramBinIndex(99.0, spec) != 0) {
        return Fail("min rate lands in bin 0");
    }
    if (HistogramBinIndex(101.0, spec) != 39) {
        return Fail("max rate is inclusive in the last bin");
    }
    const int mid = HistogramBinIndex(100.0, spec);
    if (mid < 18 || mid > 21) {
        return Fail("100.0 is the center bin");
    }

    const std::vector<nara::ParticipantRow> chips = {
        Chip("한울문화재연구원", 99.12),
        Chip("한울문화재연구원", 99.13, "N2"),
        Chip("미리내문화재연구원", 99.12, "N2"),
        Chip("동국문화재연구원", 100.04),
        Chip("동국문화재연구원", 86.2, "N3"),
    };
    auto market = BuildHistogram(chips, spec, {}, kHistogramMaxSeries);
    if (static_cast<int>(market.market.counts.size()) != spec.bin_count) {
        return Fail("market series is one count per bin");
    }
    if (market.market.total != 4) {
        return Fail("histogram counts unique chips in the 99-101 window, not bidprcrt 86.x");
    }
    if (!market.companies.empty()) {
        return Fail("no company checks means market-only, same as 전체");
    }
    const int low = HistogramBinIndex(99.12, spec);
    if (market.market.counts[static_cast<size_t>(low)] != 3) {
        return Fail("99.12/99.13 chips share one bin");
    }
    const int peak = HistogramBinIndex(100.04, spec);
    if (market.market.counts[static_cast<size_t>(peak)] != 1) {
        return Fail("동국 100.04 is one market count");
    }
    if (market.market_max != 3) {
        return Fail("market scale uses the tallest bin");
    }

    const std::vector<std::string> names = {"한울문화재연구원", "미리내문화재연구원", "동국문화재연구원"};
    std::vector<char> none = {0, 0, 0};
    if (!SelectedCompanyKeys(names, none).empty()) {
        return Fail("unchecked 전체 path yields no selected keys");
    }
    std::vector<char> two = {1, 0, 1};
    const auto selected = SelectedCompanyKeys(names, two);
    if (selected.size() != 2 || selected[0] != names[0] || selected[1] != names[2]) {
        return Fail("checked companies are the same keys as the dock checkboxes");
    }

    auto patterned = BuildHistogram(chips, spec, selected, kHistogramMaxSeries);
    if (patterned.companies.size() != 2) {
        return Fail("checked companies each get a bid-pattern series");
    }
    if (patterned.market.total != 4) {
        return Fail("market stays the full snapshot when companies are checked");
    }
    bool saw_hanul = false;
    bool saw_dongguk = false;
    for (const auto& series : patterned.companies) {
        if (series.key.find("한울") != std::string::npos) {
            saw_hanul = true;
            if (series.total != 2 || series.counts[static_cast<size_t>(low)] != 2) {
                return Fail("한울 pattern is two bids in the low bin");
            }
        }
        if (nara::HistogramIsDongguk(series.key)) {
            saw_dongguk = true;
            if (series.total != 1 || series.counts[static_cast<size_t>(peak)] != 1) {
                return Fail("동국 pattern is the 100.04 bid only");
            }
            if (series.argb != kDonggukArgb) {
                return Fail("동국 histogram series uses #FF0000");
            }
        }
    }
    if (!saw_hanul || !saw_dongguk) {
        return Fail("selected series must include 한울 and 동국");
    }
    if (patterned.company_max != 2) {
        return Fail("selected-company scale uses the tallest checked bin, not the market");
    }

    if (!HistogramIsDongguk("동국문화유산연구원") || HistogramIsDongguk("한울문화재연구원")) {
        return Fail("동국 token matches CanonicalizeCompany 문화유산→문화재");
    }
    if (HistogramCompanyArgb("동국문화재연구원") != kDonggukArgb) {
        return Fail("동국 fill is bidder_colors #FF0000");
    }

    std::vector<nara::ParticipantRow> many;
    many.reserve(12);
    for (int i = 0; i < 12; ++i) {
        many.push_back(Chip("업체" + std::to_string(i), 99.5 + 0.01 * i, "N" + std::to_string(i)));
    }
    many.push_back(Chip("동국문화재연구원", 99.80, "N99"));
    std::vector<std::string> many_keys;
    for (int i = 0; i < 12; ++i) {
        many_keys.push_back(nara::CanonicalizeCompany("업체" + std::to_string(i)));
    }
    many_keys.push_back(nara::CanonicalizeCompany("동국문화재연구원"));
    auto trimmed = BuildHistogram(many, spec, many_keys, 8);
    if (static_cast<int>(trimmed.companies.size()) != 8) {
        return Fail("3D series cap keeps the chart readable");
    }
    bool kept_dongguk = false;
    for (const auto& series : trimmed.companies) {
        if (HistogramIsDongguk(series.key)) {
            kept_dongguk = true;
        }
    }
    if (!kept_dongguk) {
        return Fail("동국 stays in the 3D series when checked even if others are trimmed");
    }

    const auto scene = LayoutHistScene(0, 0, 800, 400, 40, 3);
    if (scene.bar_w < 3.0f || scene.max_h < 20.0f) {
        return Fail("3D bars keep a drawable width and height");
    }
    const auto origin = ProjectIso(0, 0, 0, scene);
    if (std::fabs(origin.x - scene.origin_x) > 0.01f || std::fabs(origin.y - scene.origin_y) > 0.01f) {
        return Fail("iso origin maps to the floor front-left");
    }
    const auto lifted = ProjectIso(0, 40, 0, scene);
    if (!(lifted.y < origin.y)) {
        return Fail("world Y up is screen Y down");
    }
    const auto recede = ProjectIso(0, 0, 20, scene);
    if (!(recede.x > origin.x) || !(recede.y < origin.y)) {
        return Fail("Z recedes up-right for a cabinet 3D histogram");
    }
    const auto bar = MakeHistBar(0, 0, scene.bar_w, scene.bar_d, 50.0f, scene);
    if (bar.front[0].y < bar.front[3].y) {
        return Fail("front face bottom is below the top");
    }
    if (bar.top[2].x <= bar.front[2].x) {
        return Fail("top face recedes in Z");
    }
    if (bar.side[1].x <= bar.front[1].x) {
        return Fail("side face is the right 3D wall");
    }

    const int hit = HistogramHitBin(scene.origin_x + scene.bar_w * 0.4f, scene.origin_y - 10.0f, scene, 40);
    if (hit != 0) {
        return Fail("hover over the first bar hits bin 0");
    }
    if (HistogramHitBin(scene.origin_x - 20.0f, scene.origin_y, scene, 40) >= 0) {
        return Fail("left of the floor is not a bin");
    }
    if (kHistogramBinCount != 40 || kHistogramMaxSeries != 8) {
        return Fail("histogram tokens are 40 bins and 8 company rows");
    }

    if (nara::HistogramAxisMax(market) != market.market_max) {
        return Fail("전체 histogram Y axis is the market peak");
    }
    if (nara::HistogramAxisMax(patterned) != patterned.company_max) {
        return Fail("one checked company must scale Y to that company, not 전체 353");
    }
    if (nara::HistogramDrawsMarketBars(market) != true || nara::HistogramDrawsMarketBars(patterned) != false) {
        return Fail("checked companies replace 전체 bars so 3D volumes do not stack");
    }
    if (nara::HistogramVisibleSeriesCount(market) != 1 || nara::HistogramVisibleSeriesCount(patterned) != 2) {
        return Fail("visible series is 전체 or the checked companies");
    }
    if (std::fabs(nara::HistogramBarHeight(2, 2, 100.0f) - 100.0f) > 0.01f
        || std::fabs(nara::HistogramBarHeight(1, 2, 100.0f) - 50.0f) > 0.01f) {
        return Fail("bar height uses the same axis max as the Y ticks");
    }

    const auto clustered = nara::LayoutHistScene(0, 0, 800, 400, 40, 2);
    if (nara::HistClusterOverlaps(5, 0, 1, clustered)) {
        return Fail("two companies in one bin sit side by side, not in the same 3D cell");
    }
    if (nara::HistClusterBarW(clustered) >= clustered.bar_w) {
        return Fail("clustered bars are narrower than the bin");
    }

    nara::ScatterNotice notice;
    notice.notice_number = "N1";
    auto r1 = Chip("한울문화재연구원", 99.55, "N1");
    r1.rank = 1;
    r1.is_awarded = true;
    auto r2 = Chip("미리내문화재연구원", 99.80, "N1");
    r2.rank = 2;
    notice.chips = {r1, r2};
    const auto all_chips = nara::CollectHistogramChips({notice}, false);
    const auto rank1_chips = nara::CollectHistogramChips({notice}, true);
    if (all_chips.size() != 2) {
        return Fail("histogram without 1위 keeps every 99-101 chip");
    }
    if (rank1_chips.size() != 1 || !rank1_chips[0].adjust_rate || std::fabs(*rank1_chips[0].adjust_rate - 99.55) > 0.001) {
        return Fail("1위 toggle keeps IsMappedRank1 chips only");
    }
    auto rank1_hist = BuildHistogram(rank1_chips, spec, {}, kHistogramMaxSeries);
    if (rank1_hist.market.total != 1 || rank1_hist.market_max != 1) {
        return Fail("1위 histogram counts first-place bids, not the whole notice");
    }

    std::cerr << "ok histogram bins share the scatter snapshot and company checks\n";
    return 0;
}
