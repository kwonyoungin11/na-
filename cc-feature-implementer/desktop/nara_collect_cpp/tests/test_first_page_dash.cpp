#include "nara/first_page_dash.h"

#include <cmath>
#include <iostream>
#include <string>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

nara::AwardNoticeRow Notice(const std::string& day, const std::string& label, const std::string& winner,
                            double award, double dong, bool has_award = true) {
    nara::AwardNoticeRow row;
    row.open_date = day;
    row.notice_number = "N-" + day;
    row.notice_name = "공고";
    row.region = "전국";
    row.rank_label = label;
    row.award_company = winner;
    row.award_rate = award;
    row.dongguk_rate = dong;
    row.has_award_rate = has_award;
    return row;
}

}  // namespace

int main() {
    const auto empty = nara::BuildFirstPageDash({}, {});
    if (empty.notice_n != 0 || empty.dong_rank1_n != 0 || empty.dong_top10_n != 0 || empty.rank_median != 0) {
        return Fail("empty table must not invent 124/5/48");
    }
    if (!empty.months.empty() || !empty.winners.empty() || empty.winner_other_n != 0) {
        return Fail("empty table has no month series and no winner bars");
    }
    for (int n : empty.win_bin_n) {
        if (n != 0) {
            return Fail("empty band copies zero 1위 bins, not mock 14");
        }
    }

    if (nara::ParseDonggukRank("동국 99.60535 / 6위 · 6개사") != 6) {
        return Fail("parse rank from FormatDonggukRankLabel");
    }
    if (nara::ParseCompanyN("동국 99.60535 / 6위 · 6개사 · 낙찰 99.78004") != 6) {
        return Fail("parse N개사 from rank label");
    }
    if (nara::YearMonth("2026-08-26 11:00:00") != "2026-08") {
        return Fail("year-month is first 7 chars of open_date");
    }
    if (!nara::FormatDataSpan({}).empty()) {
        return Fail("empty table has no invented date span");
    }
    {
        nara::AwardNoticeRow a;
        a.open_date = "2026-08-26 11:00:00";
        nara::AwardNoticeRow b;
        b.open_date = "2025-09-03 09:00:00";
        if (nara::FormatDataSpan({a, b}) != "2025-09-03부터 2026-08-26까지") {
            return Fail("data span is min~max open_date from the table");
        }
    }

    nara::BandResult band;
    band.win_bin_n[6] = 3;
    band.win_bin_n[10] = 7;
    band.watch_firms.push_back({"재단법인 미래문화유산연구원", 18, true});
    band.watch_firms.push_back({"한울문화재연구원", 14, true});
    band.watch_firms.push_back({"한번업체", 1, false});

    const std::vector<nara::AwardNoticeRow> rows = {
        Notice("2026-08-26 11:00:00", "동국 99.90000 / 1위 · 52개사", "재단법인 미래문화유산연구원", 99.906, 99.900),
        Notice("2026-08-20 11:00:00", "동국 99.38500 / 58위 · 74개사", "재단법인 춘추문화유산연구원", 99.780, 99.385),
        Notice("2026-07-22 11:00:00", "동국 99.55000 / 3위 · 68개사", "재단법인 미래문화유산연구원", 99.440, 99.550),
        Notice("2026-07-04 11:00:00", "동국 99.61000 / 12위 · 72개사", "경산문화유산연구원", 100.132, 99.610),
    };
    const auto dash = nara::BuildFirstPageDash(rows, band);
    if (dash.notice_n != 4) {
        return Fail("notice_n is table size, not a mock constant");
    }
    if (dash.dong_rank1_n != 1 || dash.dong_top10_n != 2) {
        std::cerr << "rank1 " << dash.dong_rank1_n << " top10 " << dash.dong_top10_n << "\n";
        return Fail("1위 and 10위 안 counts come from parsed rank_label");
    }
    if (dash.win_bin_n[5] != 3 || dash.win_bin_n[9] != 7) {
        return Fail("mosaic 12 bars are band bins 1-12, copied not invented");
    }
    if (dash.peak_bin != 9) {
        return Fail("peak bar is the max real 1위 bin");
    }
    if (dash.home_bin != 5) {
        return Fail("99.60 home bar is band index 6 → display 5");
    }
    if (dash.months.size() != 2 || dash.months[0].ym != "2026-07" || dash.months[1].ym != "2026-08") {
        return Fail("month series is chronological unique YYYY-MM from open_date");
    }
    if (dash.months[1].notice_n != 2 || dash.months[0].notice_n != 2) {
        return Fail("month notice_n counts real rows");
    }
    if (dash.winners.size() != 3 || dash.winners[0].n != 2) {
        return Fail("winner bars count award_company; 미래 has 2");
    }
    if (dash.winner_other_n != 0) {
        return Fail("no leftover 기타 when every winner is in the top 5");
    }
    if (dash.watch.size() < 2 || dash.watch[0].n != 18) {
        return Fail("watch bars are regular BandResult firms, sorted by notice_n");
    }
    if (std::fabs(dash.months[1].gap_median - (99.906 - 99.900 + 99.780 - 99.385) / 2.0) > 0.02) {
        return Fail("gap series is 1위 사정률 − 동국 사정률 from the same rows");
    }
    if (dash.latest_open.find("2026-08-26") == std::string::npos) {
        return Fail("latest KPI is newest table open_date");
    }
    if (nara::FirstPageHistLabel(0) != "99.10" || nara::FirstPageHistLabel(5) != "99.60"
        || nara::FirstPageHistLabel(9) != "100.00") {
        return Fail("bar labels are 칸 시작 기초대비사정률 99.10 step 0.10");
    }

    if (!nara::BuildFirmStats({}).empty()) {
        return Fail("empty snapshot has no invented firms");
    }
    nara::ParticipantRow dong_a;
    dong_a.notice_number = "A";
    dong_a.notice_no = "A";
    dong_a.company_name = "동국문화재연구원";
    dong_a.normalized_company_name = "동국문화재연구원";
    dong_a.rank = 2;
    dong_a.adjust_rate = 99.20;
    dong_a.open_date = "2026-08-01";
    nara::ParticipantRow win_a = dong_a;
    win_a.company_name = "한울문화재연구원";
    win_a.normalized_company_name = "한울문화재연구원";
    win_a.rank = 1;
    win_a.is_awarded = true;
    win_a.adjust_rate = 99.10;
    nara::ParticipantRow extra_a = dong_a;
    extra_a.company_name = "가온문화재연구원";
    extra_a.normalized_company_name = "가온문화재연구원";
    extra_a.rank = 3;
    extra_a.adjust_rate = 99.30;
    nara::ParticipantRow pad1 = extra_a;
    pad1.company_name = "화서문화재연구원";
    pad1.normalized_company_name = pad1.company_name;
    pad1.rank = 4;
    pad1.adjust_rate = 99.40;
    nara::ParticipantRow pad2 = extra_a;
    pad2.company_name = "미리문화재연구원";
    pad2.normalized_company_name = pad2.company_name;
    pad2.rank = 5;
    pad2.adjust_rate = 99.50;
    nara::ParticipantRow pad3 = extra_a;
    pad3.company_name = "낙찰문화재연구원";
    pad3.normalized_company_name = pad3.company_name;
    pad3.rank = 6;
    pad3.adjust_rate = 99.60;
    auto clone_b = [](nara::ParticipantRow r) {
        r.notice_number = "B";
        r.notice_no = "B";
        r.open_date = "2026-08-10";
        return r;
    };
    nara::ParticipantRow dong_b = clone_b(dong_a);
    dong_b.rank = 1;
    dong_b.is_awarded = true;
    dong_b.adjust_rate = 99.11;
    nara::ParticipantRow win_b = clone_b(win_a);
    win_b.rank = 2;
    win_b.is_awarded = false;
    win_b.adjust_rate = 99.22;
    const std::vector<nara::ParticipantRow> snap = {dong_a, win_a, extra_a, pad1, pad2, pad3,
                                                    dong_b, win_b, clone_b(extra_a), clone_b(pad1), clone_b(pad2),
                                                    clone_b(pad3)};
    const auto firms = nara::BuildFirmStats(snap);
    if (firms.size() != 6) {
        std::cerr << "firms " << firms.size() << "\n";
        return Fail("firm board lists every unique competitor in the snapshot, including 동국");
    }
    if (!firms.front().dongguk || firms.front().notice_n != 2 || firms.front().rank1_n != 1) {
        return Fail("동국 is first: 2 notices, 1 mapped 1위");
    }
    bool found_hanul = false;
    for (const auto& f : firms) {
        if (f.name.find("한울") == std::string::npos) {
            continue;
        }
        found_hanul = true;
        if (f.notice_n != 2 || f.rank1_n != 1) {
            return Fail("한울 participated twice and was 1위 once");
        }
    }
    if (!found_hanul) {
        return Fail("competitors besides 동국 stay on the board");
    }
    std::cerr << "ok first page dash uses table and band rows only\n";
    return 0;
}
