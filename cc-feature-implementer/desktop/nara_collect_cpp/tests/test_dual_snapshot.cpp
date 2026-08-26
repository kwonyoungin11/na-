#include "nara/award_table.h"
#include "nara/notice_rules.h"
#include "nara/scatter_chart.h"
#include "nara/sqlite_store.h"

#include <cmath>
#include <iostream>
#include <string>

int main() {
    const auto live_db =
        "A:\\c\\cc-feature-implementer\\desktop\\nara_collect_cpp\\build\\Release\\nara-dashboard.db";
    const auto ref_db = nara::WorkspaceDashboardDbPath();
    auto live = nara::LoadParticipants(live_db);
    const auto ref = nara::LoadParticipants(ref_db);
    if (live.empty() || ref.empty()) {
        std::cerr << "FAIL: need portable live db and workspace ref to overlay 창녕 rates\n";
        return 1;
    }
    nara::PreferChartAdjustRates(live, ref);
    const auto scatter = nara::BuildScatterNotice(live, "R26BK01673928-000", 99.0, 101.0);
    double rank1 = 0;
    bool found_rank1 = false;
    for (const auto& row : scatter.chips) {
        if (row.rank == 1 && row.adjust_rate) {
            rank1 = *row.adjust_rate;
            found_rank1 = true;
            break;
        }
    }
    if (!found_rank1) {
        std::cerr << "FAIL: 창녕 1위 칩이 없다\n";
        return 1;
    }
    if (rank1 >= 99.90 && rank1 <= 100.20) {
        std::cerr << "FAIL: 창녕 1위 그래프 좌표가 가짜 100.014 군집이다 rate=" << rank1
                  << " 아이건설 99.78004여야 한다\n";
        return 1;
    }
    if (std::fabs(rank1 - 99.78004) > 0.001) {
        std::cerr << "FAIL: 창녕 1위 기초대비사정률 expected 99.78004 got " << rank1 << "\n";
        return 1;
    }

    const auto chart = nara::BuildChartUniverse(live, "2025-08-26", "2026-08-26", "전국", 99.0, 101.0);
    const auto table = nara::BuildAwardNoticeRows(chart, "2025-08-26", "2026-08-26");
    bool has_changnyeong = false;
    for (const auto& row : table) {
        if (row.notice_number.find("R26BK01673928") == std::string::npos) {
            continue;
        }
        has_changnyeong = true;
        if (row.rank_label.find("개사") == std::string::npos) {
            std::cerr << "FAIL: C# rank label uses 개사, got " << row.rank_label << "\n";
            return 1;
        }
        if (row.rank_label.find("62위") != std::string::npos) {
            std::cerr << "FAIL: stored Rank leaked into C++ table\n";
            return 1;
        }
        if (!row.has_award_rate || std::fabs(row.award_rate - 99.78004) > 0.001) {
            std::cerr << "FAIL: 표 낙찰 사정률 must match 전장 맵 1위 99.78004 got " << row.award_rate
                      << " label=" << row.rank_label << "\n";
            return 1;
        }
        if (row.rank_label.find("100.014") != std::string::npos) {
            std::cerr << "FAIL: 표에 가짜 100.014가 남아 있다 " << row.rank_label << "\n";
            return 1;
        }
    }
    if (!has_changnyeong) {
        std::cerr << "FAIL: C# first row R26BK01673928-000 missing from C++ table\n";
        return 1;
    }
    return 0;
}
