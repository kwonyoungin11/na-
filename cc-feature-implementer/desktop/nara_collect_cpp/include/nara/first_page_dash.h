#pragma once

#include "nara/award_table.h"
#include "nara/dongguk_band.h"
#include "nara/igunsul_convert.h"
#include "nara/scatter_chart.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <map>
#include <string>
#include <vector>

namespace nara {

constexpr int kFirstPageHistBars = 12;
constexpr int kFirstPageHistBand0 = 1;
constexpr int kFirstPageHomeBin = 5;
constexpr int kFirstPageWinnerTop = 5;
constexpr int kFirstPageWatchTop = 6;

struct MonthPoint {
    std::string ym;
    int notice_n = 0;
    int rank1_n = 0;
    double dong_median = 0;
    double win_median = 0;
    double gap_median = 0;
    double nfirm_median = 0;
    double rank_median = 0;
};

struct NamedCount {
    std::string name;
    std::string label;
    int n = 0;
};

struct FirmStat {
    std::string key;
    std::string name;
    std::string label;
    int notice_n = 0;
    int rank1_n = 0;
    bool dongguk = false;
};

std::vector<FirmStat> BuildFirmStats(const std::vector<ParticipantRow>& snapshot);

struct FirstPageDash {
    int notice_n = 0;
    int dong_rank1_n = 0;
    int dong_top10_n = 0;
    int rank_median = 0;
    double dong_rate_median = 0;
    std::string latest_open;
    std::array<int, kFirstPageHistBars> win_bin_n{};
    int peak_bin = -1;
    int home_bin = kFirstPageHomeBin;
    std::array<int, 8> rank_bucket_n{};
    std::vector<MonthPoint> months;
    std::vector<NamedCount> watch;
    std::vector<NamedCount> winners;
    int winner_other_n = 0;
    int watch_once_n = 0;
};

inline int ParseLeadingInt(const char* s) {
    while (*s && (*s < '0' || *s > '9')) {
        ++s;
    }
    if (*s < '0' || *s > '9') {
        return 0;
    }
    int n = 0;
    while (*s >= '0' && *s <= '9') {
        n = n * 10 + (*s - '0');
        ++s;
    }
    return n;
}

inline int ParseDonggukRank(const std::string& label) {
    const auto slash = label.find(" / ");
    if (slash == std::string::npos) {
        return 0;
    }
    return ParseLeadingInt(label.c_str() + slash + 3);
}

inline int ParseCompanyN(const std::string& label) {
    const std::string mark = "위 · ";
    const auto mid = label.find(mark);
    if (mid == std::string::npos) {
        return 0;
    }
    return ParseLeadingInt(label.c_str() + mid + mark.size());
}

inline std::string YearMonth(const std::string& open_date) {
    if (open_date.size() < 7) {
        return {};
    }
    return open_date.substr(0, 7);
}

inline std::string FormatOpenDay(const std::string& open_date) {
    if (open_date.size() >= 10) {
        return open_date.substr(0, 10);
    }
    return open_date;
}

inline std::string FormatDataSpan(const std::vector<AwardNoticeRow>& table) {
    std::string lo;
    std::string hi;
    for (const auto& row : table) {
        const auto day = FormatOpenDay(row.open_date);
        if (day.empty()) {
            continue;
        }
        if (lo.empty() || day < lo) {
            lo = day;
        }
        if (hi.empty() || day > hi) {
            hi = day;
        }
    }
    if (lo.empty()) {
        return {};
    }
    return lo + "부터 " + hi + "까지";
}

inline std::string FirstPageHistLabel(int display_bin) {
    if (display_bin < 0 || display_bin >= kFirstPageHistBars) {
        return {};
    }
    const double start = kBandWinLo + static_cast<double>(kFirstPageHistBand0 + display_bin) * kBandWinBin;
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%.2f", start);
    return buf;
}

inline double MedianD(std::vector<double> xs) {
    if (xs.empty()) {
        return 0;
    }
    std::sort(xs.begin(), xs.end());
    const size_t n = xs.size();
    if (n % 2 == 1) {
        return xs[n / 2];
    }
    return (xs[n / 2 - 1] + xs[n / 2]) * 0.5;
}

inline int MedianI(std::vector<int> xs) {
    if (xs.empty()) {
        return 0;
    }
    std::sort(xs.begin(), xs.end());
    const size_t n = xs.size();
    if (n % 2 == 1) {
        return xs[n / 2];
    }
    return static_cast<int>(std::lround((xs[n / 2 - 1] + xs[n / 2]) * 0.5));
}

inline FirstPageDash BuildFirstPageDash(const std::vector<AwardNoticeRow>& table, const BandResult& band) {
    FirstPageDash out;
    out.notice_n = static_cast<int>(table.size());
    if (!table.empty()) {
        out.latest_open = table.front().open_date;
        for (const auto& row : table) {
            if (row.open_date > out.latest_open) {
                out.latest_open = row.open_date;
            }
        }
    }
    std::vector<int> ranks;
    std::vector<double> dong_rates;
    std::map<std::string, std::vector<AwardNoticeRow>> by_month;
    std::map<std::string, int> winner_n;
    for (const auto& row : table) {
        const int rank = ParseDonggukRank(row.rank_label);
        if (rank > 0) {
            ranks.push_back(rank);
            if (rank == 1) {
                ++out.dong_rank1_n;
            }
            if (rank <= 10) {
                ++out.dong_top10_n;
            }
            const int bucket = (std::min)(7, (rank - 1) / 10);
            out.rank_bucket_n[static_cast<size_t>(bucket)] += 1;
        }
        dong_rates.push_back(row.dongguk_rate);
        const auto ym = YearMonth(row.open_date);
        if (!ym.empty()) {
            by_month[ym].push_back(row);
        }
        if (row.has_award_rate && !row.award_company.empty()) {
            winner_n[row.award_company] += 1;
        }
    }
    out.rank_median = MedianI(ranks);
    out.dong_rate_median = MedianD(dong_rates);

    for (int i = 0; i < kFirstPageHistBars; ++i) {
        const int band_i = kFirstPageHistBand0 + i;
        if (band_i >= 0 && band_i < kBandWinBinCount) {
            out.win_bin_n[static_cast<size_t>(i)] = band.win_bin_n[static_cast<size_t>(band_i)];
        }
    }
    int peak = -1;
    int peak_n = 0;
    for (int i = 0; i < kFirstPageHistBars; ++i) {
        if (out.win_bin_n[static_cast<size_t>(i)] > peak_n) {
            peak_n = out.win_bin_n[static_cast<size_t>(i)];
            peak = i;
        }
    }
    out.peak_bin = peak_n > 0 ? peak : -1;
    out.home_bin = kFirstPageHomeBin;

    for (const auto& kv : by_month) {
        MonthPoint m;
        m.ym = kv.first;
        m.notice_n = static_cast<int>(kv.second.size());
        std::vector<double> dong;
        std::vector<double> win;
        std::vector<double> gap;
        std::vector<double> nfirm;
        std::vector<int> mr;
        for (const auto& row : kv.second) {
            dong.push_back(row.dongguk_rate);
            if (row.has_award_rate) {
                win.push_back(row.award_rate);
                gap.push_back(row.award_rate - row.dongguk_rate);
            }
            const int n = ParseCompanyN(row.rank_label);
            if (n > 0) {
                nfirm.push_back(static_cast<double>(n));
            }
            const int rank = ParseDonggukRank(row.rank_label);
            if (rank > 0) {
                mr.push_back(rank);
                if (rank == 1) {
                    m.rank1_n += 1;
                }
            }
        }
        m.dong_median = MedianD(dong);
        m.win_median = MedianD(win);
        m.gap_median = MedianD(gap);
        m.nfirm_median = MedianD(nfirm);
        m.rank_median = static_cast<double>(MedianI(mr));
        out.months.push_back(std::move(m));
    }

    std::vector<nara::WatchFirm> houses = band.watch_firms;
    std::sort(houses.begin(), houses.end(),
              [](const WatchFirm& a, const WatchFirm& b) { return a.notice_n > b.notice_n; });
    std::vector<std::string> watch_names;
    for (const auto& firm : houses) {
        if (!firm.regular) {
            ++out.watch_once_n;
            continue;
        }
        if (static_cast<int>(out.watch.size()) >= kFirstPageWatchTop) {
            continue;
        }
        NamedCount c;
        c.name = firm.company;
        c.n = firm.notice_n;
        out.watch.push_back(c);
        watch_names.push_back(firm.company);
    }
    const auto watch_map = BuildDisplayMap(watch_names);
    for (auto& c : out.watch) {
        c.label = DisplayLabelFor(c.name, watch_map);
        if (c.label.empty()) {
            c.label = BuildShortLabel(c.name);
        }
    }

    std::vector<std::pair<int, std::string>> ranked;
    ranked.reserve(winner_n.size());
    for (const auto& kv : winner_n) {
        ranked.push_back({kv.second, kv.first});
    }
    std::sort(ranked.begin(), ranked.end(), [](const auto& a, const auto& b) {
        if (a.first != b.first) {
            return a.first > b.first;
        }
        return a.second < b.second;
    });
    std::vector<std::string> win_names;
    int shown = 0;
    for (const auto& kv : ranked) {
        if (shown < kFirstPageWinnerTop) {
            NamedCount c;
            c.name = kv.second;
            c.n = kv.first;
            out.winners.push_back(c);
            win_names.push_back(kv.second);
            ++shown;
        } else {
            out.winner_other_n += kv.first;
        }
    }
    const auto win_map = BuildDisplayMap(win_names);
    for (auto& c : out.winners) {
        c.label = DisplayLabelFor(c.name, win_map);
        if (c.label.empty()) {
            c.label = BuildShortLabel(c.name);
        }
    }
    return out;
}

}  // namespace nara
