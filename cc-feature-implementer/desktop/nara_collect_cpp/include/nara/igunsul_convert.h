#pragma once

#include "nara/xlsx_store.h"

#include <filesystem>
#include <map>
#include <optional>
#include <string>
#include <vector>

namespace nara {

struct ParticipantRow {
    std::string notice_no;
    std::string notice_number;
    std::string notice_name;
    std::string ordering_agency;
    std::string region;
    int rank = 0;
    std::string company_name;
    std::string normalized_company_name;
    std::string open_date;
    std::string bid_date;
    std::optional<long long> bid_amount;
    std::optional<double> bid_rate_vs_estimate;
    std::optional<double> bid_rate_vs_base;
    std::optional<double> adjust_rate;
    std::optional<long long> estimate_price;
    std::optional<long long> lower_limit_price;
    std::optional<long long> base_amount;
    std::optional<double> price_adjustment_rate;
    std::optional<double> lower_rate;
    std::string biz_no;
    bool is_awarded = false;
};

bool TryParseSignedParen(const std::string& raw, double& signed_percent, double& hundred_scale);
std::optional<double> ToHundredScaleAdjustRate(std::optional<double> raw);
std::string CanonicalizeCompany(const std::string& name);
std::string BuildShortLabel(const std::string& name);
std::string CompanyIdentity(const ParticipantRow& row);
std::map<std::string, std::string> BuildDisplayMap(const std::vector<std::string>& names);
std::string DisplayLabelFor(const std::string& name, const std::map<std::string, std::string>& labels);
std::string ExtractNoticeNo(const std::string& notice_number);

std::vector<ParticipantRow> ConvertPair(
    const std::filesystem::path& overview_path,
    const std::filesystem::path& participants_path);
std::vector<ParticipantRow> ConvertDirectory(const std::filesystem::path& directory);
std::string ToJsonLine(const ParticipantRow& row);

}  // namespace nara
