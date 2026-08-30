#include "nara/dongguk_band.h"

#include "nara/award_collect.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <map>
#include <set>
#include <sstream>

namespace nara {
namespace {

constexpr double kMinRate = 99.0;
constexpr double kMaxRate = 101.0;
constexpr double kSelfHouseIqr = 0.15;
constexpr double kRivalHouseIqr = 0.50;
constexpr int kMinPoints = 8;
constexpr int kClingCount = 3;
constexpr double kClingPad = 0.20;
constexpr double kClingRatio = 0.20;
constexpr double kMinUseWidth = 0.05;
constexpr double kTick = 0.0001;

std::string DayOf(const std::string& value) {
    return value.size() >= 10 ? value.substr(0, 10) : value;
}

std::string NoticeOf(const ParticipantRow& row) {
    return row.notice_number.empty() ? row.notice_no : row.notice_number;
}

bool ParseDay(const std::string& day, int& year, int& month, int& dom) {
    if (day.size() < 10 || day[4] != '-' || day[7] != '-') {
        return false;
    }
    year = std::atoi(day.c_str());
    month = std::atoi(day.c_str() + 5);
    dom = std::atoi(day.c_str() + 8);
    return year >= 2000 && month >= 1 && month <= 12 && dom >= 1 && dom <= 31;
}

int Dim(int year, int month) {
    static const int kDays[] = {0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
    if (month == 2 && ((year % 4 == 0 && year % 100 != 0) || year % 400 == 0)) {
        return 29;
    }
    return kDays[month];
}

std::string ShiftMonths(const std::string& day, int delta_months) {
    int year = 0;
    int month = 0;
    int dom = 0;
    if (!ParseDay(DayOf(day), year, month, dom)) {
        return {};
    }
    month += delta_months;
    while (month <= 0) {
        month += 12;
        year -= 1;
    }
    while (month > 12) {
        month -= 12;
        year += 1;
    }
    dom = (std::min)(dom, Dim(year, month));
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%04d-%02d-%02d", year, month, dom);
    return buf;
}

bool InDayRange(const std::string& day, const std::string& start, const std::string& end) {
    if (day.empty() || start.empty() || end.empty()) {
        return false;
    }
    return day >= start && day <= end;
}

bool InRate(double rate) {
    return rate >= kMinRate && rate <= kMaxRate;
}

bool IsDongguk(const ParticipantRow& row) {
    return CompanyIdentity(row).find("동국문화재연구원") != std::string::npos;
}

double Quantile(const std::vector<double>& sorted, double p) {
    if (sorted.empty()) {
        return 0;
    }
    if (sorted.size() == 1) {
        return sorted[0];
    }
    const double idx = p * static_cast<double>(sorted.size() - 1);
    const size_t lo = static_cast<size_t>(std::floor(idx));
    const size_t hi = static_cast<size_t>(std::ceil(idx));
    const double t = idx - static_cast<double>(lo);
    return sorted[lo] * (1.0 - t) + sorted[hi] * t;
}

double Round4(double value) {
    return std::round(value * 10000.0) / 10000.0;
}

std::string Fmt2(double value) {
    char buf[32];
    std::snprintf(buf, sizeof(buf), "%.2f", value);
    return buf;
}

std::string Fmt3(double value) {
    char buf[32];
    std::snprintf(buf, sizeof(buf), "%.3f", value);
    return buf;
}

std::string JoinChecks(std::vector<std::string> names) {
    std::set<std::string> unique;
    for (const auto& name : names) {
        const auto key = CanonicalizeCompany(name);
        if (!key.empty()) {
            unique.insert(key);
        }
    }
    std::string out;
    for (const auto& key : unique) {
        if (!out.empty()) {
            out += "|";
        }
        out += key;
    }
    return out;
}

std::string MaxOpen(const std::vector<ParticipantRow>& rows) {
    std::string max_day;
    for (const auto& row : rows) {
        const auto day = DayOf(row.open_date);
        if (day > max_day) {
            max_day = day;
        }
    }
    return max_day;
}

struct Stats {
    std::vector<double> rates;
    double q1 = 0;
    double q3 = 0;
    double iqr = 0;
    double median = 0;
};

Stats MakeStats(std::vector<double> rates) {
    Stats stats;
    stats.rates = std::move(rates);
    std::sort(stats.rates.begin(), stats.rates.end());
    if (stats.rates.empty()) {
        return stats;
    }
    stats.q1 = Quantile(stats.rates, 0.25);
    stats.q3 = Quantile(stats.rates, 0.75);
    stats.median = Quantile(stats.rates, 0.50);
    stats.iqr = stats.q3 - stats.q1;
    return stats;
}

bool IntervalsOverlap(double a0, double a1, double b0, double b1) {
    return a0 < b1 && b0 < a1;
}

void ExcludePrev(double x_prev, bool have_prev, double& lo, double& hi) {
    if (!have_prev) {
        return;
    }
    const double x = Round4(x_prev);
    if (!(lo <= x && x < hi)) {
        return;
    }
    const double left = x - lo;
    const double right = hi - x;
    if (right > left) {
        lo = x + kTick;
    } else {
        hi = x;
    }
}

int UniqueCompaniesOnNotice(const std::vector<ParticipantRow>& rows, const std::string& notice) {
    std::set<std::string> names;
    for (const auto& row : rows) {
        if (NoticeOf(row) != notice) {
            continue;
        }
        const auto key = CompanyIdentity(row);
        if (!key.empty()) {
            names.insert(key);
        }
    }
    return static_cast<int>(names.size());
}

int MedianNoticeSize(const std::vector<ParticipantRow>& rows, const std::string& start, const std::string& end) {
    std::map<std::string, std::set<std::string>> by_notice;
    for (const auto& row : rows) {
        const auto day = DayOf(row.open_date);
        if (!InDayRange(day, start, end)) {
            continue;
        }
        const auto key = CompanyIdentity(row);
        if (key.empty()) {
            continue;
        }
        by_notice[NoticeOf(row)].insert(key);
    }
    std::vector<int> sizes;
    sizes.reserve(by_notice.size());
    for (const auto& pair : by_notice) {
        sizes.push_back(static_cast<int>(pair.second.size()));
    }
    if (sizes.empty()) {
        return 0;
    }
    std::sort(sizes.begin(), sizes.end());
    return sizes[sizes.size() / 2];
}

}  // namespace

bool BandCacheKeyEqual(const BandCacheKey& a, const BandCacheKey& b) {
    return a.notice_id == b.notice_id && a.open_at == b.open_at && a.check_set == b.check_set
           && a.region == b.region && a.kind == b.kind && a.data_max_open == b.data_max_open;
}

BandCacheKey MakeBandCacheKey(const BandInput& in) {
    BandCacheKey key;
    key.notice_id = in.notice_id;
    key.open_at = DayOf(in.open_at);
    key.check_set = JoinChecks(in.checked_companies);
    key.region = in.region;
    key.kind = in.kind;
    key.data_max_open = MaxOpen(in.year_rows);
    return key;
}

BandResult ComputeDonggukBand(const BandInput& in) {
    BandResult out;
    out.notice_id = in.notice_id;
    const std::string baseline = !DayOf(in.open_at).empty() ? DayOf(in.open_at) : DayOf(in.today);
    const std::string year_start = ShiftMonths(baseline, -kCollectLookbackMonths);
    const std::string six_start = ShiftMonths(baseline, -6);
    if (baseline.empty() || year_start.empty()) {
        out.status = BandStatus::SampleShort;
        out.status_line = "표본부족";
        return out;
    }

    std::map<std::string, std::set<std::string>> watch_notices;
    std::map<std::string, std::vector<double>> by_company;
    std::vector<double> dongguk;
    std::vector<double> dongguk_six;
    double x_prev = 0;
    std::string prev_day;
    bool have_prev = false;
    for (const auto& row : in.year_rows) {
        if (!row.adjust_rate || !InRate(*row.adjust_rate)) {
            continue;
        }
        const auto day = DayOf(row.open_date);
        if (!InDayRange(day, year_start, baseline)) {
            continue;
        }
        const auto key = CompanyIdentity(row);
        if (key.empty()) {
            continue;
        }
        by_company[key].push_back(*row.adjust_rate);
        if (!IsDongguk(row)) {
            if (*row.adjust_rate + 1e-12 >= kWatchLo && *row.adjust_rate < kWatchHomeHi) {
                watch_notices[key].insert(NoticeOf(row));
            }
            continue;
        }
        dongguk.push_back(*row.adjust_rate);
        if (InDayRange(day, six_start, baseline)) {
            dongguk_six.push_back(*row.adjust_rate);
        }
        if (NoticeOf(row) == in.notice_id) {
            continue;
        }
        if (day < baseline && (prev_day.empty() || day > prev_day || (day == prev_day && *row.adjust_rate > x_prev))) {
            prev_day = day;
            x_prev = *row.adjust_rate;
            have_prev = true;
        }
    }

    out.has_watch = true;
    out.watch_lo = kWatchLo;
    out.home_lo = kWatchHomeLo;
    out.home_hi = kWatchHomeHi;
    for (const auto& [key, notices] : watch_notices) {
        WatchFirm firm;
        firm.company = key;
        firm.notice_n = static_cast<int>(notices.size());
        firm.regular = firm.notice_n >= kWatchRegularMin;
        out.watch_firms.push_back(firm);
    }
    {
        double home_fixed_min = 1e9;
        double home_any_min = 1e9;
        bool any_fixed = false;
        bool any_home = false;
        for (const auto& row : in.year_rows) {
            if (NoticeOf(row) != in.notice_id || !row.adjust_rate || IsDongguk(row)) {
                continue;
            }
            const double r = *row.adjust_rate;
            if (r + 1e-12 < kWatchHomeLo || r >= kWatchHomeHi) {
                continue;
            }
            any_home = true;
            home_any_min = (std::min)(home_any_min, r);
            bool regular = false;
            const auto key = CompanyIdentity(row);
            for (const auto& firm : out.watch_firms) {
                if (firm.company == key) {
                    regular = firm.regular;
                    break;
                }
            }
            if (regular) {
                any_fixed = true;
                home_fixed_min = (std::min)(home_fixed_min, r);
            }
        }
        double sit = kWatchHomeLo;
        if (any_fixed) {
            sit = home_fixed_min - kWatchStep;
        } else if (any_home) {
            sit = home_any_min - kWatchStep;
        }
        if (sit < kWatchLo) {
            sit = kWatchLo;
        }
        if (sit > kWatchHomeHi - kWatchStep) {
            sit = kWatchHomeHi - kWatchStep;
        }
        out.has_sit = true;
        out.sit_rate = sit;
    }

    out.n_estimate = UniqueCompaniesOnNotice(in.year_rows, in.notice_id);
    if (out.n_estimate <= 0) {
        out.n_estimate = MedianNoticeSize(in.year_rows, year_start, baseline);
    }

    if (static_cast<int>(dongguk.size()) < kMinPoints) {
        out.status = BandStatus::SampleShort;
        out.status_line = "표본부족";
        return out;
    }

    const auto self = MakeStats(dongguk);
    out.has_dongguk_stats = true;
    out.dongguk_q1 = self.q1;
    out.dongguk_q3 = self.q3;
    out.dongguk_iqr = self.iqr;
    out.dongguk_center = self.median;
    out.self_house = self.iqr < kSelfHouseIqr;

    if (!dongguk_six.empty()) {
        auto six = dongguk_six;
        std::sort(six.begin(), six.end());
        out.has_six_month_median = true;
        out.six_month_median = Quantile(six, 0.50);
    }

    std::set<std::string> checked;
    for (const auto& name : in.checked_companies) {
        const auto key = CanonicalizeCompany(name);
        if (!key.empty() && key.find("동국문화재연구원") == std::string::npos) {
            checked.insert(key);
        }
    }

    for (const auto& key : checked) {
        const auto found = by_company.find(key);
        if (found == by_company.end()) {
            continue;
        }
        if (static_cast<int>(found->second.size()) < kMinPoints) {
            continue;
        }
        const auto stats = MakeStats(found->second);
        if (stats.iqr >= kRivalHouseIqr) {
            continue;
        }
        BandHouse house;
        house.company = key;
        house.q1 = stats.q1;
        house.q3 = stats.q3;
        out.competitor_houses.push_back(house);
    }
    std::sort(out.competitor_houses.begin(), out.competitor_houses.end(),
              [](const BandHouse& a, const BandHouse& b) { return a.q1 < b.q1; });
    out.house_count = static_cast<int>(out.competitor_houses.size());

    const double t_lo = self.q1;
    const double t_hi = self.q3;
    const double pad_lo = t_lo - kClingPad;

    struct DongOnNotice {
        bool has = false;
        double x = 0;
        int rank = 99;
    };
    std::map<std::string, DongOnNotice> dong_on;
    for (const auto& row : in.year_rows) {
        if (!row.adjust_rate || !InRate(*row.adjust_rate) || !IsDongguk(row)) {
            continue;
        }
        const auto day = DayOf(row.open_date);
        if (!InDayRange(day, year_start, baseline)) {
            continue;
        }
        auto& slot = dong_on[NoticeOf(row)];
        const int rank = row.rank <= 0 ? 99 : row.rank;
        if (!slot.has || rank < slot.rank || (rank == slot.rank && *row.adjust_rate < slot.x)) {
            slot.has = true;
            slot.x = *row.adjust_rate;
            slot.rank = rank;
        }
    }

    for (const auto& row : in.year_rows) {
        if (!row.adjust_rate || !InRate(*row.adjust_rate) || IsDongguk(row)) {
            continue;
        }
        const auto day = DayOf(row.open_date);
        if (!InDayRange(day, year_start, baseline)) {
            continue;
        }
        const double x = *row.adjust_rate;
        if (x >= pad_lo && x < t_lo) {
            // threat points counted via houses below
        }
        if (x + 1e-12 < t_lo || x - 1e-12 > t_hi) {
            continue;
        }
        out.occupy_n += 1;
        if (day >= six_start) {
            out.occupy_recent_6 += 1;
        } else {
            out.occupy_prev_6 += 1;
        }
        const auto found = dong_on.find(NoticeOf(row));
        if (found != dong_on.end() && found->second.has && found->second.x < x && found->second.rank == 1) {
            out.prey_n += 1;
        }
    }

    out.avoid_ok = out.occupy_recent_6 < out.occupy_prev_6;
    out.others_thick = out.occupy_n > 0 && out.occupy_recent_6 >= out.occupy_prev_6;

    for (const auto& pair : by_company) {
        if (pair.first.find("동국문화재연구원") != std::string::npos) {
            continue;
        }
        if (static_cast<int>(pair.second.size()) < kMinPoints) {
            continue;
        }
        const auto stats = MakeStats(pair.second);
        if (stats.iqr >= kRivalHouseIqr) {
            continue;
        }
        if (!IntervalsOverlap(stats.q1, stats.q3, pad_lo, t_lo)) {
            continue;
        }
        BandHouse house;
        house.company = pair.first;
        house.q1 = stats.q1;
        house.q3 = stats.q3;
        house.cling = true;
        out.cling.push_back(house);
        if (!out.has_threat || stats.q1 < out.threat_q1) {
            out.has_threat = true;
            out.threat_q1 = stats.q1;
        }
    }
    out.cling_count = static_cast<int>(out.cling.size());
    out.threat_n = out.cling_count;

    bool house_on_t = false;
    for (const auto& pair : by_company) {
        if (pair.first.find("동국문화재연구원") != std::string::npos) {
            continue;
        }
        if (static_cast<int>(pair.second.size()) < kMinPoints) {
            continue;
        }
        const auto stats = MakeStats(pair.second);
        if (stats.iqr >= kRivalHouseIqr) {
            continue;
        }
        if (IntervalsOverlap(stats.q1, stats.q3 == stats.q1 ? stats.q1 + kTick : stats.q3, t_lo, t_hi)) {
            house_on_t = true;
            break;
        }
    }

    auto write_status = [&](bool overlap) {
        std::ostringstream line;
        line << "동국구간 " << Fmt2(t_lo) << "–" << Fmt2(t_hi) << " | ";
        if (overlap || !out.has_use) {
            line << "겹침";
        } else {
            line << "사용 " << Fmt2(out.use_lo) << "–" << Fmt2(out.use_hi);
        }
        line << " | T점유 " << out.occupy_n << " | 먹이 " << out.prey_n << " | 위협 " << out.threat_n << " | N≈"
             << out.n_estimate;
        out.status_line = line.str();
    };

    const bool invasion = out.others_thick || house_on_t;
    const bool hold_t = !invasion;

    double use_lo = t_lo;
    double use_hi = t_hi;
    if (!hold_t) {
        use_lo = t_lo;
        use_hi = (std::min)(t_hi, t_lo + kMinUseWidth);
        use_hi = (std::min)(use_hi, 100.0);
        if (out.has_threat) {
            use_hi = (std::min)(use_hi, out.threat_q1);
            use_lo = use_hi - kMinUseWidth;
        }
    }
    if (hold_t) {
        ExcludePrev(x_prev, have_prev, use_lo, use_hi);
    }

    auto in_closed = [](double x, double lo, double hi) {
        return x + 1e-12 >= lo && x - 1e-12 <= hi;
    };
    auto count_interval = [&](double lo, double hi, bool empty, int& bids, int& rank1, int& pct) {
        bids = 0;
        rank1 = 0;
        pct = -1;
        if (empty || hi < lo) {
            return;
        }
        for (const auto& row : in.year_rows) {
            if (!row.adjust_rate || !InRate(*row.adjust_rate)) {
                continue;
            }
            const auto day = DayOf(row.open_date);
            if (!InDayRange(day, year_start, baseline)) {
                continue;
            }
            const double x = *row.adjust_rate;
            if (!in_closed(x, lo, hi)) {
                continue;
            }
            bids += 1;
            if (row.rank == 1) {
                rank1 += 1;
            }
        }
        if (bids > 0) {
            pct = static_cast<int>(std::lround(100.0 * static_cast<double>(rank1) / static_cast<double>(bids)));
        }
    };
    count_interval(t_lo, t_hi, !out.has_dongguk_stats, out.t_bid_n, out.t_rank1_n, out.t_rank1_pct);
    count_interval(use_lo, use_hi, use_hi < use_lo, out.use_bid_n, out.use_rank1_n, out.use_rank1_pct);
    count_interval(pad_lo, t_lo - 1e-12, !out.has_dongguk_stats, out.threat_bid_n, out.threat_rank1_n,
                   out.threat_rank1_pct);
    out.win_bin_n.fill(0);
    out.win_bin_pct.fill(0);
    std::set<std::string> rank1_notices;
    for (const auto& row : in.year_rows) {
        if (!row.adjust_rate || !InRate(*row.adjust_rate) || row.rank != 1) {
            continue;
        }
        const auto day = DayOf(row.open_date);
        if (!InDayRange(day, year_start, baseline)) {
            continue;
        }
        if (!rank1_notices.insert(NoticeOf(row)).second) {
            continue;
        }
        const int bin = BandWinBinIndex(*row.adjust_rate);
        if (bin >= 0) {
            out.win_bin_n[static_cast<size_t>(bin)] += 1;
        }
    }
    out.window_rank1_n = static_cast<int>(rank1_notices.size());
    if (out.window_rank1_n > 0) {
        for (int i = 0; i < kBandWinBinCount; ++i) {
            if (out.win_bin_n[static_cast<size_t>(i)] <= 0) {
                continue;
            }
            out.win_bin_pct[static_cast<size_t>(i)] = static_cast<int>(
                std::lround(100.0 * static_cast<double>(out.win_bin_n[static_cast<size_t>(i)])
                            / static_cast<double>(out.window_rank1_n)));
        }
    }
    auto win_share = [](int part, int all) {
        if (all <= 0 || part < 0) {
            return -1;
        }
        return static_cast<int>(std::lround(100.0 * static_cast<double>(part) / static_cast<double>(all)));
    };
    out.t_win_pct = win_share(out.t_rank1_n, out.window_rank1_n);
    out.use_win_pct = win_share(out.use_rank1_n, out.window_rank1_n);

    if (use_lo > 100.0 + 1e-12) {
        out.status = BandStatus::OverlapFail;
        out.has_use = false;
        write_status(true);
        return out;
    }
    if (!hold_t && (use_hi - use_lo < kMinUseWidth - 1e-12 || use_hi <= use_lo)) {
        out.status = BandStatus::OverlapFail;
        out.has_use = false;
        write_status(true);
        return out;
    }

    out.has_use = true;
    out.use_lo = use_lo;
    out.use_hi = use_hi;
    out.status = BandStatus::Ok;
    if (BandUseIsRightOfDonggukQ3(out)) {
        out.status = BandStatus::OverlapFail;
        out.has_use = false;
        write_status(true);
        return out;
    }
    write_status(false);
    return out;
}

bool BandUseOverlapsSelfHouse(const BandResult& result) {
    if (!result.self_house || !result.has_use) {
        return false;
    }
    return result.use_lo < result.dongguk_q3 && result.use_hi > result.dongguk_q1;
}

bool BandUseIsRightOfDonggukQ3(const BandResult& result) {
    if (!result.has_use || !result.has_dongguk_stats) {
        return false;
    }
    return result.use_lo > result.dongguk_q3;
}

bool BandStatusHasForbiddenCopy(const std::string& text) {
    return text.find("추천") != std::string::npos || text.find("최적") != std::string::npos
           || text.find("월등") != std::string::npos || text.find("확률") != std::string::npos
           || text.find('%') != std::string::npos || text.find("사정률") != std::string::npos;
}

std::string FormatBandUseRange(const BandResult& result) {
    if (!result.has_use) {
        return {};
    }
    return "사용 " + Fmt2(result.use_lo) + "–" + Fmt2(result.use_hi);
}

std::string FormatBandTerritoryRange(const BandResult& result) {
    if (!result.has_dongguk_stats) {
        return {};
    }
    return "동국구간 " + Fmt2(result.dongguk_q1) + "–" + Fmt2(result.dongguk_q3);
}

std::string FormatBandRank1Pct(int pct) {
    if (pct < 0) {
        return {};
    }
    return "1위 " + std::to_string(pct) + "%";
}

std::string FormatBandWinShare(const BandResult& result) {
    if (result.t_win_pct < 0 || result.window_rank1_n <= 0) {
        return {};
    }
    return "1위 " + std::to_string(result.t_win_pct) + "%";
}

bool BandWatchIsRegular(const BandResult& result, const ParticipantRow& row) {
    const auto key = CompanyIdentity(row);
    if (key.empty()) {
        return false;
    }
    for (const auto& firm : result.watch_firms) {
        if (firm.company == key) {
            return firm.regular;
        }
    }
    return false;
}

std::string FormatWatchSit(const BandResult& result) {
    if (!result.has_sit) {
        return {};
    }
    return "넣을 곳 " + Fmt3(result.sit_rate);
}

std::string FormatBandWinBinPct(const BandResult& result, int bin) {
    if (bin < 0 || bin >= kBandWinBinCount) {
        return {};
    }
    const int pct = result.win_bin_pct[static_cast<size_t>(bin)];
    if (pct <= 0) {
        return {};
    }
    return std::to_string(pct) + "%";
}

BandResult DonggukBandCache::Get(const BandInput& in) {
    const auto key = MakeBandCacheKey(in);
    if (valid_ && BandCacheKeyEqual(key_, key)) {
        return result_;
    }
    result_ = ComputeDonggukBand(in);
    key_ = key;
    valid_ = true;
    return result_;
}

}  // namespace nara
