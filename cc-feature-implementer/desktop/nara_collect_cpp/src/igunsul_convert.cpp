#include "nara/igunsul_convert.h"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <iomanip>
#include <map>
#include <regex>
#include <set>
#include <sstream>

namespace nara {
namespace {

std::string Trim(std::string value) {
    auto not_space = [](unsigned char ch) { return !std::isspace(ch); };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), not_space));
    value.erase(std::find_if(value.rbegin(), value.rend(), not_space).base(), value.end());
    return value;
}

std::string ReplaceAll(std::string value, const std::string& from, const std::string& to) {
    size_t pos = 0;
    while ((pos = value.find(from, pos)) != std::string::npos) {
        value.replace(pos, from.size(), to);
        pos += to.size();
    }
    return value;
}

std::optional<double> ParseDouble(const std::string& raw) {
    auto text = ReplaceAll(Trim(raw), "%", "");
    if (text.empty()) {
        return std::nullopt;
    }
    try {
        return std::stod(text);
    } catch (...) {
        return std::nullopt;
    }
}

std::optional<long long> ParseLong(const std::string& raw) {
    auto text = ReplaceAll(ReplaceAll(Trim(raw), ",", ""), "원", "");
    if (text.empty()) {
        return std::nullopt;
    }
    try {
        return std::stoll(text);
    } catch (...) {
        return std::nullopt;
    }
}

std::string NormalizeDate(const std::string& raw) {
    auto text = ReplaceAll(Trim(raw), "/", "-");
    return text;
}

std::string JsonEscape(const std::string& raw) {
    std::ostringstream out;
    for (unsigned char ch : raw) {
        switch (ch) {
            case '"':
                out << "\\\"";
                break;
            case '\\':
                out << "\\\\";
                break;
            case '\n':
                out << "\\n";
                break;
            case '\r':
                out << "\\r";
                break;
            default:
                out << ch;
                break;
        }
    }
    return out.str();
}

std::string JsonOpt(const std::optional<double>& value) {
    if (!value) {
        return "null";
    }
    std::ostringstream out;
    out << std::setprecision(12) << *value;
    return out.str();
}

std::string JsonOpt(const std::optional<long long>& value) {
    if (!value) {
        return "null";
    }
    return std::to_string(*value);
}

int HeaderIndex(const std::vector<std::string>& header, const std::string& key) {
    for (int i = 0; i < static_cast<int>(header.size()); ++i) {
        if (header[i] == key || header[i].find(key) != std::string::npos) {
            return i;
        }
    }
    return -1;
}

std::string Cell(const std::vector<std::string>& row, int index) {
    if (index < 0 || index >= static_cast<int>(row.size())) {
        return {};
    }
    return row[index];
}

std::map<std::string, std::string> OverviewMap(const Sheet& sheet) {
    std::map<std::string, std::string> map;
    for (const auto& row : sheet.cells) {
        if (row.empty()) {
            continue;
        }
        const auto label = Trim(row[0]);
        if (label.empty()) {
            continue;
        }
        map[label] = row.size() > 1 ? Trim(row[1]) : "";
    }
    return map;
}

std::string MapGet(const std::map<std::string, std::string>& map, const std::vector<std::string>& keys) {
    for (const auto& key : keys) {
        auto exact = map.find(key);
        if (exact != map.end() && !exact->second.empty()) {
            return exact->second;
        }
        for (const auto& pair : map) {
            if (pair.first.find(key) != std::string::npos && !pair.second.empty()) {
                return pair.second;
            }
        }
    }
    return {};
}

}  // namespace

bool TryParseSignedParen(const std::string& raw, double& signed_percent, double& hundred_scale) {
    static const std::regex re(
        R"(([-+]?\d+(?:\.\d+)?)\s*%?\s*\(\s*([-+]?\d+(?:\.\d+)?)\s*%?\s*\))");
    std::smatch match;
    if (!std::regex_search(raw, match, re)) {
        return false;
    }
    signed_percent = std::stod(match[1].str());
    hundred_scale = std::stod(match[2].str());
    return true;
}

std::optional<double> ToHundredScaleAdjustRate(std::optional<double> raw) {
    if (!raw) {
        return std::nullopt;
    }
    const double value = *raw;
    if (std::fabs(value) <= 10.0) {
        return std::round((100.0 + value) * 100000.0) / 100000.0;
    }
    return value;
}

std::string CanonicalizeCompany(const std::string& name) {
    auto output = Trim(name);
    const std::vector<std::string> removals = {
        "(재)", "(재단)", "재단법인", "재단", "(주)", "주식회사", "(사)", "사단법인"};
    for (const auto& token : removals) {
        output = ReplaceAll(output, token, "");
    }
    output = ReplaceAll(output, "문화유산연구원", "문화재연구원");
    output = Trim(ReplaceAll(output, "  ", " "));
    return output;
}

std::string ExtractNoticeNo(const std::string& notice_number) {
    auto text = Trim(notice_number);
    const auto dash = text.rfind('-');
    if (dash == std::string::npos || dash == 0) {
        return text;
    }
    const auto suffix = text.substr(dash + 1);
    if (suffix.empty() || !std::all_of(suffix.begin(), suffix.end(), [](unsigned char ch) {
            return std::isdigit(ch);
        })) {
        return text;
    }
    if (!text.empty() && (text[0] == 'R' || text[0] == 'r')) {
        return text.substr(0, dash);
    }
    return suffix.size() <= 3 ? text.substr(0, dash) : text;
}

std::vector<ParticipantRow> ConvertPair(
    const std::filesystem::path& overview_path,
    const std::filesystem::path& participants_path) {
    const auto overview_sheet = ReadFirstSheet(overview_path);
    const auto part_sheet = ReadFirstSheet(participants_path);
    const auto overview = OverviewMap(overview_sheet);
    const auto notice_number = MapGet(overview, {"공고번호"});
    const auto notice_name = MapGet(overview, {"공고명", "공사명"});
    // C# CollectorNoticeRules.IsExcludedNoticeName: 영향진단 awards are not ingested.
    if (notice_name.find("영향진단") != std::string::npos) {
        return {};
    }
    const auto agency = MapGet(overview, {"발주처"});
    auto region = MapGet(overview, {"지역제한", "지역"});
    if (region.find("전국") != std::string::npos) {
        region = "전국";
    }
    const auto open_date = NormalizeDate(MapGet(overview, {"개찰일시"}));
    double signed_rate = 0;
    double paren = 0;
    const auto 사정률 = MapGet(overview, {"사정률"});
    if (!TryParseSignedParen(사정률, signed_rate, paren)) {
        if (const auto labeled = ParseDouble(MapGet(overview, {"기초대비사정율", "기초대비사정률"}))) {
            paren = *labeled;
        }
    }
    const auto base_amount = ParseLong(MapGet(overview, {"기초금액"}));
    const auto estimate_price = ParseLong(MapGet(overview, {"예정가격"}));
    const auto lower_limit = ParseLong(MapGet(overview, {"낙찰하한가"}));
    const auto lower_rate = ParseDouble(MapGet(overview, {"투찰률"}));

    if (part_sheet.cells.empty()) {
        return {};
    }
    const auto header = part_sheet.cells.front();
    const int rank_i = HeaderIndex(header, "순위");
    const int biz_i = HeaderIndex(header, "사업자등록번호");
    const int name_i = HeaderIndex(header, "업체명");
    const int amount_i = HeaderIndex(header, "투찰금액");
    const int vs_est_i = HeaderIndex(header, "예가대비투찰률");
    const int vs_base_i = HeaderIndex(header, "기초대비투찰률");
    const int adj_i = HeaderIndex(header, "기초대비사정률");
    const int bid_date_i = HeaderIndex(header, "투찰일시");

    std::vector<ParticipantRow> rows;
    for (size_t r = 1; r < part_sheet.cells.size(); ++r) {
        const auto& excel_row = part_sheet.cells[r];
        const auto company = Trim(Cell(excel_row, name_i));
        if (company.empty()) {
            continue;
        }
        ParticipantRow row;
        row.notice_number = notice_number;
        row.notice_no = ExtractNoticeNo(notice_number);
        row.notice_name = notice_name;
        row.ordering_agency = agency;
        row.region = region;
        row.rank = static_cast<int>(ParseDouble(Cell(excel_row, rank_i)).value_or(0));
        row.company_name = company;
        row.normalized_company_name = CanonicalizeCompany(company);
        row.open_date = open_date;
        row.bid_date = NormalizeDate(Cell(excel_row, bid_date_i));
        row.bid_amount = ParseLong(Cell(excel_row, amount_i));
        row.bid_rate_vs_estimate = ParseDouble(Cell(excel_row, vs_est_i));
        row.bid_rate_vs_base = ParseDouble(Cell(excel_row, vs_base_i));
        row.adjust_rate = ToHundredScaleAdjustRate(ParseDouble(Cell(excel_row, adj_i)));
        row.estimate_price = estimate_price;
        row.lower_limit_price = lower_limit;
        row.base_amount = base_amount;
        row.price_adjustment_rate = paren == 0 ? std::optional<double>{} : paren;
        row.lower_rate = lower_rate;
        row.biz_no = Trim(Cell(excel_row, biz_i));
        row.is_awarded = row.rank == 1;
        rows.push_back(std::move(row));
    }
    std::sort(rows.begin(), rows.end(), [](const ParticipantRow& a, const ParticipantRow& b) {
        return a.rank < b.rank;
    });
    std::set<std::string> companies;
    for (const auto& row : rows)
    {
        auto name = row.normalized_company_name.empty() ? row.company_name : row.normalized_company_name;
        if (!name.empty())
        {
            companies.insert(name);
        }
    }
    if (companies.size() <= 5)
    {
        return {};
    }
    return rows;
}

std::vector<ParticipantRow> ConvertDirectory(const std::filesystem::path& directory) {
    std::vector<ParticipantRow> rows;
    if (!std::filesystem::exists(directory)) {
        return rows;
    }
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        const auto name = entry.path().filename().string();
        const auto marker = std::string("_overview.xlsx");
        if (name.size() < marker.size() || name.compare(name.size() - marker.size(), marker.size(), marker) != 0) {
            continue;
        }
        auto participants = entry.path();
        auto filename = participants.filename().wstring();
        const auto pos = filename.rfind(L"_overview.xlsx");
        if (pos == std::wstring::npos) {
            continue;
        }
        filename.replace(pos, std::wstring(L"_overview.xlsx").size(), L"_participants.xlsx");
        participants = entry.path().parent_path() / filename;
        if (!std::filesystem::exists(participants)) {
            continue;
        }
        auto part = ConvertPair(entry.path(), participants);
        rows.insert(rows.end(), part.begin(), part.end());
    }
    std::sort(rows.begin(), rows.end(), [](const ParticipantRow& a, const ParticipantRow& b) {
        if (a.notice_number != b.notice_number) {
            return a.notice_number < b.notice_number;
        }
        return a.rank < b.rank;
    });
    return rows;
}

std::string ToJsonLine(const ParticipantRow& row) {
    std::ostringstream out;
    out << "{"
        << "\"notice_no\":\"" << JsonEscape(row.notice_no) << "\","
        << "\"notice_number\":\"" << JsonEscape(row.notice_number) << "\","
        << "\"notice_name\":\"" << JsonEscape(row.notice_name) << "\","
        << "\"ordering_agency\":\"" << JsonEscape(row.ordering_agency) << "\","
        << "\"region\":\"" << JsonEscape(row.region) << "\","
        << "\"rank\":" << row.rank << ","
        << "\"company_name\":\"" << JsonEscape(row.company_name) << "\","
        << "\"normalized_company_name\":\"" << JsonEscape(row.normalized_company_name) << "\","
        << "\"open_date\":\"" << JsonEscape(row.open_date) << "\","
        << "\"bid_date\":\"" << JsonEscape(row.bid_date) << "\","
        << "\"bid_amount\":" << JsonOpt(row.bid_amount) << ","
        << "\"bid_rate_vs_estimate\":" << JsonOpt(row.bid_rate_vs_estimate) << ","
        << "\"bid_rate_vs_base\":" << JsonOpt(row.bid_rate_vs_base) << ","
        << "\"adjust_rate\":" << JsonOpt(row.adjust_rate) << ","
        << "\"estimate_price\":" << JsonOpt(row.estimate_price) << ","
        << "\"lower_limit_price\":" << JsonOpt(row.lower_limit_price) << ","
        << "\"base_amount\":" << JsonOpt(row.base_amount) << ","
        << "\"price_adjustment_rate\":" << JsonOpt(row.price_adjustment_rate) << ","
        << "\"lower_rate\":" << JsonOpt(row.lower_rate) << ","
        << "\"biz_no\":\"" << JsonEscape(row.biz_no) << "\","
        << "\"is_awarded\":" << (row.is_awarded ? "true" : "false")
        << "}";
    return out.str();
}

}  // namespace nara
