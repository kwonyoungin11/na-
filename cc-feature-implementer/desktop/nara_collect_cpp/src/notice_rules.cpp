#include "nara/notice_rules.h"

#include <algorithm>
#include <cctype>
#include <set>
#include <string>
#include <unordered_map>
#include <unordered_set>

namespace nara {
namespace {

std::string ToLowerAscii(std::string value) {
    for (char& ch : value) {
        if (static_cast<unsigned char>(ch) < 128) {
            ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
        }
    }
    return value;
}

std::string NoticeKey(const ParticipantRow& row) {
    const auto& value = row.notice_number.empty() ? row.notice_no : row.notice_number;
    auto start = value.find_first_not_of(" \t");
    auto end = value.find_last_not_of(" \t");
    if (start == std::string::npos) {
        return {};
    }
    return value.substr(start, end - start + 1);
}

std::string CompanyKey(const ParticipantRow& row) {
    return CompanyIdentity(row);
}

}  // namespace

bool IsExcludedNoticeName(const std::string& notice_name) {
    return !notice_name.empty() && notice_name.find("영향진단") != std::string::npos;
}

bool IsDonggukCompany(const ParticipantRow& row) {
    const auto canonical = CompanyIdentity(row);
    return canonical.find("동국문화재연구원") != std::string::npos;
}

std::vector<ParticipantRow> DropExcludedNoticeNames(const std::vector<ParticipantRow>& rows) {
    std::vector<ParticipantRow> kept;
    kept.reserve(rows.size());
    for (const auto& row : rows) {
        if (!IsExcludedNoticeName(row.notice_name)) {
            kept.push_back(row);
        }
    }
    return kept;
}

std::vector<ParticipantRow> DropNoticesWithTooFewCompanies(const std::vector<ParticipantRow>& rows) {
    std::unordered_map<std::string, std::set<std::string>> companies;
    for (const auto& row : rows) {
        const auto notice = NoticeKey(row);
        const auto company = CompanyKey(row);
        if (notice.empty() || company.empty()) {
            continue;
        }
        companies[ToLowerAscii(notice)].insert(ToLowerAscii(company));
    }
    std::unordered_set<std::string> allowed;
    for (const auto& [notice, names] : companies) {
        if (names.size() >= 6) {
            allowed.insert(notice);
        }
    }
    std::vector<ParticipantRow> kept;
    kept.reserve(rows.size());
    for (const auto& row : rows) {
        if (allowed.count(ToLowerAscii(NoticeKey(row))) != 0) {
            kept.push_back(row);
        }
    }
    return kept;
}

std::vector<ParticipantRow> KeepDonggukParticipatingNotices(const std::vector<ParticipantRow>& rows) {
    std::unordered_set<std::string> dongguk;
    for (const auto& row : rows) {
        if (IsDonggukCompany(row)) {
            const auto key = NoticeKey(row);
            if (!key.empty()) {
                dongguk.insert(ToLowerAscii(key));
            }
        }
    }
    std::vector<ParticipantRow> kept;
    kept.reserve(rows.size());
    for (const auto& row : rows) {
        if (dongguk.count(ToLowerAscii(NoticeKey(row))) != 0) {
            kept.push_back(row);
        }
    }
    return kept;
}

std::vector<ParticipantRow> ApplyPersistRules(const std::vector<ParticipantRow>& rows) {
    return DropNoticesWithTooFewCompanies(DropExcludedNoticeNames(rows));
}

std::vector<ParticipantRow> ApplyChartRules(const std::vector<ParticipantRow>& rows) {
    return KeepDonggukParticipatingNotices(DropExcludedNoticeNames(rows));
}

}  // namespace nara
