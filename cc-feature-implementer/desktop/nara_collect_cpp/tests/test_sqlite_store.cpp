#include "nara/sqlite_store.h"

#include <winsqlite/winsqlite3.h>

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

std::filesystem::path MakeTempDir() {
    const auto stamp = std::chrono::steady_clock::now().time_since_epoch().count();
    const auto root = std::filesystem::temp_directory_path() / ("nara-sqlite-store-" + std::to_string(stamp));
    std::filesystem::create_directories(root);
    return root;
}

bool Exec(sqlite3* db, const char* sql) {
    char* err = nullptr;
    const int rc = sqlite3_exec(db, sql, nullptr, nullptr, &err);
    if (err) {
        sqlite3_free(err);
    }
    return rc == SQLITE_OK;
}

bool SeedKeepNotice(const std::filesystem::path& db_path) {
    sqlite3* db = nullptr;
    if (sqlite3_open(db_path.string().c_str(), &db) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return false;
    }
    const bool ok = Exec(
        db,
        "INSERT INTO participants("
        "notice_no, notice_number, notice_name, ordering_agency, region, rank,"
        "company_name, normalized_company_name, open_date, is_awarded, updated_at)"
        "VALUES('KEEP1','KEEP1-000','기존공고','기존기관','전국',1,'한울문화재연구원','한울문화재연구원',"
        "'2026-07-01 11:00:00',1,'2026-07-01 12:00:00');");
    sqlite3_close(db);
    return ok;
}

std::string JsonlLine(
    const char* notice,
    const char* name,
    const char* agency,
    const char* company,
    int rank,
    const char* rate,
    const char* open_date,
    const char* lower = nullptr) {
    std::string line = "{\"notice_number\":\"";
    line += notice;
    line += "\",\"notice_name\":\"";
    line += name;
    line += "\",\"ordering_agency\":\"";
    line += agency;
    line += "\",\"region\":\"전국\",\"open_date\":\"";
    line += open_date;
    line += "\",\"rank\":";
    line += std::to_string(rank);
    line += ",\"company_name\":\"";
    line += company;
    line += "\",\"adjust_rate\":";
    line += rate;
    if (lower) {
        line += ",\"lower_rate\":";
        line += lower;
    }
    line += "}";
    return line;
}

void WriteSixCompanyNotice(
    std::ostream& out,
    const char* notice,
    const char* name,
    const char* agency,
    const char* open_date,
    const char* dongguk_rate,
    const char* lower = nullptr) {
    const char* companies[] = {
        "춘추문화재연구원",
        "전북문화재연구원",
        "경강문화재연구원",
        "한울문화재연구원",
        "고대문화재연구원",
        "동국문화재연구원",
    };
    for (int i = 0; i < 6; ++i) {
        const char* rate = i == 5 ? dongguk_rate : "99.78000";
        out << JsonlLine(notice, name, agency, companies[i], i + 1, rate, open_date, lower) << "\n";
    }
}

}  // namespace

int main(int argc, char** argv) {
    if (argc >= 4 && std::string(argv[1]) == "--merge") {
        const auto merged = nara::MergeJsonlIntoStore(argv[2], argv[3]);
        std::cerr << "merge ok=" << merged.ok << " notices=" << merged.notices << " rows=" << merged.rows
                  << " err=" << merged.error << "\n";
        return merged.ok && merged.notices > 0 ? 0 : 1;
    }
    const auto dir = MakeTempDir();
    const auto db = dir / "nara-dashboard.db";
    const auto jsonl = dir / "cpp_award_collect.jsonl";

    if (!nara::EnsureParticipantsSchema(db)) {
        return Fail("EnsureParticipantsSchema should create a portable sqlite file");
    }
    if (!SeedKeepNotice(db)) {
        return Fail("seed KEEP notice");
    }

    {
        std::ofstream out(jsonl, std::ios::binary);
        WriteSixCompanyNotice(
            out, "NEW1-000", "울산 서생리 정밀발굴조사 용역", "울산광역시", "2025-09-12 11:00:00", "99.65000");
        WriteSixCompanyNotice(
            out,
            "NEW2-000",
            "창녕 정밀발굴조사 용역",
            "국토교통부",
            "2025-10-01 11:00:00",
            "86.447",
            "86.745");
        out << JsonlLine(
                   "TINY-000",
                   "표본조사",
                   "기관",
                   "동국문화재연구원",
                   1,
                   "99.70000",
                   "2025-11-01 11:00:00")
            << "\n";
        WriteSixCompanyNotice(
            out, "IMPACT-000", "금강 공주권역 국가유산영향진단 용역", "공주시", "2025-12-01 11:00:00", "99.61000");
    }

    const auto merged = nara::MergeJsonlIntoStore(jsonl, db);
    if (!merged.ok) {
        std::cerr << "FAIL: merge error " << merged.error << "\n";
        return 1;
    }
    if (merged.notices != 2) {
        std::cerr << "FAIL: persist-filtered merge should keep 2 notices, got " << merged.notices << "\n";
        return 1;
    }

    const auto rows = nara::LoadParticipants(db);
    bool has_keep = false;
    bool has_new = false;
    bool has_tiny = false;
    bool has_impact = false;
    bool has_converted = false;
    for (const auto& row : rows) {
        if (row.notice_number == "KEEP1-000") {
            has_keep = true;
        }
        if (row.notice_number == "NEW1-000" && row.company_name.find("동국") != std::string::npos) {
            has_new = row.ordering_agency == "울산광역시" && row.adjust_rate && *row.adjust_rate > 99.6;
        }
        if (row.notice_number == "TINY-000") {
            has_tiny = true;
        }
        if (row.notice_number == "IMPACT-000") {
            has_impact = true;
        }
        if (row.notice_number == "NEW2-000" && row.company_name.find("동국") != std::string::npos) {
            has_converted = row.adjust_rate && *row.adjust_rate >= 99.0 && *row.adjust_rate <= 101.0;
        }
    }
    if (!has_keep) {
        return Fail("merge must keep notices already in sqlite");
    }
    if (!has_new) {
        return Fail("merge must add jsonl notices from the collect period");
    }
    if (has_tiny) {
        return Fail("5-company jsonl notice must not replace or insert");
    }
    if (has_impact) {
        return Fail("영향진단 must not be stored");
    }
    if (has_converted) {
        return Fail("예가대비 86.x를 100.00 기초대비사정률로 바꾸지 않는다");
    }

    _putenv_s("NARA_DASHBOARD_DB", "");
    _putenv_s("NARA_ENV", "");
    _putenv_s("NARA_APP_DIR", dir.string().c_str());
    const auto app_dir = nara::DefaultAppDir();
    const auto db_path = nara::DefaultDashboardDbPath();
    const auto jsonl_path = nara::DefaultAwardJsonlPath();
    const auto env_path = nara::DefaultServiceKeyEnvPath();
    if (std::filesystem::path(app_dir) != dir) {
        return Fail("NARA_APP_DIR should be the portable folder");
    }
    if (std::filesystem::path(db_path) != db) {
        std::cerr << "FAIL: portable db path " << db_path << "\n";
        return 1;
    }
    if (std::filesystem::path(jsonl_path) != jsonl) {
        std::cerr << "FAIL: portable jsonl path " << jsonl_path << "\n";
        return 1;
    }
    if (std::filesystem::path(env_path) != dir / ".env") {
        std::cerr << "FAIL: portable env path " << env_path << "\n";
        return 1;
    }

    const auto cmd = nara::AwardCollectCommandLine(
        (dir / "nara_award_collect.exe").string(), "2025-08", "2026-08", jsonl_path, env_path);
    if (cmd.find("--start-month 2025-08") == std::string::npos
        || cmd.find("--end-month 2026-08") == std::string::npos
        || cmd.find("--out") == std::string::npos
        || cmd.find("--env") == std::string::npos
        || cmd.find("cpp_award_collect.jsonl") == std::string::npos) {
        std::cerr << "FAIL: collect command must pass period and portable out/env: " << cmd << "\n";
        return 1;
    }

    _putenv_s("NARA_APP_DIR", "");

    const auto live_jsonl =
        std::filesystem::path(R"(A:\c\cc-feature-implementer\desktop\nara_collect_cpp\build\Release\cpp_award_collect.jsonl)");
    if (std::filesystem::exists(live_jsonl)) {
        const auto live_db = dir / "live-merge.db";
        if (!nara::EnsureParticipantsSchema(live_db)) {
            return Fail("live-merge schema");
        }
        const auto live = nara::MergeJsonlIntoStore(live_jsonl, live_db);
        std::cerr << "live merge ok=" << live.ok << " notices=" << live.notices << " rows=" << live.rows
                  << " err=" << live.error << "\n";
        if (!live.ok || live.notices < 1) {
            return Fail("live collect jsonl should merge into sqlite");
        }
        const auto populated = dir / "populated-merge.db";
        std::filesystem::copy_file(
            R"(A:\c\cc-feature-implementer\desktop\nara_collect_cpp\build\Release\nara-dashboard.db)",
            populated,
            std::filesystem::copy_options::overwrite_existing);
        const auto onto = nara::MergeJsonlIntoStore(live_jsonl, populated);
        std::cerr << "populated merge ok=" << onto.ok << " notices=" << onto.notices << " rows=" << onto.rows
                  << " err=" << onto.error << "\n";
        if (!onto.ok || onto.notices < 1) {
            return Fail("jsonl must merge into the existing portable sqlite without dropping it");
        }
        const auto after = nara::LoadParticipants(populated);
        bool has_2025 = false;
        for (const auto& row : after) {
            if (row.open_date.size() >= 4 && row.open_date.substr(0, 4) == "2025") {
                has_2025 = true;
                break;
            }
        }
        if (!has_2025) {
            return Fail("merged portable sqlite should contain the collect-period 2025 notices");
        }
    }

    std::cerr << "ok sqlite store merge and portable paths\n";
    return 0;
}
