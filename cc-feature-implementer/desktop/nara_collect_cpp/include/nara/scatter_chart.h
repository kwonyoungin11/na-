#pragma once

#include "nara/igunsul_convert.h"

#include <string>
#include <vector>

namespace nara {

struct ScatterNotice {
    std::string notice_number;
    int chip_count = 0;
    int dongguk_rank = 0;
    int total_companies = 0;
    std::vector<ParticipantRow> chips;
};

ScatterNotice BuildScatterNotice(
    const std::vector<ParticipantRow>& rows,
    const std::string& notice_number,
    double min_rate,
    double max_rate);

std::vector<ParticipantRow> InPeriod(
    const std::vector<ParticipantRow>& rows,
    const std::string& start_date,
    const std::string& end_date);

double ToChartAdjustRate(double raw, double rank1_raw, double rank1_chart);

void PreferChartAdjustRates(
    std::vector<ParticipantRow>& live,
    const std::vector<ParticipantRow>& reference);

bool IsLowerLimitAwardRate(double rate);
bool LooksLikeOpenApiHundred(const ParticipantRow& row);
bool NoticeHasLowerLimitAwardCluster(const std::vector<ParticipantRow>& rows);
bool IsMappedRank1(const ParticipantRow& chip, const std::vector<ParticipantRow>& notice_chips);

std::string FormatDonggukRankLabel(
    const ScatterNotice& scatter,
    double dongguk_rate,
    double award_rate);

std::string FormatDonggukRankLabel(
    const ScatterNotice& scatter,
    double dongguk_rate);

}  // namespace nara
