#pragma once

#include <optional>
#include <string>
#include <vector>

namespace nara {

struct AwardCompany {
    std::string name;
    int rank = 0;
    std::optional<double> adjust_rate;
    std::optional<double> bid_amount;
};

struct AwardNotice {
    std::string notice_number;
    std::string notice_name;
    std::string agency;
    std::string region;
    std::string open_date;
    std::optional<double> base_amount;
    std::optional<double> lower_rate;
    std::vector<AwardCompany> companies;
};

std::optional<double> ComputeBaseAdjustRate(double bid_amount, double base_amount, double lower_rate);
std::optional<double> FromBidRateVsLowerLimit(double bid_rate_percent, double lower_rate_percent);
bool PassesCollectRules(const AwardNotice& notice);
struct CollectWindow {
    std::string start_month;
    std::string end_month;
};

inline constexpr int kCollectLookbackMonths = 12;
std::string FormatYmd(int year, int month, int day);
std::string YmdMinusMonths(int year, int month, int day, int months);
CollectWindow LookbackCollectMonthSpan(int year, int month, int lookback_months = kCollectLookbackMonths);
std::vector<std::string> CollectMonths(const std::string& start_month, const std::string& end_month);
std::vector<CollectWindow> CollectThreeMonthWindows(const std::string& start_month, const std::string& end_month);
std::vector<std::string> ParseJsonObjectArray(const std::string& json, const std::string& array_key);
std::vector<std::string> ParseResponseItems(const std::string& body);
std::optional<double> ParseLowerRate(const std::string& bid_notice_body);
std::optional<double> ParseBaseAmount(const std::string& bid_notice_body);
std::string CollectRegionToken(const std::string& raw);
std::string ParseParticipantRegion(const std::string& body);
void ApplyDetailCompanies(AwardNotice& notice, const std::string& detail_body);
std::string JsonString(const std::string& object, const std::string& key);
std::optional<double> JsonNumber(const std::string& object, const std::string& key);
std::string BuildOpengListQuery(
    const std::string& begin_dt,
    const std::string& end_dt,
    const std::string& keyword,
    int page);

}  // namespace nara
