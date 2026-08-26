#include "nara/award_collect.h"

#include <algorithm>
#include <cctype>
#include <cmath>
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
        || notice.notice_name.find("매장유산") != std::string::npos;
    if (!domain) {
        return false;
    }
    const auto region = notice.region;
    if (region.find("전국") == std::string::npos && region.find("경북") == std::string::npos) {
        return false;
    }
    std::set<std::string> names;
    bool has_target = false;
    for (const auto& company : notice.companies) {
        if (company.name.empty()) {
            continue;
        }
        names.insert(company.name);
        if (company.name.find("문화재연구원") != std::string::npos) {
            has_target = true;
        }
    }
    return has_target && names.size() >= 6;
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
    return items;
}

std::optional<double> ParseLowerRate(const std::string& bid_notice_body) {
    for (const auto& item : ParseResponseItems(bid_notice_body)) {
        if (const auto value = JsonNumber(item, "sucsfbidLwltRate")) {
            return value;
        }
    }
    return std::nullopt;
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
        const auto bid_rate = JsonNumber(corp, "bidprcrt");
        if (company.bid_amount && notice.base_amount && notice.lower_rate) {
            company.adjust_rate = ComputeBaseAdjustRate(*company.bid_amount, *notice.base_amount, *notice.lower_rate);
        } else if (bid_rate && *bid_rate >= 99.0 && *bid_rate <= 101.0) {
            company.adjust_rate = bid_rate;
        }
        notice.companies.push_back(std::move(company));
    }
}

}  // namespace nara
