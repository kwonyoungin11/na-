#include "nara/award_collect.h"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdio>
#include <ctime>
#include <set>
#include <sstream>

namespace nara {
namespace {

int MonthIndex(const std::string& month) {
    if (month.size() < 7) {
        return 0;
    }
    return std::stoi(month.substr(0, 4)) * 12 + std::stoi(month.substr(5, 2));
}

std::string TrimCopy(const std::string& value) {
    size_t begin = 0;
    while (begin < value.size() && std::isspace(static_cast<unsigned char>(value[begin]))) {
        ++begin;
    }
    size_t end = value.size();
    while (end > begin && std::isspace(static_cast<unsigned char>(value[end - 1]))) {
        --end;
    }
    return value.substr(begin, end - begin);
}

}  // namespace

std::optional<double> ComputeBaseAdjustRate(double bid_amount, double base_amount, double lower_rate) {
    if (base_amount == 0.0 || lower_rate == 0.0) {
        return std::nullopt;
    }
    return (bid_amount / (base_amount * (lower_rate / 100.0))) * 100.0;
}

std::optional<double> FromBidRateVsLowerLimit(double bid_rate_percent, double lower_rate_percent) {
    if (lower_rate_percent == 0.0) {
        return std::nullopt;
    }
    return std::round((bid_rate_percent / lower_rate_percent * 100.0) * 100000.0) / 100000.0;
}

bool PassesCollectRules(const AwardNotice& notice) {
    if (notice.notice_name.find("영향진단") != std::string::npos) {
        return false;
    }
    const bool domain =
        notice.notice_name.find("발굴") != std::string::npos
        || notice.notice_name.find("시굴") != std::string::npos
        || notice.notice_name.find("문화재") != std::string::npos
        || notice.notice_name.find("매장유산") != std::string::npos
        || notice.notice_name.find("지표조사") != std::string::npos
        || notice.notice_name.find("표본조사") != std::string::npos
        || notice.notice_name.find("문화유산") != std::string::npos
        || notice.notice_name.find("문화재조사") != std::string::npos
        || notice.notice_name.find("매장문화재") != std::string::npos;
    if (!domain) {
        return false;
    }
    std::set<std::string> names;
    bool has_target = false;
    for (const auto& company : notice.companies) {
        if (company.name.empty()) {
            continue;
        }
        names.insert(company.name);
        if (company.name.find("문화재연구원") != std::string::npos
            || company.name.find("문화유산연구원") != std::string::npos) {
            has_target = true;
        }
    }
    return has_target && names.size() >= 6;
}

std::vector<std::string> CollectSearchKeywords() {
    return {
        "발굴조사",
        "정밀발굴조사",
        "시굴조사",
        "지표조사",
        "표본조사",
        "문화재발굴조사",
        "매장유산",
        "문화재조사",
        "문화유산",
    };
}

std::string ResolveOrderingAgency(const std::string& dminstt_nm, const std::string& ntce_instt_nm) {
    const auto demand = TrimCopy(dminstt_nm);
    if (!demand.empty()) {
        return demand;
    }
    return TrimCopy(ntce_instt_nm);
}

std::vector<std::string> CollectMonths(const std::string& start_month, const std::string& end_month) {
    std::vector<std::string> months;
    int start = MonthIndex(start_month);
    int end = MonthIndex(end_month);
    if (start <= 0 || end <= 0 || start > end) {
        return months;
    }
    for (int i = start; i <= end; ++i) {
        const int year = (i - 1) / 12;
        const int month = (i - 1) % 12 + 1;
        std::ostringstream out;
        out << year << "-";
        if (month < 10) {
            out << "0";
        }
        out << month;
        months.push_back(out.str());
    }
    return months;
}

std::string FormatYmd(int year, int month, int day) {
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%04d-%02d-%02d", year, month, day);
    return buf;
}

std::string AddDaysYmd(const std::string& ymd, int days) {
    if (ymd.size() < 10) {
        return {};
    }
    std::tm t{};
    t.tm_year = std::stoi(ymd.substr(0, 4)) - 1900;
    t.tm_mon = std::stoi(ymd.substr(5, 2)) - 1;
    t.tm_mday = std::stoi(ymd.substr(8, 2)) + days;
    t.tm_isdst = -1;
    if (std::mktime(&t) == static_cast<std::time_t>(-1)) {
        return {};
    }
    return FormatYmd(t.tm_year + 1900, t.tm_mon + 1, t.tm_mday);
}

MissingCollectPlan PlanMissingCollect(
    const std::string& stored_max_open_day,
    const std::string& today,
    int lookback_months) {
    MissingCollectPlan plan;
    const auto today_d = today.size() >= 10 ? today.substr(0, 10) : today;
    plan.to_date = today_d;
    const auto stored = stored_max_open_day.size() >= 10 ? stored_max_open_day.substr(0, 10) : stored_max_open_day;
    if (stored.size() == 10 && stored > today_d) {
        plan.need = CollectNeed::AlreadyCurrent;
        plan.from_date = stored;
        return plan;
    }
    if (stored.size() == 10) {
        plan.need = CollectNeed::GapFromStore;
        plan.from_date = stored;
        return plan;
    }
    plan.need = CollectNeed::FullLookback;
    if (today_d.size() >= 10) {
        plan.from_date = YmdMinusMonths(std::stoi(today_d.substr(0, 4)), std::stoi(today_d.substr(5, 2)),
                                       std::stoi(today_d.substr(8, 2)), lookback_months);
    }
    return plan;
}

std::string YmdMinusMonths(int year, int month, int day, int months) {
    month -= months;
    while (month <= 0) {
        month += 12;
        year -= 1;
    }
    return FormatYmd(year, month, day);
}

CollectWindow LookbackCollectMonthSpan(int year, int month, int lookback_months) {
    CollectWindow span;
    if (year < 1 || month < 1 || month > 12 || lookback_months < 0) {
        return span;
    }
    const int end_index = year * 12 + month;
    const int start_index = end_index - lookback_months;
    auto format = [](int index) {
        const int y = (index - 1) / 12;
        const int m = (index - 1) % 12 + 1;
        std::ostringstream out;
        out << y << "-";
        if (m < 10) {
            out << "0";
        }
        out << m;
        return out.str();
    };
    span.start_month = format(start_index);
    span.end_month = format(end_index);
    return span;
}

std::vector<CollectWindow> CollectThreeMonthWindows(const std::string& start_month, const std::string& end_month) {
    const auto months = CollectMonths(start_month, end_month);
    std::vector<CollectWindow> windows;
    for (size_t i = 0; i < months.size();) {
        CollectWindow window;
        window.start_month = months[i];
        const size_t last = (std::min)(i + 2, months.size() - 1);
        window.end_month = months[last];
        windows.push_back(window);
        i = last + 1;
    }
    return windows;
}

std::string JsonString(const std::string& object, const std::string& key) {
    const std::string needle = "\"" + key + "\"";
    auto pos = object.find(needle);
    if (pos == std::string::npos) {
        return {};
    }
    pos = object.find(':', pos + needle.size());
    if (pos == std::string::npos) {
        return {};
    }
    pos = object.find_first_not_of(" \t\r\n", pos + 1);
    if (pos == std::string::npos || object[pos] != '"') {
        return {};
    }
    ++pos;
    std::string out;
    for (; pos < object.size(); ++pos) {
        const char ch = object[pos];
        if (ch == '\\' && pos + 1 < object.size()) {
            out.push_back(object[++pos]);
            continue;
        }
        if (ch == '"') {
            break;
        }
        out.push_back(ch);
    }
    return out;
}

std::optional<double> JsonNumber(const std::string& object, const std::string& key) {
    const auto text = JsonString(object, key);
    if (!text.empty()) {
        try {
            return std::stod(text);
        } catch (...) {
        }
    }
    const std::string needle = "\"" + key + "\"";
    auto pos = object.find(needle);
    if (pos == std::string::npos) {
        return std::nullopt;
    }
    pos = object.find(':', pos + needle.size());
    if (pos == std::string::npos) {
        return std::nullopt;
    }
    pos = object.find_first_not_of(" \t\r\n\"", pos + 1);
    if (pos == std::string::npos) {
        return std::nullopt;
    }
    try {
        size_t consumed = 0;
        const double value = std::stod(object.substr(pos), &consumed);
        return value;
    } catch (...) {
        return std::nullopt;
    }
}

std::string ExtractJsonObject(const std::string& json, const std::string& key) {
    const std::string needle = "\"" + key + "\"";
    auto pos = json.find(needle);
    if (pos == std::string::npos) {
        return {};
    }
    pos = json.find(':', pos + needle.size());
    if (pos == std::string::npos) {
        return {};
    }
    pos = json.find_first_not_of(" \t\r\n", pos + 1);
    if (pos == std::string::npos || json[pos] != '{') {
        return {};
    }
    int depth = 0;
    for (size_t i = pos; i < json.size(); ++i) {
        if (json[i] == '{') {
            ++depth;
        } else if (json[i] == '}') {
            --depth;
            if (depth == 0) {
                return json.substr(pos, i - pos + 1);
            }
        }
    }
    return {};
}

std::vector<std::string> ParseJsonObjectArray(const std::string& json, const std::string& array_key) {
    const std::string needle = "\"" + array_key + "\"";
    auto pos = json.find(needle);
    if (pos == std::string::npos) {
        return {};
    }
    pos = json.find('[', pos);
    if (pos == std::string::npos) {
        return {};
    }
    std::vector<std::string> items;
    int depth = 0;
    size_t start = std::string::npos;
    for (size_t i = pos + 1; i < json.size(); ++i) {
        const char ch = json[i];
        if (ch == '{') {
            if (depth == 0) {
                start = i;
            }
            ++depth;
        } else if (ch == '}') {
            --depth;
            if (depth == 0 && start != std::string::npos) {
                items.push_back(json.substr(start, i - start + 1));
                start = std::string::npos;
            }
        } else if (ch == ']' && depth == 0) {
            break;
        }
    }
    return items;
}

std::vector<std::string> ParseResponseItems(const std::string& body) {
    auto items = ParseJsonObjectArray(body, "items");
    if (items.empty()) {
        items = ParseJsonObjectArray(body, "item");
    }
    if (items.empty()) {
        auto obj = ExtractJsonObject(body, "item");
        if (!obj.empty()) {
            items.push_back(std::move(obj));
        }
    }
    return items;
}

std::string BuildOpengListQuery(
    const std::string& begin_dt,
    const std::string& end_dt,
    const std::string& keyword,
    int page) {
    std::ostringstream path;
    path << "/1230000/as/ScsbidInfoService/getOpengResultListInfoServcPPSSrch"
         << "?inqryDiv=2&inqryBgnDt=" << begin_dt << "&inqryEndDt=" << end_dt
         << "&type=json&numOfRows=100&pageNo=" << page << "&bidNtceNm=" << keyword;
    return path.str();
}

std::optional<double> ParseLowerRate(const std::string& bid_notice_body) {
    for (const auto& item : ParseResponseItems(bid_notice_body)) {
        if (const auto value = JsonNumber(item, "sucsfbidLwltRate")) {
            return value;
        }
    }
    return std::nullopt;
}

std::string CollectRegionToken(const std::string& raw) {
    if (raw.find("경상북") != std::string::npos || raw.find("경북") != std::string::npos) {
        return "경북";
    }
    if (raw.find("전국") != std::string::npos) {
        return "전국";
    }
    return {};
}

bool ParticipantRegionListed(const std::string& body) {
    for (const auto& item : ParseResponseItems(body)) {
        if (!JsonString(item, "prtcptPsblRgnNm").empty()) {
            return true;
        }
    }
    return false;
}

std::string ResolveNoticeRegion(
    const std::string& api_token, const std::string& list_raw, bool api_ok, bool api_listed) {
    if (!api_token.empty()) {
        return api_token;
    }
    if (api_ok && api_listed) {
        return {};
    }
    return CollectRegionToken(list_raw);
}

std::string ParseParticipantRegion(const std::string& body) {
    std::string joined;
    for (const auto& item : ParseResponseItems(body)) {
        auto name = JsonString(item, "prtcptPsblRgnNm");
        if (name.empty()) {
            continue;
        }
        if (!joined.empty()) {
            joined += ",";
        }
        joined += name;
    }
    return CollectRegionToken(joined);
}

std::optional<double> ParseBaseAmount(const std::string& bid_notice_body) {
    for (const auto& item : ParseResponseItems(bid_notice_body)) {
        if (const auto value = JsonNumber(item, "bssAmt")) {
            return value;
        }
        if (const auto value = JsonNumber(item, "bssamt")) {
            return value;
        }
    }
    return std::nullopt;
}

void ApplyDetailCompanies(AwardNotice& notice, const std::string& detail_body) {
    for (const auto& corp : ParseResponseItems(detail_body)) {
        AwardCompany company;
        company.name = JsonString(corp, "prcbdrNm");
        if (company.name.empty()) {
            company.name = JsonString(corp, "bidprcCorpNm");
        }
        if (company.name.empty()) {
            continue;
        }
        company.rank = static_cast<int>(JsonNumber(corp, "opengRank").value_or(0));
        company.bid_amount = JsonNumber(corp, "bidprcAmt");
        company.bid_rate = JsonNumber(corp, "bidprcrt");
        company.biz_no = JsonString(corp, "prcbdrBizno");
        if (company.biz_no.empty()) {
            company.biz_no = JsonString(corp, "bidprcCorpBizrno");
        }
        if (company.bid_amount && notice.base_amount && notice.lower_rate) {
            company.adjust_rate = ComputeBaseAdjustRate(*company.bid_amount, *notice.base_amount, *notice.lower_rate);
        }
        notice.companies.push_back(std::move(company));
    }
    InferNoticeCompanyAdjustRates(notice);
}

int ExtractOpenApiTotalCount(const std::string& body) {
    if (const auto count = JsonNumber(body, "totalCount")) {
        return static_cast<int>(*count);
    }
    return 0;
}

int ComputeOpenApiPageCount(int total_count, int rows_per_page) {
    if (total_count <= 0 || rows_per_page <= 0) {
        return 1;
    }
    return (total_count + rows_per_page - 1) / rows_per_page;
}

std::string SelectMatchingNoticeItem(const std::vector<std::string>& items, const std::string& notice_order) {
    if (items.empty()) {
        return {};
    }
    if (!notice_order.empty()) {
        for (const auto& item : items) {
            if (JsonString(item, "bidNtceOrd") == notice_order) {
                return item;
            }
        }
    }
    return items.front();
}

std::optional<double> ParseLowerRateForOrder(const std::string& bid_notice_body, const std::string& notice_order) {
    const auto items = ParseResponseItems(bid_notice_body);
    const auto matched = SelectMatchingNoticeItem(items, notice_order);
    if (!matched.empty()) {
        if (const auto value = JsonNumber(matched, "sucsfbidLwltRate")) {
            return value;
        }
    }
    return ParseLowerRate(bid_notice_body);
}

std::optional<double> ParseBaseAmountForOrder(const std::string& bid_notice_body, const std::string& notice_order) {
    const auto items = ParseResponseItems(bid_notice_body);
    const auto matched = SelectMatchingNoticeItem(items, notice_order);
    if (!matched.empty()) {
        if (const auto value = JsonNumber(matched, "bssAmt")) {
            return value;
        }
        if (const auto value = JsonNumber(matched, "bssamt")) {
            return value;
        }
    }
    return ParseBaseAmount(bid_notice_body);
}

void InferNoticeCompanyAdjustRates(AwardNotice& notice) {
    std::optional<double> anchor_rate;
    std::optional<double> anchor_amount;
    std::optional<double> anchor_bid_rate;
    for (const auto& company : notice.companies) {
        if (!company.adjust_rate || *company.adjust_rate < 95.0 || *company.adjust_rate > 105.0) {
            continue;
        }
        if ((company.bid_amount && *company.bid_amount > 0.0) || (company.bid_rate && *company.bid_rate > 0.0)) {
            anchor_rate = company.adjust_rate;
            anchor_amount = company.bid_amount;
            anchor_bid_rate = company.bid_rate;
            if (company.rank == 1) {
                break;
            }
        }
    }
    if (!anchor_rate) {
        return;
    }
    for (auto& company : notice.companies) {
        if (company.adjust_rate) {
            continue;
        }
        if (company.bid_amount && *company.bid_amount > 0.0 && anchor_amount && *anchor_amount > 0.0) {
            company.adjust_rate =
                std::round(((*company.bid_amount / *anchor_amount) * *anchor_rate) * 100000.0) / 100000.0;
        } else if (company.bid_rate && *company.bid_rate > 0.0 && anchor_bid_rate && *anchor_bid_rate > 0.0) {
            company.adjust_rate =
                std::round(((*company.bid_rate / *anchor_bid_rate) * *anchor_rate) * 100000.0) / 100000.0;
        }
    }
}

namespace {

bool IsCulturalHeritageDomainTitle(const std::string& title) {
    if (title.find("영향진단") != std::string::npos) {
        return false;
    }
    return title.find("발굴") != std::string::npos
        || title.find("시굴") != std::string::npos
        || title.find("문화재") != std::string::npos
        || title.find("매장유산") != std::string::npos
        || title.find("지표조사") != std::string::npos
        || title.find("표본조사") != std::string::npos
        || title.find("문화유산") != std::string::npos
        || title.find("문화재조사") != std::string::npos
        || title.find("매장문화재") != std::string::npos;
}

std::string StripHtmlTagsAndTrim(const std::string& html) {
    std::string out;
    bool in_tag = false;
    for (char ch : html) {
        if (ch == '<') {
            in_tag = true;
            continue;
        }
        if (ch == '>') {
            in_tag = false;
            continue;
        }
        if (!in_tag) {
            out.push_back(ch);
        }
    }
    return TrimCopy(out);
}

std::optional<double> ParseKoreanMoney(const std::string& text) {
    std::string digits;
    bool started = false;
    for (unsigned char ch : text) {
        if (std::isdigit(ch) || ch == '.') {
            digits.push_back(static_cast<char>(ch));
            started = true;
        } else if (ch == ',' && started) {
            continue;
        } else if (started) {
            break;
        }
    }
    if (digits.empty()) {
        return std::nullopt;
    }
    try {
        return std::stod(digits);
    } catch (...) {
        return std::nullopt;
    }
}

std::string ExtractTableValueAfterHeader(const std::string& html, const std::string& header) {
    auto pos = html.find(header);
    if (pos == std::string::npos) {
        return {};
    }
    pos = html.find("<td", pos);
    if (pos == std::string::npos) {
        return {};
    }
    pos = html.find('>', pos);
    if (pos == std::string::npos) {
        return {};
    }
    const auto end = html.find("</td>", pos + 1);
    if (end == std::string::npos) {
        return {};
    }
    return StripHtmlTagsAndTrim(html.substr(pos + 1, end - (pos + 1)));
}

}  // namespace

std::vector<UpcomingBidNotice> ParseD2bUpcomingBids(const std::string& json) {
    std::vector<UpcomingBidNotice> out;
    auto items = ParseJsonObjectArray(json, "list");
    if (items.empty()) {
        items = ParseResponseItems(json);
    }
    for (const auto& item : items) {
        auto title = JsonString(item, "bidNtceNm");
        if (title.empty()) {
            title = JsonString(item, "pblancNm");
        }
        if (!IsCulturalHeritageDomainTitle(title)) {
            continue;
        }
        UpcomingBidNotice bid;
        bid.source = "국방전자조달";
        bid.notice_number = JsonString(item, "bidNtceNo");
        if (bid.notice_number.empty()) {
            bid.notice_number = JsonString(item, "pblancNo");
        }
        if (bid.notice_number.empty()) {
            continue;
        }
        bid.notice_name = title;
        bid.agency = ResolveOrderingAgency(JsonString(item, "dminsttNm"), JsonString(item, "ntceInsttNm"));
        bid.region = CollectRegionToken(JsonString(item, "rgnLmtBidLocplcJdgmBssNm"));
        if (bid.region.empty()) {
            bid.region = "전국";
        }
        bid.open_date = JsonString(item, "opengDt");
        bid.deadline_date = JsonString(item, "bidClseDt");
        bid.base_amount = JsonNumber(item, "bssamt");
        if (!bid.base_amount) {
            bid.base_amount = JsonNumber(item, "bssAmt");
        }
        bid.estimate_price = JsonNumber(item, "presmptPrce");
        out.push_back(std::move(bid));
    }
    return out;
}

std::optional<UpcomingBidNotice> ParseKaahNoticeDetailHtml(
    const std::string& notice_number,
    const std::string& html) {
    auto title = ExtractTableValueAfterHeader(html, "입찰명");
    if (title.empty()) {
        title = ExtractTableValueAfterHeader(html, "공고명");
    }
    if (title.empty() || !IsCulturalHeritageDomainTitle(title)) {
        return std::nullopt;
    }
    UpcomingBidNotice bid;
    bid.source = "한국문화유산협회";
    bid.notice_number = notice_number;
    bid.notice_name = title;
    bid.agency = ExtractTableValueAfterHeader(html, "발주처");
    if (bid.agency.empty()) {
        bid.agency = "한국문화유산협회";
    }
    const auto region_raw = ExtractTableValueAfterHeader(html, "지역제한");
    bid.region = CollectRegionToken(region_raw);
    if (bid.region.empty()) {
        bid.region = "전국";
    }
    bid.open_date = ExtractTableValueAfterHeader(html, "개찰일시");
    bid.deadline_date = ExtractTableValueAfterHeader(html, "제출기한");
    bid.base_amount = ParseKoreanMoney(ExtractTableValueAfterHeader(html, "기초금액"));
    bid.estimate_price = ParseKoreanMoney(ExtractTableValueAfterHeader(html, "추정가격"));
    return bid;
}

}  // namespace nara
