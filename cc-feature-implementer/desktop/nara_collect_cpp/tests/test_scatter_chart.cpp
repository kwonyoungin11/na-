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

    std::cerr << "ok scatter chips match window companies\n";
    return 0;
}
