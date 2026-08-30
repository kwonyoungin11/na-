#include "nara/dongguk_band.h"

#include <cmath>
#include <iostream>
#include <string>
#include <vector>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

nara::ParticipantRow P(const std::string& notice, const std::string& company, double rate,
                       const std::string& open, int rank = 2) {
    nara::ParticipantRow row;
    row.notice_number = notice;
    row.notice_no = notice;
    row.company_name = company;
    row.normalized_company_name = company;
    row.adjust_rate = rate;
    row.open_date = open;
    row.rank = rank;
    row.region = "전국";
    return row;
}

std::vector<nara::ParticipantRow> RepeatDongguk(const std::string& notice, double rate, const std::string& open,
                                                int n, int rank = 2) {
    std::vector<nara::ParticipantRow> rows;
    for (int i = 0; i < n; ++i) {
        rows.push_back(P(notice + "-" + std::to_string(i), "동국문화재연구원", rate, open, rank));
    }
    return rows;
}

bool Near(double a, double b, double eps = 0.0001) {
    return std::fabs(a - b) <= eps;
}

}  // namespace

int main() {
    using nara::BandInput;
    using nara::BandStatus;
    using nara::ComputeDonggukBand;
    using nara::DonggukBandCache;
    using nara::MakeBandCacheKey;

    BandInput tight;
    tight.notice_id = "A";
    tight.open_at = "2026-08-01";
    tight.region = "전국";
    tight.kind = "용역";
    tight.year_rows = RepeatDongguk("A", 99.60, "2026-06-01", 8, 3);
    tight.year_rows.push_back(P("A", "한울문화재연구원", 99.70, "2026-08-01", 1));
    const auto tight_band = ComputeDonggukBand(tight);
    if (tight_band.status == BandStatus::SampleShort) {
        return Fail("8 dongguk rank-3 points are enough; rank==1 must not be required");
    }
    if (!tight_band.self_house || !tight_band.has_dongguk_stats) {
        return Fail("IQR 0 is a narrow house and T still exists");
    }
    if (!Near(tight_band.dongguk_q1, 99.60) || !Near(tight_band.dongguk_q3, 99.60)) {
        return Fail("T is 1-year Dongguk Q1-Q3 even when the house is a needle");
    }
    if (tight_band.occupy_n != 0) {
        return Fail("한울 99.70 is right of T and is not T occupancy");
    }
    if (!tight_band.has_use || !Near(tight_band.use_lo, 99.60) || !Near(tight_band.use_hi, 99.60)) {
        return Fail("empty T is held as use=T, not opened left of the house");
    }
    if (tight_band.use_lo < tight_band.dongguk_q1 - 1e-9) {
        return Fail("use must not open left of T when T is empty");
    }
    if (nara::BandUseIsRightOfDonggukQ3(tight_band) || tight_band.use_lo > 100.0 + 1e-9) {
        return Fail("use must not sit right of T or open above 100");
    }
    if (tight_band.status_line.find("동국구간") == std::string::npos
        || tight_band.status_line.find("T점유") == std::string::npos
        || tight_band.status_line.find("먹이") == std::string::npos
        || tight_band.status_line.find("위협") == std::string::npos) {
        return Fail("status is 동국구간 | 사용 | T점유 | 먹이 | 위협");
    }
    if (tight_band.status_line.find("자사집") != std::string::npos
        || tight_band.status_line.find("띠") != std::string::npos) {
        return Fail("status must not say 자사집/띠");
    }
    if (nara::BandStatusHasForbiddenCopy(tight_band.status_line)
        || nara::BandStatusHasForbiddenCopy(nara::FormatBandUseRange(tight_band))
        || nara::BandStatusHasForbiddenCopy(nara::FormatBandTerritoryRange(tight_band))) {
        return Fail("copy must not contain 추천/최적/월등/확률/%/사정률");
    }

    BandInput invade = tight;
    for (int i = 0; i < 4; ++i) {
        invade.year_rows.push_back(P("INV" + std::to_string(i), "한울문화재연구원", 99.60, "2026-05-01", 2));
    }
    const auto invade_band = ComputeDonggukBand(invade);
    if (invade_band.occupy_n < 4) {
        std::cerr << "occupy " << invade_band.occupy_n << "\n";
        return Fail("other points with x in T count as T occupancy");
    }
    if (invade_band.has_use && nara::BandUseIsWholeTerritory(invade_band)) {
        return Fail("iqr<0.15 and invasion remaining must not paint use=T");
    }
    if (invade_band.has_use && invade_band.use_hi > invade_band.dongguk_q3 + 1e-9) {
        return Fail("defense use must not open right of T");
    }
    if (nara::BandChipIsPrey(invade_band, 99.60, false) == false) {
        return Fail("other point inside T is prey color");
    }
    if (nara::BandChipIsPrey(invade_band, 99.60, true)) {
        return Fail("Dongguk chips stay Dongguk, not prey");
    }

    BandInput short_sample = tight;
    short_sample.year_rows = RepeatDongguk("A", 99.60, "2026-06-01", 7, 2);
    const auto short_band = ComputeDonggukBand(short_sample);
    if (short_band.status != BandStatus::SampleShort || short_band.has_use || short_band.self_house) {
        return Fail("7 dongguk points are sample_short; do not fill with a mean");
    }

    BandInput stale = tight;
    stale.year_rows.push_back(P("OLD", "동국문화재연구원", 100.80, "2024-01-01", 1));
    const auto stale_band = ComputeDonggukBand(stale);
    if (!stale_band.self_house || std::fabs(stale_band.dongguk_q3 - 99.60) > 0.0001) {
        return Fail("points outside the loaded 1-year window must not enter T");
    }

    BandInput wide;
    wide.notice_id = "W";
    wide.open_at = "2026-08-01";
    wide.region = "전국";
    const double spread[] = {99.40, 99.47, 99.54, 99.61, 99.68, 99.75, 99.82, 99.90};
    for (int i = 0; i < 8; ++i) {
        wide.year_rows.push_back(
            P("W" + std::to_string(i), "동국문화재연구원", spread[i], "2026-05-01", 2));
    }
    for (int i = 0; i < 5; ++i) {
        wide.year_rows.push_back(P("WR" + std::to_string(i), "한울문화재연구원", 99.60, "2026-06-01", 3));
    }
    const auto wide_band = ComputeDonggukBand(wide);
    if (wide_band.self_house || !wide_band.has_dongguk_stats) {
        return Fail("IQR >= 0.15 is territory T, not a narrow house");
    }
    if (wide_band.occupy_n < 5 || wide_band.occupy_recent_6 < 5) {
        return Fail("recent others in T are occupancy, not a probability");
    }
    if (!wide_band.others_thick || !wide_band.has_use) {
        return Fail("occupancy not down is thick invasion; still emit a defense use");
    }
    if (nara::BandUseIsWholeTerritory(wide_band)) {
        return Fail("thick invasion uses the left 0.05 of T, not the whole T");
    }
    if (!Near(wide_band.use_lo, wide_band.dongguk_q1)
        || std::fabs((wide_band.use_hi - wide_band.use_lo) - 0.05) > 0.0001) {
        std::cerr << "use " << wide_band.use_lo << "–" << wide_band.use_hi << " T " << wide_band.dongguk_q1 << "–"
                  << wide_band.dongguk_q3 << "\n";
        return Fail("defense use is [T.lo, T.lo+0.05] inside T");
    }
    if (wide_band.use_hi > wide_band.dongguk_q3 + 1e-9) {
        return Fail("defense stays inside T");
    }

    BandInput avoid = wide;
    avoid.year_rows.clear();
    for (int i = 0; i < 8; ++i) {
        avoid.year_rows.push_back(
            P("W" + std::to_string(i), "동국문화재연구원", spread[i], "2026-05-01", 2));
    }
    for (int i = 0; i < 6; ++i) {
        avoid.year_rows.push_back(P("WP" + std::to_string(i), "한울문화재연구원", 99.60, "2025-11-01", 3));
    }
    avoid.year_rows.push_back(P("WN", "한울문화재연구원", 99.60, "2026-06-01", 3));
    const auto avoid_band = ComputeDonggukBand(avoid);
    if (!avoid_band.avoid_ok || avoid_band.occupy_recent_6 >= avoid_band.occupy_prev_6) {
        std::cerr << "recent " << avoid_band.occupy_recent_6 << " prev " << avoid_band.occupy_prev_6 << "\n";
        return Fail("recent 6m T occupancy down vs previous 6m is 기피 진행");
    }
    if (!avoid_band.has_use || !nara::BandUseIsWholeTerritory(avoid_band)) {
        return Fail("avoiding others: use = T");
    }

    BandInput narrow_avoid = tight;
    narrow_avoid.year_rows = RepeatDongguk("A", 99.60, "2026-06-01", 8, 3);
    for (int i = 0; i < 6; ++i) {
        narrow_avoid.year_rows.push_back(P("NA" + std::to_string(i), "한울문화재연구원", 99.60, "2025-11-01", 3));
    }
    narrow_avoid.year_rows.push_back(P("NAN", "한울문화재연구원", 99.60, "2026-06-01", 3));
    const auto narrow_avoid_band = ComputeDonggukBand(narrow_avoid);
    if (!narrow_avoid_band.self_house || !narrow_avoid_band.avoid_ok) {
        return Fail("narrow T with occupancy down is still a house, and 기피 진행");
    }
    if (!narrow_avoid_band.has_use || !nara::BandUseIsWholeTerritory(narrow_avoid_band)) {
        return Fail("occupancy down and no house-on-T: use = T even if leftover 1y points remain");
    }

    BandInput house_hold = avoid;
    for (int i = 0; i < 8; ++i) {
        house_hold.year_rows.push_back(P("HH" + std::to_string(i), "한울문화재연구원", 99.60, "2025-11-01", 2));
    }
    const auto house_hold_band = ComputeDonggukBand(house_hold);
    if (!house_hold_band.avoid_ok) {
        return Fail("house-on-T fixture still has occupancy down");
    }
    if (house_hold_band.has_use && nara::BandUseIsWholeTerritory(house_hold_band)) {
        return Fail("house overlapping T is leftover invasion; do not widen use to all of T");
    }

    BandInput prey = avoid;
    prey.year_rows.push_back(P("PR", "동국문화재연구원", 99.50, "2026-03-01", 1));
    prey.year_rows.push_back(P("PR", "한울문화재연구원", 99.62, "2026-03-01", 2));
    const auto prey_band = ComputeDonggukBand(prey);
    if (prey_band.prey_n < 1) {
        std::cerr << "prey " << prey_band.prey_n << "\n";
        return Fail("Dongguk 1st and left of an other point in T is prey");
    }

    BandInput threat = wide;
    threat.checked_companies = {"가람문화재연구원"};
    for (int i = 0; i < 8; ++i) {
        threat.year_rows.push_back(P("TH" + std::to_string(i), "가람문화재연구원", 99.35, "2026-04-01", 2));
    }
    for (int i = 0; i < 8; ++i) {
        threat.year_rows.push_back(P("TH2" + std::to_string(i), "화서문화재연구원", 99.42, "2026-04-01", 2));
    }
    const auto threat_band = ComputeDonggukBand(threat);
    if (threat_band.threat_n < 2) {
        return Fail("house in [T.lo-0.20, T.lo) is a threat");
    }
    if (!Near(threat_band.threat_q1, 99.35)) {
        std::cerr << "threat_q1 " << threat_band.threat_q1 << "\n";
        return Fail("threat_q1 is the leftmost pad-house Q1");
    }
    if (!nara::BandChipIsThreat(threat_band, 99.35, false)) {
        return Fail("point in the 0.20 pad left of T is threat color");
    }
    if (threat_band.has_use && threat_band.use_hi > threat_band.threat_q1 + 1e-9) {
        return Fail("threat blocking T left moves use to the left of that house Q1");
    }

    BandInput clip = wide;
    clip.checked_companies = {"한울문화재연구원"};
    for (int i = 0; i < 8; ++i) {
        clip.year_rows.push_back(P("H" + std::to_string(i), "한울문화재연구원", 99.60, "2026-04-01", 2));
    }
    const auto clip_band = ComputeDonggukBand(clip);
    if (clip_band.competitor_houses.empty()) {
        return Fail("checked rival with 8 tight points is a 집");
    }
    bool invasion_house = false;
    for (const auto& house : clip_band.competitor_houses) {
        if (house.q1 < clip_band.dongguk_q3 && house.q3 > clip_band.dongguk_q1) {
            invasion_house = true;
        }
    }
    if (!invasion_house) {
        return Fail("house overlapping T is still-invading, not avoided");
    }

    BandInput prev = avoid;
    prev.year_rows.push_back(P("PREV", "동국문화재연구원", 99.61, "2026-07-01", 2));
    const auto prev_band = ComputeDonggukBand(prev);
    if (prev_band.has_use && prev_band.use_lo <= 99.61 && prev_band.use_hi > 99.61) {
        return Fail("previous dongguk four-digit rate must be excluded from use=T");
    }

    if (nara::BandStatusHasForbiddenCopy("확률 월등") == false
        || nara::BandStatusHasForbiddenCopy("사정률 99.12") == false
        || nara::BandStatusHasForbiddenCopy("최적 99.40") == false) {
        return Fail("월등/사정률/최적 are forbidden copy");
    }

    DonggukBandCache cache;
    BandInput other = tight;
    other.notice_id = "B";
    other.open_at = "2026-08-20";
    other.year_rows = RepeatDongguk("B", 99.80, "2026-07-01", 8, 2);
    const auto from_a = cache.Get(tight);
    const auto from_b = cache.Get(other);
    if (from_b.notice_id == from_a.notice_id) {
        return Fail("notice B must not reuse notice A BandResult");
    }
    if (std::fabs(from_b.dongguk_q1 - 99.80) > 0.001) {
        return Fail("new notice recomputes 1-year T from scratch");
    }
    const auto key_a = MakeBandCacheKey(tight);
    const auto key_b = MakeBandCacheKey(other);
    if (nara::BandCacheKeyEqual(key_a, key_b)) {
        return Fail("cache key differs by notice_id and open_at");
    }

    const auto t_label = nara::FormatBandTerritoryRange(tight_band);
    if (t_label != "동국구간 99.60–99.60") {
        std::cerr << "T [" << t_label << "]\n";
        return Fail("territory legend is 동국구간 lo–hi");
    }
    const auto use_label = nara::FormatBandUseRange(tight_band);
    if (use_label != "사용 99.60–99.60") {
        std::cerr << "use [" << use_label << "]\n";
        return Fail("use legend is 사용 lo–hi, not 넣을 구간/추천");
    }
    nara::BandResult empty_use;
    if (!nara::FormatBandUseRange(empty_use).empty() || !nara::FormatBandTerritoryRange(empty_use).empty()) {
        return Fail("no T/use means no range string");
    }

    BandInput rank1_pct;
    rank1_pct.notice_id = "R1";
    rank1_pct.open_at = "2026-08-01";
    rank1_pct.region = "전국";
    rank1_pct.year_rows = RepeatDongguk("R1", 99.60, "2026-06-01", 8, 2);
    rank1_pct.year_rows.push_back(P("R1W", "한울문화재연구원", 99.60, "2026-05-01", 1));
    for (int i = 0; i < 3; ++i) {
        rank1_pct.year_rows.push_back(P("R1L" + std::to_string(i), "한울문화재연구원", 99.60, "2026-04-01", 2));
    }
    const auto rank1_band = ComputeDonggukBand(rank1_pct);
    if (rank1_band.t_bid_n != 12 || rank1_band.t_rank1_n != 1 || rank1_band.t_rank1_pct != 8) {
        std::cerr << "T bids " << rank1_band.t_bid_n << " rank1 " << rank1_band.t_rank1_n << " pct "
                  << rank1_band.t_rank1_pct << "\n";
        return Fail("동국구간 1위 % is rank1 bids / all bids in T");
    }
    if (nara::FormatBandRank1Pct(rank1_band.t_rank1_pct) != "1위 8%") {
        return Fail("interval label is 1위 n%");
    }
    rank1_pct.year_rows.push_back(P("R1OUT", "가람문화재연구원", 100.40, "2026-05-01", 1));
    const auto share_band = ComputeDonggukBand(rank1_pct);
    if (nara::BandWinBinIndex(99.60) != 6 || nara::BandWinBinIndex(100.40) != 14) {
        return Fail("0.1 bins: 99.60 is 99.6, 100.40 is 100.4");
    }
    if (share_band.window_rank1_n != 2 || share_band.win_bin_pct[6] != 50 || share_band.win_bin_pct[14] != 50) {
        std::cerr << "window " << share_band.window_rank1_n << " bin6 " << share_band.win_bin_pct[6] << " bin14 "
                  << share_band.win_bin_pct[14] << "\n";
        return Fail("1위 분포 is rank1 notices in each 0.1 bin / all 99-101 rank1");
    }
    if (nara::FormatBandWinBinPct(share_band, 6) != "50%") {
        std::cerr << "bin6 [" << nara::FormatBandWinBinPct(share_band, 6) << "]\n";
        return Fail("0.1-bin center prints 50%, not 99.60~99.70 50%");
    }
    if (std::fabs(nara::BandWinBinCenterRate(1) - 99.15) > 1e-9
        || std::fabs(nara::BandWinBinCenterRate(6) - 99.65) > 1e-9) {
        return Fail("99.10~99.20 center is 99.15; 99.60~99.70 center is 99.65");
    }
    BandInput rank1_b = rank1_pct;
    rank1_b.notice_id = "R2";
    rank1_b.open_at = "2026-08-20";
    rank1_b.year_rows = RepeatDongguk("R2", 99.80, "2026-07-01", 8, 1);
    const auto rank1_b_band = ComputeDonggukBand(rank1_b);
    if (rank1_b_band.window_rank1_n != 8 || rank1_b_band.win_bin_pct[8] != 100) {
        std::cerr << "B window " << rank1_b_band.window_rank1_n << " bin8 " << rank1_b_band.win_bin_pct[8] << "\n";
        return Fail("all 1sts at 99.80 fill the 99.8 bin at 100%");
    }
    rank1_b.year_rows.push_back(P("R2NEW", "한울문화재연구원", 99.11, "2026-07-15", 1));
    const auto plus_band = ComputeDonggukBand(rank1_b);
    if (plus_band.window_rank1_n != 9 || plus_band.win_bin_pct[1] != 11 || plus_band.win_bin_pct[8] != 89) {
        std::cerr << "plus window " << plus_band.window_rank1_n << " bin1 " << plus_band.win_bin_pct[1] << " bin8 "
                  << plus_band.win_bin_pct[8] << "\n";
        return Fail("one more 1st-place notice must change every 0.1 bin %");
    }
    if (nara::FormatBandWinBinPct(plus_band, 8) == nara::FormatBandWinBinPct(rank1_b_band, 8)) {
        return Fail("99.8 bin % changes when a new 1st place is added");
    }
    if (nara::FormatBandWinBinPct(plus_band, 2) != "") {
        return Fail("empty 0.1 bin has no % label");
    }

    if (std::fabs(nara::kWatchLo - 99.50) > 1e-9 || std::fabs(nara::kWatchHomeLo - 99.60) > 1e-9
        || std::fabs(nara::kWatchHomeHi - 99.70) > 1e-9 || std::fabs(nara::kWatchStep - 0.001) > 1e-9
        || nara::kWatchRegularMin != 3) {
        return Fail("watch is 99.50-99.70 home 99.60-99.70 step 0.001 regular 3");
    }

    BandInput watch;
    watch.notice_id = "NOW";
    watch.open_at = "2026-08-01";
    watch.region = "전국";
    watch.kind = "용역";
    watch.year_rows = RepeatDongguk("D", 99.62, "2026-07-01", 8);
    watch.year_rows.push_back(P("F1", "미래문화재연구원", 99.62, "2026-05-01"));
    watch.year_rows.push_back(P("F2", "미래문화재연구원", 99.63, "2026-06-01"));
    watch.year_rows.push_back(P("F3", "미래문화재연구원", 99.61, "2026-07-01"));
    watch.year_rows.push_back(P("NOW", "미래문화재연구원", 99.62, "2026-08-01"));
    watch.year_rows.push_back(P("NOW", "나그네문화재연구원", 99.55, "2026-08-01"));
    watch.year_rows.push_back(P("OLD", "옛집문화재연구원", 99.62, "2025-06-01"));
    watch.year_rows.push_back(P("T1", "두번문화재연구원", 99.64, "2026-04-01"));
    watch.year_rows.push_back(P("T2", "두번문화재연구원", 99.64, "2026-05-01"));
    const auto watch_band = ComputeDonggukBand(watch);
    if (!watch_band.has_watch || !Near(watch_band.watch_lo, 99.50) || !Near(watch_band.home_lo, 99.60)
        || !Near(watch_band.home_hi, 99.70)) {
        return Fail("watch box is 99.50-99.70 with home 99.60-99.70");
    }
    nara::ParticipantRow mirae;
    mirae.company_name = "미래문화재연구원";
    nara::ParticipantRow once;
    once.company_name = "나그네문화재연구원";
    nara::ParticipantRow twice;
    twice.company_name = "두번문화재연구원";
    nara::ParticipantRow old_firm;
    old_firm.company_name = "옛집문화재연구원";
    if (!nara::BandWatchIsRegular(watch_band, mirae) || nara::BandWatchIsRegular(watch_band, once)
        || nara::BandWatchIsRegular(watch_band, twice) || nara::BandWatchIsRegular(watch_band, old_firm)) {
        return Fail("고정 is >=3 notices in 1y watch; 1-2 and 13-month-old are 한번");
    }
    if (!watch_band.has_sit || !Near(watch_band.sit_rate, 99.619)) {
        std::cerr << "sit " << watch_band.sit_rate << "\n";
        return Fail("sit is 0.001 left of the 고정 to cover in 99.60-99.70");
    }
    const auto sit_line = nara::FormatWatchSit(watch_band);
    if (sit_line != "넣을 곳 99.619" || nara::BandStatusHasForbiddenCopy(sit_line)) {
        std::cerr << "sit line [" << sit_line << "]\n";
        return Fail("sit line is 넣을 곳 99.619 to 3 decimals");
    }

    BandInput cover_once = watch;
    cover_once.notice_id = "ONCE";
    cover_once.year_rows.push_back(P("ONCE", "나그네문화재연구원", 99.650, "2026-08-01"));
    const auto once_band = ComputeDonggukBand(cover_once);
    if (!once_band.has_sit || !Near(once_band.sit_rate, 99.649)) {
        std::cerr << "once sit " << once_band.sit_rate << "\n";
        return Fail("한번이라도 홈에 있으면 0.001 왼쪽에서 가린다");
    }

    std::cerr << "ok dongguk band\n";
    return 0;
}
