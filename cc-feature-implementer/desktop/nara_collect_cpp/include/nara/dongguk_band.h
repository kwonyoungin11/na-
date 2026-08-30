#pragma once

#include "nara/igunsul_convert.h"

#include <array>
#include <cmath>
#include <string>
#include <vector>

namespace nara {

constexpr double kWatchLo = 99.50;
constexpr double kWatchHomeLo = 99.60;
constexpr double kWatchHomeHi = 99.70;
constexpr double kWatchStep = 0.001;
constexpr int kWatchRegularMin = 3;

constexpr double kBandWinLo = 99.0;
constexpr double kBandWinHi = 101.0;
constexpr double kBandWinBin = 0.1;
constexpr int kBandWinBinCount = 20;

inline double BandWinBinCenterRate(int bin) {
    return kBandWinLo + (static_cast<double>(bin) + 0.5) * kBandWinBin;
}

inline int BandWinBinIndex(double rate) {
    if (rate < kBandWinLo || rate > kBandWinHi) {
        return -1;
    }
    int i = static_cast<int>(std::floor((rate - kBandWinLo) / kBandWinBin + 1e-12));
    if (i < 0) {
        return -1;
    }
    if (i >= kBandWinBinCount) {
        i = kBandWinBinCount - 1;
    }
    return i;
}

enum class BandStatus {
    Ok = 0,
    SampleShort = 1,
    OverlapFail = 2,
};

struct BandHouse {
    std::string company;
    double q1 = 0;
    double q3 = 0;
    bool cling = false;
};

struct WatchFirm {
    std::string company;
    int notice_n = 0;
    bool regular = false;
};

struct BandResult {
    std::string notice_id;
    BandStatus status = BandStatus::SampleShort;
    bool self_house = false;
    bool has_dongguk_stats = false;
    double dongguk_q1 = 0;
    double dongguk_q3 = 0;
    double dongguk_iqr = 0;
    double dongguk_center = 0;
    bool has_use = false;
    double use_lo = 0;
    double use_hi = 0;
    bool has_six_month_median = false;
    double six_month_median = 0;
    std::vector<BandHouse> competitor_houses;
    std::vector<BandHouse> cling;
    int n_estimate = 0;
    int cling_count = 0;
    int house_count = 0;
    int occupy_n = 0;
    int occupy_recent_6 = 0;
    int occupy_prev_6 = 0;
    bool avoid_ok = false;
    bool others_thick = false;
    int prey_n = 0;
    int threat_n = 0;
    bool has_threat = false;
    double threat_q1 = 0;
    int t_bid_n = 0;
    int t_rank1_n = 0;
    int t_rank1_pct = -1;
    int use_bid_n = 0;
    int use_rank1_n = 0;
    int use_rank1_pct = -1;
    int threat_bid_n = 0;
    int threat_rank1_n = 0;
    int threat_rank1_pct = -1;
    int window_rank1_n = 0;
    int t_win_pct = -1;
    int use_win_pct = -1;
    std::array<int, kBandWinBinCount> win_bin_n{};
    std::array<int, kBandWinBinCount> win_bin_pct{};
    bool has_watch = false;
    double watch_lo = 0;
    double home_lo = 0;
    double home_hi = 0;
    bool has_sit = false;
    double sit_rate = 0;
    std::vector<WatchFirm> watch_firms;
    std::string status_line;
};

struct BandCacheKey {
    std::string notice_id;
    std::string open_at;
    std::string check_set;
    std::string region;
    std::string kind;
    std::string data_max_open;
};

struct BandInput {
    std::string notice_id;
    std::string open_at;
    std::string region;
    std::string kind;
    std::string today;
    std::vector<std::string> checked_companies;
    std::vector<ParticipantRow> year_rows;
};

bool BandCacheKeyEqual(const BandCacheKey& a, const BandCacheKey& b);
BandCacheKey MakeBandCacheKey(const BandInput& in);
BandResult ComputeDonggukBand(const BandInput& in);
bool BandUseOverlapsSelfHouse(const BandResult& result);
bool BandUseIsRightOfDonggukQ3(const BandResult& result);
bool BandStatusHasForbiddenCopy(const std::string& text);
std::string FormatBandUseRange(const BandResult& result);
std::string FormatBandTerritoryRange(const BandResult& result);
std::string FormatBandRank1Pct(int pct);
std::string FormatBandWinShare(const BandResult& result);
std::string FormatBandWinBinPct(const BandResult& result, int bin);
bool BandWatchIsRegular(const BandResult& result, const ParticipantRow& row);
std::string FormatWatchSit(const BandResult& result);

inline bool BandChipIsPrey(const BandResult& result, double rate, bool is_dongguk) {
    if (is_dongguk || !result.has_dongguk_stats) {
        return false;
    }
    return rate + 1e-12 >= result.dongguk_q1 && rate - 1e-12 <= result.dongguk_q3;
}

inline bool BandChipIsThreat(const BandResult& result, double rate, bool is_dongguk) {
    if (is_dongguk || !result.has_dongguk_stats) {
        return false;
    }
    return rate + 1e-12 >= result.dongguk_q1 - 0.20 && rate < result.dongguk_q1;
}

inline bool BandUseIsWholeTerritory(const BandResult& result) {
    if (!result.has_use || !result.has_dongguk_stats) {
        return false;
    }
    return std::fabs(result.use_lo - result.dongguk_q1) <= 1e-9
           && std::fabs(result.use_hi - result.dongguk_q3) <= 1e-9;
}

class DonggukBandCache {
public:
    BandResult Get(const BandInput& in);

private:
    bool valid_ = false;
    BandCacheKey key_{};
    BandResult result_{};
};

}  // namespace nara
