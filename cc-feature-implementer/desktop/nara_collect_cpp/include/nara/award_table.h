#pragma once

#include "nara/igunsul_convert.h"

#include <string>
#include <vector>

namespace nara {

struct AwardNoticeRow {
    std::string open_date;
    std::string notice_number;
    std::string notice_name;
    std::string ordering_agency;
    std::string region;
    std::string rank_label;
    double dongguk_rate = 0;
    double award_rate = 0;
    bool has_award_rate = false;
};

std::string CanonicalizeRegion(const std::string& raw);
bool MatchesRegion(const std::string& source, const std::string& selected);

std::vector<ParticipantRow> BuildChartUniverse(
    const std::vector<ParticipantRow>& rows,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region,
    double min_rate,
    double max_rate);

std::vector<AwardNoticeRow> BuildAwardNoticeRows(
    const std::vector<ParticipantRow>& chart_rows,
    const std::string& start_date,
    const std::string& end_date);

}  // namespace nara
