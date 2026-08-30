#include "nara/scatter_chart.h"

#include <algorithm>
#include <cmath>
#include <map>
#include <set>
#include <sstream>

namespace nara {
namespace {

std::string CompanyKey(const ParticipantRow& row) {
    return CompanyIdentity(row);
}

bool SameNotice(const ParticipantRow& row, const std::string& notice) {
    return row.notice_number == notice || row.notice_no == notice;
}

bool IsDongguk(const ParticipantRow& row) {
    const auto key = CompanyKey(row);
    return key.find("동국문화재연구원") != std::string::npos;
}

std::vector<ParticipantRow> UniqueCompanies(const std::vector<ParticipantRow>& rows) {
    std::map<std::string, ParticipantRow> unique;
    for (const auto& row : rows) {
        const auto key = CompanyKey(row);
        if (key.empty()) {
            continue;
        }
        auto found = unique.find(key);
        if (found == unique.end()) {
            unique.emplace(key, row);
            continue;
        }
        if (row.rank > 0 && (found->second.rank <= 0 || row.rank < found->second.rank)) {
            found->second = row;
        }
    }
    std::vector<ParticipantRow> ordered;
    ordered.reserve(unique.size());
    for (auto& pair : unique) {
        ordered.push_back(pair.second);
    }
    std::sort(ordered.begin(), ordered.end(), [](const ParticipantRow& a, const ParticipantRow& b) {
        const int ra = a.rank > 0 ? a.rank : 1'000'000;
        const int rb = b.rank > 0 ? b.rank : 1'000'000;
        if (ra != rb) {
            return ra < rb;
        }
        return CompanyKey(a) < CompanyKey(b);
    });
    return ordered;
}

}  // namespace

ScatterNotice BuildScatterNotice(
    const std::vector<ParticipantRow>& rows,
    const std::string& notice_number,
    double min_rate,
    double max_rate) {
    ScatterNotice scatter;
    scatter.notice_number = notice_number;
    std::vector<ParticipantRow> notice_rows;
    for (const auto& row : rows) {
        if (SameNotice(row, notice_number)) {
            notice_rows.push_back(row);
        }
    }
    const auto unique = UniqueCompanies(notice_rows);
    scatter.total_companies = static_cast<int>(unique.size());
    for (const auto& row : unique) {
        if (!row.adjust_rate || *row.adjust_rate < min_rate || *row.adjust_rate > max_rate) {
            continue;
        }
        scatter.chips.push_back(row);
    }
    scatter.chip_count = static_cast<int>(scatter.chips.size());
    for (int i = 0; i < scatter.chip_count; ++i) {
        if (IsDongguk(scatter.chips[i])) {
            scatter.dongguk_rank = i + 1;
            break;
        }
    }
    return scatter;
}

std::vector<ParticipantRow> InPeriod(
    const std::vector<ParticipantRow>& rows,
    const std::string& start_date,
    const std::string& end_date) {
    auto start = start_date.size() >= 10 ? start_date.substr(0, 10) : start_date;
    auto end = end_date.size() >= 10 ? end_date.substr(0, 10) : end_date;
    if (end < start) {
        std::swap(start, end);
    }
    std::vector<ParticipantRow> kept;
    for (const auto& row : rows) {
        auto day = row.open_date.size() >= 10 ? row.open_date.substr(0, 10) : row.open_date;
        if (day >= start && day <= end) {
            kept.push_back(row);
        }
    }
    return kept;
}

bool IsLowerLimitAwardRate(double rate) {
    return rate >= 99.90 && rate <= 100.20;
}

bool LooksLikeOpenApiHundred(const ParticipantRow& row) {
    return row.adjust_rate && IsLowerLimitAwardRate(*row.adjust_rate) && row.lower_rate
           && *row.lower_rate > 50.0 && *row.lower_rate < 95.0;
}

bool NoticeHasLowerLimitAwardCluster(const std::vector<ParticipantRow>& rows) {
    int usable = 0;
    int near100 = 0;
    double lo = 1e9;
    double hi = -1e9;
    for (const auto& row : rows) {
        if (row.rank <= 0 || !row.adjust_rate) {
            continue;
        }
        if (IsDongguk(row)) {
            continue;
        }
        const double rate = *row.adjust_rate;
        if (rate < 99.0 || rate > 101.0) {
            continue;
        }
        ++usable;
        if (IsLowerLimitAwardRate(rate)) {
            ++near100;
            lo = (std::min)(lo, rate);
            hi = (std::max)(hi, rate);
        }
    }
    return usable >= 2 && near100 == usable && (hi - lo) <= 0.25;
}

bool IsMappedRank1(const ParticipantRow& chip, const std::vector<ParticipantRow>& notice_chips) {
    return chip.rank == 1 && !NoticeHasLowerLimitAwardCluster(notice_chips);
}

void PreferChartAdjustRates(
    std::vector<ParticipantRow>& live,
    const std::vector<ParticipantRow>& reference) {
    auto notice_of = [](const ParticipantRow& row) {
        return row.notice_number.empty() ? row.notice_no : row.notice_number;
    };
    auto overlay_key = [&](const ParticipantRow& row) {
        return notice_of(row) + "\x1f" + CompanyKey(row);
    };
    auto has_nara_adjust = [](const ParticipantRow& row) {
        return row.adjust_rate && *row.adjust_rate >= 99.0 && *row.adjust_rate <= 101.0;
    };
    auto looks_lower_award = [](const ParticipantRow& row) {
        return row.adjust_rate && row.lower_rate && *row.adjust_rate >= 99.90 && *row.adjust_rate <= 100.20;
    };

    std::map<std::string, const ParticipantRow*> ref_by_key;
    std::map<std::string, const ParticipantRow*> ref_rank1;
    for (const auto& row : reference) {
        if (!row.adjust_rate) {
            continue;
        }
        const auto key = overlay_key(row);
        auto found = ref_by_key.find(key);
        if (found == ref_by_key.end()
            || (row.rank > 0 && (found->second->rank <= 0 || row.rank < found->second->rank))) {
            ref_by_key[key] = &row;
        }
        if (row.rank == 1) {
            ref_rank1[notice_of(row)] = &row;
        }
    }

    std::map<std::string, std::vector<ParticipantRow*>> by_notice;
    for (auto& row : live) {
        by_notice[notice_of(row)].push_back(&row);
    }

    for (auto& [notice, rows] : by_notice) {
        int open96 = 0;
        int rated = 0;
        double live_rank1_raw = 0;
        bool have_live_rank1 = false;
        std::vector<ParticipantRow> notice_copy;
        notice_copy.reserve(rows.size());
        for (const auto* row : rows) {
            notice_copy.push_back(*row);
            if (!row->adjust_rate) {
                continue;
            }
            ++rated;
            const double rate = *row->adjust_rate;
            if (rate >= 90.0 && rate < 99.0) {
                ++open96;
            }
            if (row->rank == 1) {
                live_rank1_raw = rate;
                have_live_rank1 = true;
            }
        }
        const bool lower_cluster = NoticeHasLowerLimitAwardCluster(notice_copy);
        const bool api96 = rated >= 1 && open96 * 2 >= rated;
        const auto ref1 = ref_rank1.find(notice);

        if (api96 && have_live_rank1 && ref1 != ref_rank1.end() && ref1->second->adjust_rate) {
            const double rank1_chart = *ref1->second->adjust_rate;
            for (auto* row : rows) {
                if (!row->adjust_rate) {
                    continue;
                }
                row->adjust_rate = ToChartAdjustRate(*row->adjust_rate, live_rank1_raw, rank1_chart);
            }
            continue;
        }

        for (auto* row : rows) {
            const auto found = ref_by_key.find(overlay_key(*row));
            if (found == ref_by_key.end()) {
                continue;
            }
            const bool keep_dongguk_scale =
                has_nara_adjust(*row) && row->adjust_rate && *row->adjust_rate < 99.90;
            const bool nara_missing =
                !keep_dongguk_scale
                && (!has_nara_adjust(*row) || lower_cluster || looks_lower_award(*row));
            if (!nara_missing) {
                continue;
            }
            const auto& src = *found->second;
            row->adjust_rate = src.adjust_rate;
            if (src.rank > 0) {
                row->rank = src.rank;
                row->is_awarded = src.rank == 1;
            }
        }
    }

    std::set<std::string> live_keys;
    for (const auto& row : live) {
        live_keys.insert(overlay_key(row));
    }
    for (const auto& row : reference) {
        if (!row.adjust_rate || *row.adjust_rate < 99.0 || *row.adjust_rate > 101.0) {
            continue;
        }
        const auto key = overlay_key(row);
        if (live_keys.count(key) != 0) {
            continue;
        }
        live.push_back(row);
        live_keys.insert(key);
    }
}

double ToChartAdjustRate(double raw, double rank1_raw, double rank1_chart) {
    if (raw >= 99.0 && raw <= 101.0) {
        return raw;
    }
    if (std::fabs(raw) <= 10.0) {
        return std::round((100.0 + raw) * 100000.0) / 100000.0;
    }
    return std::round((raw + (rank1_chart - rank1_raw)) * 100000.0) / 100000.0;
}

std::string FormatDonggukRankLabel(
    const ScatterNotice& scatter,
    double dongguk_rate) {
    std::ostringstream out;
    out.setf(std::ios::fixed);
    out.precision(5);
    out << "동국 " << dongguk_rate << " / " << scatter.dongguk_rank << "위 · " << scatter.chip_count
        << "개사";
    return out.str();
}

std::string FormatDonggukRankLabel(
    const ScatterNotice& scatter,
    double dongguk_rate,
    double award_rate) {
    std::ostringstream out;
    out << FormatDonggukRankLabel(scatter, dongguk_rate);
    out.setf(std::ios::fixed);
    out.precision(5);
    out << " · 낙찰 " << award_rate;
    return out.str();
}

}  // namespace nara
