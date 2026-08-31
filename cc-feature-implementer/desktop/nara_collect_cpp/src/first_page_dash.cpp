#include "nara/first_page_dash.h"

#include "nara/notice_rules.h"

#include <map>
#include <set>
#include <utility>

namespace nara {

std::vector<FirmStat> BuildFirmStats(const std::vector<ParticipantRow>& snapshot) {
    std::map<std::string, std::vector<ParticipantRow>> by_notice;
    for (const auto& row : snapshot) {
        const auto id = row.notice_number.empty() ? row.notice_no : row.notice_number;
        if (id.empty()) {
            continue;
        }
        by_notice[id].push_back(row);
    }
    std::map<std::string, FirmStat> stats;
    for (const auto& kv : by_notice) {
        const auto scatter = BuildScatterNotice(kv.second, kv.first, 99.0, 101.0);
        std::set<std::string> seen;
        for (const auto& chip : scatter.chips) {
            const auto key = CompanyIdentity(chip);
            if (key.empty() || !seen.insert(key).second) {
                continue;
            }
            auto& s = stats[key];
            if (s.name.empty()) {
                s.key = key;
                s.name = chip.company_name.empty() ? chip.normalized_company_name : chip.company_name;
                s.dongguk = IsDonggukCompany(chip);
            }
            s.notice_n += 1;
            if (IsMappedRank1(chip, scatter.chips)) {
                s.rank1_n += 1;
            }
        }
    }
    std::vector<FirmStat> out;
    out.reserve(stats.size());
    std::vector<std::string> names;
    names.reserve(stats.size());
    for (auto& kv : stats) {
        names.push_back(kv.second.name);
        out.push_back(std::move(kv.second));
    }
    const auto labels = BuildDisplayMap(names);
    for (auto& s : out) {
        s.label = DisplayLabelFor(s.name, labels);
        if (s.label.empty()) {
            s.label = BuildShortLabel(s.name);
        }
    }
    std::sort(out.begin(), out.end(), [](const FirmStat& a, const FirmStat& b) {
        if (a.dongguk != b.dongguk) {
            return a.dongguk;
        }
        if (a.notice_n != b.notice_n) {
            return a.notice_n > b.notice_n;
        }
        if (a.rank1_n != b.rank1_n) {
            return a.rank1_n > b.rank1_n;
        }
        return a.label < b.label;
    });
    return out;
}

}  // namespace nara
