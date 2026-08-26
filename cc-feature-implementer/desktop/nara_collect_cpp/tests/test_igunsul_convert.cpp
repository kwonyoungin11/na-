#include "nara/igunsul_convert.h"

#include <filesystem>
#include <iostream>
#include <string>

namespace {

std::filesystem::path FixtureDir() {
    return std::filesystem::path(__FILE__).parent_path().parent_path().parent_path() /
           "NaraCollector.Desktop.Tests" / "Fixtures" / "igunsul";
}

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    const auto dir = FixtureDir();
    if (!std::filesystem::exists(dir)) {
        std::cerr << "missing fixture dir: " << dir.string() << "\n";
        return 1;
    }

    const auto impact = nara::ConvertPair(
        dir / "geumgang-gongju_overview.xlsx", dir / "geumgang-gongju_participants.xlsx");
    if (!impact.empty()) {
        return Fail("ConvertPair must drop notice_name containing 영향진단");
    }

    const auto kept = nara::ConvertPair(
        dir / "changnyeong-goam_overview.xlsx", dir / "changnyeong-goam_participants.xlsx");
    if (kept.size() != 6) {
        std::cerr << "FAIL: changnyeong-goam expected 6 rows, got " << kept.size() << "\n";
        return 1;
    }
    for (const auto& row : kept) {
        if (row.notice_name.find("영향진단") != std::string::npos) {
            return Fail("kept notice must not contain 영향진단");
        }
    }

    const auto directory_rows = nara::ConvertDirectory(dir);
    if (directory_rows.size() != 12) {
        std::cerr << "FAIL: ConvertDirectory expected 12 rows, got " << directory_rows.size()
                  << "\n";
        return 1;
    }
    for (const auto& row : directory_rows) {
        if (row.notice_name.find("영향진단") != std::string::npos) {
            return Fail("ConvertDirectory must not ingest 영향진단 awards");
        }
    }

    std::cerr << "ok ConvertPair drops 영향진단\n";
    return 0;
}
