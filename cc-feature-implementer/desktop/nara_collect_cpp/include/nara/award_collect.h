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
    std::optional<double> bid_rate;
    std::string biz_no;
};

struct AwardNotice {
    std::string notice_number;
    std::string notice_name;
    std::string agency;
    std::string region;
    std::string open_date;
    std::optional<double> base_amount;
    std::optional<double> lower_rate;
    std::optional<double> estimate_price;
    std::optional<double> planned_price;
    std::vector<AwardCompany> companies;
};

struct UpcomingBidNotice {
    std::string source;
    std::string notice_number;
    std::string notice_name;
    std::string agency;
    std::string region;
    std::string open_date;
    std::string deadline_date;
    std::optional<double> base_amount;
    std::optional<double> estimate_price;
};

std::optional<double> ComputeBaseAdjustRate(double bid_amount, double base_amount, double lower_rate);
std::optional<double> FromBidRateVsLowerLimit(double bid_rate_percent, double lower_rate_percent);
bool PassesCollectRules(const AwardNotice& notice);
std::vector<std::string> CollectSearchKeywords();
std::string ResolveOrderingAgency(const std::string& dminstt_nm, const std::string& ntce_instt_nm);
struct CollectWindow {
    std::string start_month;
    std::string end_month;
};

inline constexpr int kCollectLookbackMonths = 12;

enum class CollectNeed { AlreadyCurrent, GapFromStore, FullLookback };

struct MissingCollectPlan {
    CollectNeed need = CollectNeed::FullLookback;
    std::string from_date;
    std::string to_date;
};

std::string FormatYmd(int year, int month, int day);
std::string YmdMinusMonths(int year, int month, int day, int months);
std::string AddDaysYmd(const std::string& ymd, int days);
inline std::string YmdToInqryBegin(const std::string& ymd) {
    if (ymd.size() < 10) {
        return {};
    }
    return ymd.substr(0, 4) + ymd.substr(5, 2) + ymd.substr(8, 2) + "0000";
}
inline std::string YmdToInqryEnd(const std::string& ymd) {
    if (ymd.size() < 10) {
        return {};
    }
    return ymd.substr(0, 4) + ymd.substr(5, 2) + ymd.substr(8, 2) + "2359";
}
struct DateGapSlice {
    std::string from_date;
    std::string to_date;
};

MissingCollectPlan PlanMissingCollect(
    const std::string& stored_max_open_day,
    const std::string& today,
    int lookback_months = kCollectLookbackMonths);
std::vector<DateGapSlice> SplitDateGapByMonth(const std::string& from_date, const std::string& to_date);
CollectWindow LookbackCollectMonthSpan(int year, int month, int lookback_months = kCollectLookbackMonths);
std::vector<std::string> CollectMonths(const std::string& start_month, const std::string& end_month);
std::vector<CollectWindow> CollectThreeMonthWindows(const std::string& start_month, const std::string& end_month);
std::vector<std::string> ParseJsonObjectArray(const std::string& json, const std::string& array_key);
std::vector<std::string> ParseResponseItems(const std::string& body);
std::optional<double> ParseLowerRate(const std::string& bid_notice_body);
std::optional<double> ParseBaseAmount(const std::string& bid_notice_body);
std::string CollectRegionToken(const std::string& raw);
std::string ResolveNoticeRegion(
    const std::string& api_token, const std::string& list_raw, bool api_ok, bool api_listed);
bool ParticipantRegionListed(const std::string& body);
std::string ParseParticipantRegion(const std::string& body);
void ApplyDetailCompanies(AwardNotice& notice, const std::string& detail_body);
std::string JsonString(const std::string& object, const std::string& key);
std::optional<double> JsonNumber(const std::string& object, const std::string& key);
std::string BuildOpengListQuery(
    const std::string& begin_dt,
    const std::string& end_dt,
    const std::string& keyword,
    int page);
int ExtractOpenApiTotalCount(const std::string& body);
int ComputeOpenApiPageCount(int total_count, int rows_per_page);
std::string SelectMatchingNoticeItem(const std::vector<std::string>& items, const std::string& notice_order);
std::optional<double> ParseLowerRateForOrder(const std::string& bid_notice_body, const std::string& notice_order);
std::optional<double> ParseBaseAmountForOrder(const std::string& bid_notice_body, const std::string& notice_order);
void InferNoticeCompanyAdjustRates(AwardNotice& notice);
std::vector<UpcomingBidNotice> ParseD2bUpcomingBids(const std::string& json);
std::optional<UpcomingBidNotice> ParseKaahNoticeDetailHtml(
    const std::string& notice_number,
    const std::string& html);

}  // namespace nara
