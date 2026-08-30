#include "nara/award_table.h"
#include "nara/notice_rules.h"

#include <cmath>
#include <iostream>
#include <set>
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
    double rate,
    const std::string& open_date,
    bool awarded,
    const std::string& region = "전국") {
    nara::ParticipantRow row;
    row.notice_number = notice;
    row.notice_no = notice;
    row.notice_name = "공고-" + notice;
    row.ordering_agency = "기관";
    row.region = region;
    row.rank = rank;
    row.company_name = company;
    row.normalized_company_name = company;
    row.open_date = open_date;
    row.adjust_rate = rate;
    row.is_awarded = awarded;
    return row;
}

void AddSixFirmNotice(std::vector<nara::ParticipantRow>& rows, const std::string& notice,
                      const std::string& region, const std::string& open_date) {
    const char* firms[] = {"동국문화재연구원", "한울문화재연구원", "가온문화재연구원",
                           "화서문화재연구원", "미리문화재연구원", "낙찰문화재연구원"};
    for (int i = 0; i < 6; ++i) {
        rows.push_back(Row(notice, i == 5 ? 1 : i + 2, firms[i], 99.100 + i * 0.01, open_date, i == 5, region));
    }
}

}  // namespace

int main() {
    std::vector<nara::ParticipantRow> rows = {
        Row("N-1", 3, "동국문화재연구원", 99.222, "2025-01-15 11:00:00", false),
        Row("N-1", 1, "다른기관", 99.456, "2025-01-15 11:00:00", true),
        Row("N-2", 4, "동국문화재연구원", 99.333, "2025-06-15 11:00:00", false),
        Row("N-2", 1, "다른기관", 99.777, "2025-06-15 11:00:00", true),
    };
    const auto table = nara::BuildAwardNoticeRows(rows, "2025-01-01", "2025-12-31");
    if (table.size() != 2) {
        return Fail("year window must keep both Dongguk award notices");
    }
    if (table[0].notice_number != "N-2" || table[1].notice_number != "N-1") {
        return Fail("rows must be newest first");
    }
    if (table[0].rank_label.find("개사") == std::string::npos) {
        return Fail("rank label must use chip count N개사");
    }
    if (table[1].award_company.find("다른") == std::string::npos || std::fabs(table[1].award_rate - 99.456) > 1e-6) {
        return Fail("table stores 1위 업체 and 1위 사정률, not 넣을 곳");
    }

    if (nara::CanonicalizeRegion("경상북도") != "경북" || nara::CanonicalizeRegion("경상북") != "경북") {
        return Fail("경상북도/경상북 canonicalize to 경북");
    }
    if (nara::CanonicalizeRegion("전국") != "전국") {
        return Fail("전국 stays 전국");
    }
    if (nara::MatchesRegion("전국", "경북")) {
        return Fail("전국 source must not match selected 경북");
    }
    if (!nara::MatchesRegion("경상북도", "경북")) {
        return Fail("경상북도 source must match selected 경북");
    }
    if (nara::MatchesRegion("경북", "전국") || nara::MatchesRegion("전국", "전체")) {
        return Fail("do not mix regions or treat 전체 as pass-all");
    }

    std::vector<nara::ParticipantRow> mixed;
    AddSixFirmNotice(mixed, "N-NAT", "전국", "2025-06-15 11:00:00");
    AddSixFirmNotice(mixed, "N-GB", "경상북도", "2025-06-16 11:00:00");
    const auto gyungbuk = nara::BuildChartUniverse(mixed, "2025-01-01", "2025-12-31", "경북", 99.0, 101.0);
    for (const auto& row : gyungbuk) {
        if (nara::CanonicalizeRegion(row.region) != "경북") {
            return Fail("BuildChartUniverse(경북) must keep only 경북 notices");
        }
    }
    bool has_gb = false;
    for (const auto& row : gyungbuk) {
        if (row.notice_number == "N-GB") {
            has_gb = true;
            break;
        }
    }
    if (!has_gb) {
        return Fail("BuildChartUniverse(경북) must keep the 경북 notice");
    }
    const auto gb_table = nara::BuildAwardNoticeRows(gyungbuk, "2025-01-01", "2025-12-31");
    if (gb_table.size() != 1 || gb_table[0].notice_number != "N-GB") {
        return Fail("경북 table universe is only the 경북 notice");
    }

    std::vector<nara::ParticipantRow> market;
    AddSixFirmNotice(market, "DONG", "전국", "2026-08-01 11:00:00");
    const char* others[] = {"한울문화재연구원", "가온문화재연구원", "화서문화재연구원",
                            "미리문화재연구원", "낙찰문화재연구원", "춘추문화재연구원"};
    for (int i = 0; i < 6; ++i) {
        market.push_back(Row("OTHER", i + 1, others[i], 99.200 + i * 0.01, "2026-08-01 11:00:00", i == 0));
    }
    std::set<std::string> persist_notices;
    for (const auto& row : nara::ApplyPersistRules(market)) {
        persist_notices.insert(row.notice_number);
    }
    const auto chart = nara::BuildChartUniverse(market, "2026-02-01", "2026-08-26", "전국", 99.0, 101.0);
    const auto dongguk_table = nara::BuildAwardNoticeRows(chart, "2026-02-01", "2026-08-26");
    if (persist_notices.size() != 2) {
        return Fail("persist market has 동국 + 비동국 notices");
    }
    if (dongguk_table.size() != 1 || dongguk_table[0].notice_number != "DONG") {
        return Fail("hero chip and table must use 동국 참여 공고 only, not persist market 165 vs 86");
    }
    if (!dongguk_table[0].has_award_rate) {
        return Fail("real 99.xxxx 낙찰 사정률 must stay valid");
    }

    std::vector<nara::ParticipantRow> fake100;
    auto dong = Row("F100", 0, "동국문화재연구원", 99.6185, "2025-10-21 11:00:00", false);
    fake100.push_back(dong);
    for (int i = 0; i < 6; ++i) {
        auto r = Row("F100", i + 1, others[i], 100.010 + i * 0.002, "2025-10-21 11:00:00", i == 0);
        r.lower_rate = 86.245;
        fake100.push_back(r);
    }
    const auto fake_table = nara::BuildAwardNoticeRows(fake100, "2025-09-01", "2026-08-26");
    if (fake_table.size() != 1) {
        return Fail("동국 참여 공고는 표에 남긴다");
    }
    if (std::fabs(fake_table[0].dongguk_rate - 99.6185) > 0.0001) {
        return Fail("동국 기초대비사정률은 그대로 둔다");
    }
    if (fake_table[0].has_award_rate) {
        return Fail("100.00 하한대비를 실제 낙찰 사정률로 쓰지 않는다");
    }
    if (fake_table[0].rank_label.find("낙찰") != std::string::npos) {
        return Fail("가짜 100 낙찰률을 문구에 넣지 않는다");
    }

    std::vector<nara::ParticipantRow> shot;
    auto dong2 = Row("SHOT", 55, "동국문화재연구원", 99.6185, "2026-05-15 11:00:00", false);
    shot.push_back(dong2);
    auto win = Row("SHOT", 1, "한성문화재연구원", 100.051, "2026-05-15 11:00:00", true);
    win.lower_rate = 88.0;
    shot.push_back(win);
    auto second = Row("SHOT", 2, "한울문화재연구원", 99.430, "2026-05-15 11:00:00", false);
    second.lower_rate = 88.0;
    shot.push_back(second);
    const auto shot_table = nara::BuildAwardNoticeRows(shot, "2025-08-26", "2026-08-26");
    if (shot_table.size() != 1) {
        return Fail("screenshot notice must remain on the 동국 table");
    }
    if (!shot_table[0].has_award_rate || std::fabs(shot_table[0].award_rate - 100.051) > 0.0001) {
        return Fail("spread chips keep 100.051 1위 as 기초대비 낙찰");
    }
    if (shot_table[0].rank_label.find("100.051") == std::string::npos) {
        return Fail("표 낙찰 문구에 퍼진 창의 1위 100.051을 넣는다");
    }
    return 0;
}
