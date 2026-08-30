#include "nara/award_table.h"

#include "nara/igunsul_convert.h"
#include "nara/notice_rules.h"
#include "nara/scatter_chart.h"

#include <algorithm>
#include <cctype>
#include <map>
#include <string>
#include <unordered_set>
#include <vector>

namespace nara {
namespace {

std::string NoticeKey(const ParticipantRow& row) {
    return row.notice_number.empty() ? row.notice_no : row.notice_number;
}

std::string Day(const std::string& open_date) {
    return open_date.size() >= 10 ? open_date.substr(0, 10) : open_date;
}

bool IsRetired(const ParticipantRow& row) {
    auto text = row.company_name + row.normalized_company_name;
    text.erase(std::remove_if(text.begin(), text.end(), [](unsigned char ch) { return std::isspace(ch); }), text.end());
    return text.find("효림") != std::string::npos;
}

}  // namespace

std::string CanonicalizeRegion(const std::string& raw) {
    if (raw.find("경상북") != std::string::npos || raw.find("경북") != std::string::npos) {
        return "경북";
    }
    if (raw.find("전국") != std::string::npos) {
        return "전국";
    }
    return {};
}

bool MatchesRegion(const std::string& source, const std::string& selected) {
    const auto want = CanonicalizeRegion(selected);
    if (want.empty()) {
        return false;
    }
    return CanonicalizeRegion(source) == want;
}

namespace {

bool InAdjustWindow(const ParticipantRow& row, double min_rate, double max_rate) {
    if (!row.adjust_rate) {
        return false;
    }
    return *row.adjust_rate >= min_rate && *row.adjust_rate <= max_rate;
}

}  // namespace

std::vector<ParticipantRow> BuildChartUniverse(
    const std::vector<ParticipantRow>& rows,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region,
    double min_rate,
    double max_rate) {
    const auto persist_rows = ApplyPersistRules(rows);
    std::unordered_set<std::string> persist_keys;
    for (const auto& row : persist_rows) {
        persist_keys.insert(NoticeKey(row));
    }
    std::vector<ParticipantRow> filtered;
    filtered.reserve(rows.size());
    for (const auto& row : rows) {
        if (persist_keys.count(NoticeKey(row)) == 0) {
            continue;
        }
        if (IsRetired(row)) {
            continue;
        }
        const auto day = Day(row.open_date);
        if (day < start_date || day > end_date) {
            continue;
        }
        if (!MatchesRegion(row.region, region)) {
            continue;
        }
        if (!InAdjustWindow(row, min_rate, max_rate)) {
            continue;
        }
        filtered.push_back(row);
    }
    return ApplyChartRules(filtered);
}

std::vector<AwardNoticeRow> BuildAwardNoticeRows(
    const std::vector<ParticipantRow>& chart_rows,
    const std::string& start_date,
    const std::string& end_date) {
    const auto window = InPeriod(chart_rows, start_date, end_date);
    std::map<std::string, std::vector<ParticipantRow>> by_notice;
    for (const auto& row : window) {
        if (!IsDonggukCompany(row) || !row.adjust_rate) {
            continue;
        }
        by_notice[NoticeKey(row)].push_back(row);
    }

    struct Sortable {
        std::string open_date;
        AwardNoticeRow row;
    };
    std::vector<Sortable> items;
    for (const auto& [notice, dongguk_rows] : by_notice) {
        const auto scatter = BuildScatterNotice(chart_rows, notice, 99.0, 101.0);
        const ParticipantRow* dongguk = nullptr;
        for (const auto& row : dongguk_rows) {
            if (!dongguk || row.open_date > dongguk->open_date) {
                dongguk = &row;
            }
        }
        if (!dongguk) {
            continue;
        }
        const ParticipantRow* awarded = nullptr;
        for (const auto& row : chart_rows) {
            if (NoticeKey(row) != notice || !row.adjust_rate) {
                continue;
            }
            if (!(row.is_awarded || row.rank == 1)) {
                continue;
            }
            if (!awarded || row.open_date > awarded->open_date) {
                awarded = &row;
            }
        }
        if (!awarded || !awarded->adjust_rate || !dongguk->adjust_rate) {
            continue;
        }
        std::vector<ParticipantRow> notice_rows;
        for (const auto& row : chart_rows) {
            if (NoticeKey(row) == notice) {
                notice_rows.push_back(row);
            }
        }
        const bool fake_award = NoticeHasLowerLimitAwardCluster(notice_rows);
        AwardNoticeRow out;
        out.open_date = dongguk->open_date;
        out.notice_number = notice;
        out.notice_name = dongguk->notice_name;
        out.ordering_agency = dongguk->ordering_agency;
        out.region = dongguk->region;
        out.dongguk_rate = *dongguk->adjust_rate;
        out.has_award_rate = !fake_award;
        if (out.has_award_rate) {
            out.award_rate = *awarded->adjust_rate;
            out.award_company = awarded->company_name.empty() ? awarded->normalized_company_name : awarded->company_name;
            out.rank_label = FormatDonggukRankLabel(scatter, *dongguk->adjust_rate, *awarded->adjust_rate);
        } else {
            out.rank_label = FormatDonggukRankLabel(scatter, *dongguk->adjust_rate);
        }
        items.push_back(Sortable{dongguk->open_date, std::move(out)});
    }
    std::sort(items.begin(), items.end(), [](const Sortable& a, const Sortable& b) {
        if (a.open_date != b.open_date) {
            return a.open_date > b.open_date;
        }
        return a.row.notice_number < b.row.notice_number;
    });
    std::vector<AwardNoticeRow> rows;
    rows.reserve(items.size());
    for (auto& item : items) {
        rows.push_back(std::move(item.row));
    }
    return rows;
}

}  // namespace nara
