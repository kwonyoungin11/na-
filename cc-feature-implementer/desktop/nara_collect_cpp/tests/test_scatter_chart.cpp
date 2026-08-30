#include "nara/scatter_chart.h"

#include <cmath>
#include <iostream>
#include <string>
#include <vector>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

nara::ParticipantRow Row(
    const std::string& notice,
    int rank,
    const std::string& company,
    double rate) {
    nara::ParticipantRow row;
    row.notice_number = notice;
    row.notice_no = notice;
    row.rank = rank;
    row.company_name = company;
    row.normalized_company_name = company;
    row.adjust_rate = rate;
    row.is_awarded = rank == 1;
    return row;
}

}  // namespace

int main() {
    std::vector<nara::ParticipantRow> rows = {
        Row("R26BK01673928-000", 1, "춘추문화재연구원", 99.78004),
        Row("R26BK01673928-000", 2, "전북문화유산연구원", 99.78127),
        Row("R26BK01673928-000", 3, "경강문화유산연구원", 99.78376),
        Row("R26BK01673928-000", 4, "한울문화재연구원", 99.79000),
        Row("R26BK01673928-000", 5, "가람문화재연구원", 99.80000),
        Row("R26BK01673928-000", 62, "동국문화재연구원", 99.60535),
        Row("R26BK01673928-000", 70, "범위밖연구원", 98.10000),
    };

    const auto scatter = nara::BuildScatterNotice(rows, "R26BK01673928-000", 99.0, 101.0);
    if (scatter.chip_count != 6) {
        std::cerr << "FAIL: chip_count expected 6 got " << scatter.chip_count << "\n";
        return 1;
    }
    if (scatter.dongguk_rank != 6) {
        std::cerr << "FAIL: dongguk_rank among window expected 6 got " << scatter.dongguk_rank
                  << "\n";
        return 1;
    }
    if (scatter.total_companies != 7) {
        std::cerr << "FAIL: total_companies expected 7 got " << scatter.total_companies << "\n";
        return 1;
    }

    const auto label = nara::FormatDonggukRankLabel(scatter, 99.60535, 99.78004);
    if (label.find("6위") == std::string::npos || label.find("6개사") == std::string::npos) {
        std::cerr << "FAIL: label must show window rank and chip count, got " << label << "\n";
        return 1;
    }
    if (label.find("62위") != std::string::npos) {
        return Fail("label must not keep stored rank 62 when only 6 chips plot");
    }

    nara::ParticipantRow hanguk_heritage = Row("HANGUK", 1, "재단법인 한국문화유산연구원", 100.102);
    hanguk_heritage.normalized_company_name = "한국문화재연구원";
    nara::ParticipantRow hanguk_jae = Row("HANGUK", 21, "재단법인 한국문화재연구원", 100.110);
    hanguk_jae.normalized_company_name = "한국문화재연구원";
    const auto hanguk_scatter = nara::BuildScatterNotice({hanguk_heritage, hanguk_jae}, "HANGUK", 99.0, 101.0);
    if (hanguk_scatter.chip_count != 2) {
        std::cerr << "FAIL: 한국문화유산 and 한국문화재 are two chips, got " << hanguk_scatter.chip_count
                  << "\n";
        return 1;
    }

    nara::ParticipantRow hanul_old = Row("HANUL", 4, "재단법인 한울문화유산연구원", 99.79);
    hanul_old.normalized_company_name = "한울문화재연구원";
    nara::ParticipantRow hanul_new = Row("HANUL", 5, "한울문화재연구원", 99.80);
    hanul_new.normalized_company_name = "한울문화재연구원";
    const auto hanul_scatter = nara::BuildScatterNotice({hanul_old, hanul_new}, "HANUL", 99.0, 101.0);
    if (hanul_scatter.chip_count != 1) {
        std::cerr << "FAIL: 한울 개명 variants stay one chip, got " << hanul_scatter.chip_count << "\n";
        return 1;
    }

    std::vector<nara::ParticipantRow> twice = {
        Row("DUP", 1, "춘추문화재연구원", 99.72),
        Row("DUP", 1, "춘추문화재연구원", 99.72),
        Row("DUP", 7, "경상문화유산연구원", 99.83248),
        Row("DUP", 7, "경상문화유산연구원", 99.83248),
        Row("DUP", 8, "새론문화유산연구원", 99.83292),
        Row("DUP", 8, "새론문화유산연구원", 99.83292),
        Row("DUP", 62, "동국문화재연구원", 99.60),
    };
    const auto dup_scatter = nara::BuildScatterNotice(twice, "DUP", 99.0, 101.0);
    if (dup_scatter.chip_count != 4) {
        std::cerr << "FAIL: duplicate sqlite rows must collapse to one chip each, got " << dup_scatter.chip_count
                  << "\n";
        return 1;
    }
    int gold_count = 0;
    for (const auto& chip : dup_scatter.chips) {
        if (nara::IsMappedRank1(chip, dup_scatter.chips)) {
            ++gold_count;
        }
    }
    if (gold_count != 1) {
        std::cerr << "FAIL: mapped 1위 count " << gold_count << "\n";
        return Fail("한 공고에 금테 1위는 한 칩만");
    }

    std::vector<nara::ParticipantRow> two_first = {
        Row("GOLD2", 1, "문유문화재연구원", 99.72000),
        Row("GOLD2", 1, "문유문화학연구원", 99.72100),
        Row("GOLD2", 62, "동국문화재연구원", 99.60000),
    };
    const auto two_scatter = nara::BuildScatterNotice(two_first, "GOLD2", 99.0, 101.0);
    if (two_scatter.chip_count != 3) {
        std::cerr << "FAIL: two rank1 identities stay two chips, got " << two_scatter.chip_count << "\n";
        return 1;
    }
    int two_gold = 0;
    const nara::ParticipantRow* gold_chip = nullptr;
    for (const auto& chip : two_scatter.chips) {
        if (nara::IsMappedRank1(chip, two_scatter.chips)) {
            ++two_gold;
            gold_chip = &chip;
        }
    }
    if (two_gold != 1) {
        std::cerr << "FAIL: two rank1 identities gold " << two_gold << "\n";
        return Fail("서로 다른 업체라도 한 공고 금테 1위는 한 칩만");
    }
    if (!gold_chip || gold_chip->company_name.find("문유문화재연구원") == std::string::npos) {
        return Fail("금테는 사정률이 더 낮은 rank1만");
    }

    nara::ParticipantRow feb = Row("FEB", 1, "동국문화재연구원", 99.5);
    feb.open_date = "2026-02-20 11:00:00";
    nara::ParticipantRow jun = Row("JUN", 1, "동국문화재연구원", 99.6);
    jun.open_date = "2026-06-09 11:00:00";
    const auto six = nara::InPeriod({feb, jun}, "2026-02-25", "2026-08-25");
    if (six.size() != 1 || six[0].notice_number != "JUN") {
        return Fail("InPeriod 6-month window must drop Feb 20 when start is Feb 25");
    }

    const auto mapped_rank1 = nara::ToChartAdjustRate(96.78818, 96.78818, 99.78004);
    const auto mapped_dongguk = nara::ToChartAdjustRate(96.40535, 96.78818, 99.78004);
    if (std::fabs(mapped_rank1 - 99.78004) > 0.00001) {
        std::cerr << "FAIL: rank1 map " << mapped_rank1 << "\n";
        return 1;
    }
    if (std::fabs(mapped_dongguk - 99.39721) > 0.00001) {
        std::cerr << "FAIL: dongguk map " << mapped_dongguk << "\n";
        return 1;
    }

    auto live_rank1 = Row("R26BK01673928-000", 1, "재단법인 춘추문화유산연구원", 100.014);
    live_rank1.normalized_company_name = "춘추문화재연구원";
    auto live_rank2 = Row("R26BK01673928-000", 2, "재단법인 전북문화유산연구원", 100.015);
    live_rank2.normalized_company_name = "전북문화재연구원";
    std::vector<nara::ParticipantRow> live = {live_rank1, live_rank2};
    nara::PreferChartAdjustRates(
        live,
        {Row("R26BK01673928-000", 1, "춘추문화재연구원", 99.78004),
         Row("R26BK01673928-000", 2, "전북문화재연구원", 99.78123)});
    if (!live[0].adjust_rate || std::fabs(*live[0].adjust_rate - 99.78004) > 0.00001) {
        return Fail("rank1 must use 아이건설 99.78004 not 예가대비 100.014");
    }
    if (live[0].rank != 1) {
        return Fail("overlay must keep 춘추 as rank 1");
    }
    const auto overlaid = nara::BuildScatterNotice(live, "R26BK01673928-000", 99.0, 101.0);
    if (overlaid.chips.empty() || overlaid.chips[0].rank != 1 || !overlaid.chips[0].adjust_rate
        || std::fabs(*overlaid.chips[0].adjust_rate - 99.78004) > 0.00001) {
        return Fail("전장 맵 1위 칩은 99.78004에 있어야 한다");
    }

    auto nara_rank1 = Row("KEEP", 1, "춘추문화재연구원", 99.78004);
    auto nara_rank2 = Row("KEEP", 2, "전북문화재연구원", 99.78123);
    std::vector<nara::ParticipantRow> nara_ok = {nara_rank1, nara_rank2};
    nara::PreferChartAdjustRates(
        nara_ok,
        {Row("KEEP", 1, "춘추문화재연구원", 99.99900), Row("KEEP", 2, "전북문화재연구원", 99.99800)});
    if (!nara_ok[0].adjust_rate || std::fabs(*nara_ok[0].adjust_rate - 99.78004) > 0.00001) {
        return Fail("나라장터 99.xxxx 1위는 아이건설 값으로 덮지 않는다");
    }

    std::vector<nara::ParticipantRow> nara_only = {Row("ADD", 1, "춘추문화재연구원", 99.78004)};
    nara::PreferChartAdjustRates(nara_only, {Row("ADD", 1, "춘추문화재연구원", 99.78004),
                                            Row("ADD", 77, "해동문화재연구원", 99.53117)});
    if (nara_only.size() != 2) {
        return Fail("나라장터에 없는 업체만 아이건설에서 추가해야 한다");
    }
    bool added = false;
    for (const auto& row : nara_only) {
        if (row.company_name.find("해동") != std::string::npos && row.adjust_rate
            && std::fabs(*row.adjust_rate - 99.53117) < 0.00001) {
            added = true;
        }
    }
    if (!added) {
        return Fail("아이건설 해동 99.53117이 추가되어야 한다");
    }

    std::vector<nara::ParticipantRow> api96 = {
        Row("S96", 1, "춘추문화재연구원", 96.78818),
        Row("S96", 62, "동국문화재연구원", 96.40535),
    };
    nara::PreferChartAdjustRates(api96, {Row("S96", 1, "춘추문화재연구원", 99.78004)});
    if (!api96[0].adjust_rate || std::fabs(*api96[0].adjust_rate - 99.78004) > 0.00001) {
        return Fail("96.x 나라장터는 아이건설 1위 앵커로 스케일해야 한다");
    }
    if (!api96[1].adjust_rate || std::fabs(*api96[1].adjust_rate - 99.39721) > 0.00001) {
        return Fail("96.x 동국도 같은 앵커로 스케일해야 한다");
    }

    auto mixed_r1 = Row("MIX", 1, "춘추문화재연구원", 100.014);
    mixed_r1.lower_rate = 86.245;
    auto mixed_r2 = Row("MIX", 2, "전북문화재연구원", 100.015);
    mixed_r2.lower_rate = 86.245;
    auto mixed_dg = Row("MIX", 55, "동국문화재연구원", 99.6185);
    std::vector<nara::ParticipantRow> mixed = {mixed_r1, mixed_r2, mixed_dg};
    nara::PreferChartAdjustRates(
        mixed,
        {Row("MIX", 1, "춘추문화재연구원", 99.78004), Row("MIX", 62, "동국문화재연구원", 99.39721)});
    if (!mixed[0].adjust_rate || std::fabs(*mixed[0].adjust_rate - 99.78004) > 0.00001) {
        return Fail("1위 100.014만 아이건설 99.78004로 채운다");
    }
    if (!mixed[2].adjust_rate || std::fabs(*mixed[2].adjust_rate - 99.6185) > 0.00001) {
        return Fail("동국 99.xxxx는 덮지 않는다");
    }
    if (!nara::NoticeHasLowerLimitAwardCluster({mixed_r1, mixed_r2, mixed_dg})) {
        return Fail("순위 있는 100.00 군집은 동국 99.x와 무관하게 잡아야 한다");
    }
    if (nara::IsMappedRank1(mixed_r1, {mixed_r1, mixed_r2, mixed_dg})) {
        return Fail("100.00 cluster rank1 must not map as 1위");
    }

    auto spread_r1 = Row("SPREAD", 1, "미래문화재연구원", 99.95105);
    spread_r1.lower_rate = 87.745;
    auto spread_r2 = Row("SPREAD", 2, "한울문화재연구원", 99.99000);
    spread_r2.lower_rate = 87.745;
    auto spread_dg = Row("SPREAD", 40, "동국문화재연구원", 99.20000);
    spread_dg.lower_rate = 87.745;
    std::vector<nara::ParticipantRow> spread = {spread_r1, spread_r2, spread_dg};
    const char* extras[] = {"가온문화재연구원", "화서문화재연구원", "미리문화재연구원",
                            "산들문화재연구원", "금강문화재연구원", "호남문화재연구원"};
    const double extra_rates[] = {99.12, 99.40, 99.70, 100.40, 100.70, 100.95};
    for (int i = 0; i < 6; ++i) {
        auto extra = Row("SPREAD", i + 3, extras[i], extra_rates[i]);
        extra.lower_rate = 87.745;
        spread.push_back(extra);
    }
    if (!nara::LooksLikeOpenApiHundred(spread_r1)) {
        return Fail("99.95 + 하한율 still matches the per-chip OpenAPI band");
    }
    if (nara::NoticeHasLowerLimitAwardCluster(spread)) {
        return Fail("99.12-100.95 spread is not a 100.00 cluster");
    }
    if (!nara::IsMappedRank1(spread_r1, spread)) {
        return Fail("1위 at 99.95 with spread chips must stay on the 1위 map");
    }

    auto mid_r1 = Row("MID", 1, "한빛문화재연구원", 100.04000);
    mid_r1.lower_rate = 87.745;
    auto mid_r2 = Row("MID", 2, "호남문화재연구원", 100.04708);
    mid_r2.lower_rate = 87.745;
    std::vector<nara::ParticipantRow> mid = {mid_r1, mid_r2, spread_dg};
    for (int i = 0; i < 6; ++i) {
        auto extra = Row("MID", i + 3, extras[i], extra_rates[i]);
        extra.lower_rate = 87.745;
        mid.push_back(extra);
    }
    if (!nara::LooksLikeOpenApiHundred(mid_r1)) {
        return Fail("100.04 + 하한율 still matches the per-chip OpenAPI band");
    }
    if (!nara::IsMappedRank1(mid_r1, mid)) {
        return Fail("1위 at 100.04 with spread chips must stay on the 1위 map");
    }

    std::vector<std::pair<std::string, std::string>> keys = {
        {"R-OLD", "2026-06-12 11:00:00"},
        {"R-NEW", "2026-08-28 11:00:00"},
        {"R-MID", "2026-07-03 11:00:00"},
    };
    nara::SortNoticeKeysOldestFirst(keys);
    if (keys.size() != 3 || keys[0].first != "R-OLD" || keys[2].first != "R-NEW") {
        return Fail("map Y is oldest at the top, newest at the bottom (C# OrderBy OpenDate)");
    }

    std::cerr << "ok scatter chips match window companies\n";
    return 0;
}
