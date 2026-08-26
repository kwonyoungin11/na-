#include "nara/notice_rules.h"

#include <iostream>
#include <string>
#include <vector>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

nara::ParticipantRow Row(
    const std::string& notice,
    int rank,
    const std::string& company,
    const std::string& notice_name = "정밀발굴조사 용역") {
    nara::ParticipantRow row;
    row.notice_number = notice;
    row.notice_no = notice;
    row.rank = rank;
    row.company_name = company;
    row.normalized_company_name = company;
    row.notice_name = notice_name;
    return row;
}

}  // namespace

int main() {
    {
        std::vector<nara::ParticipantRow> rows = {
            Row("A", 1, "동국문화재연구원"),
            Row("A", 2, "한울문화재연구원"),
            Row("B", 1, "한울문화재연구원"),
            Row("B", 2, "호서문화재연구원"),
        };
        const auto kept = nara::KeepDonggukParticipatingNotices(rows);
        if (kept.size() != 2) {
            return Fail("KeepDongguk must keep both co-bidders on notice A");
        }
        for (const auto& row : kept) {
            if (row.notice_number != "A") {
                return Fail("KeepDongguk must drop notice B");
            }
        }
    }

    {
        std::vector<nara::ParticipantRow> rows = {
            Row("G", 1, "동국문화재연구원", "금강 공주권역 배수영향구간 실시설계 국가유산영향진단 용역"),
            Row("D", 1, "동국문화재연구원", "창녕-고암 국도건설공사 문화재 정밀발굴조사용역"),
        };
        const auto kept = nara::DropExcludedNoticeNames(rows);
        if (kept.size() != 1 || kept[0].notice_number != "D") {
            return Fail("DropExcluded must drop 영향진단 and keep 발굴");
        }
    }

    if (nara::IsExcludedNoticeName("배수영향구간 정밀발굴조사용역")) {
        return Fail("영향구간 is not 영향진단");
    }
    if (!nara::IsExcludedNoticeName("국가유산영향진단 용역")) {
        return Fail("영향진단 must be excluded");
    }

    {
        std::vector<nara::ParticipantRow> rows;
        for (int i = 1; i <= 6; ++i) {
            rows.push_back(Row("DIG", i, "업체" + std::to_string(i), "창녕-고암 문화재 정밀발굴조사용역"));
        }
        for (int i = 1; i <= 6; ++i) {
            rows.push_back(Row("IMP", i, "진단" + std::to_string(i), "국가유산영향진단 용역"));
        }
        for (int i = 1; i <= 5; ++i) {
            rows.push_back(Row("TINY", i, "소수" + std::to_string(i), "소규모 정밀발굴조사용역"));
        }
        const auto kept = nara::ApplyPersistRules(rows);
        if (kept.size() != 6) {
            return Fail("ApplyPersistRules must keep only 6-company non-진단 notice");
        }
        for (const auto& row : kept) {
            if (row.notice_number != "DIG") {
                return Fail("ApplyPersistRules leaked IMP or TINY");
            }
        }
    }

    {
        std::vector<nara::ParticipantRow> rows = {
            Row("A", 1, "동국문화재연구원", "정밀발굴조사 용역"),
            Row("A", 2, "한울문화재연구원", "정밀발굴조사 용역"),
            Row("G", 1, "동국문화재연구원", "국가유산영향진단 용역"),
            Row("G", 2, "한울문화재연구원", "국가유산영향진단 용역"),
        };
        const auto kept = nara::ApplyChartRules(rows);
        if (kept.size() != 2) {
            return Fail("ApplyChartRules must keep Dongguk notice co-bidders only");
        }
        bool has_hanul = false;
        for (const auto& row : kept) {
            if (row.notice_number != "A") {
                return Fail("ApplyChartRules must drop 영향진단 even if Dongguk bid");
            }
            if (row.company_name.find("한울") != std::string::npos) {
                has_hanul = true;
            }
        }
        if (!has_hanul) {
            return Fail("ApplyChartRules must keep 한울 on Dongguk notice");
        }
    }

    {
        std::vector<nara::ParticipantRow> rows = {
            Row("H", 1, "재단법인 동국문화유산연구원"),
            Row("H", 2, "한울문화재연구원"),
            Row("X", 1, "한울문화재연구원"),
        };
        const auto kept = nara::KeepDonggukParticipatingNotices(rows);
        if (kept.size() != 2) {
            return Fail("C# Canonicalize maps 동국문화유산연구원 to 동국; co-bidders must stay");
        }
        for (const auto& row : kept) {
            if (row.notice_number != "H") {
                return Fail("alias Dongguk notice must not keep unrelated X");
            }
        }
    }

    return 0;
}
