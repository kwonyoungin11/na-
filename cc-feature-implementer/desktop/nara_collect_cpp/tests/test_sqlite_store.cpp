#include "nara/sqlite_store.h"

#include <winsqlite/winsqlite3.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

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
    if (sqlite3_open16(db_path.wstring().c_str(), &db) != SQLITE_OK) {
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
    const char* lower = nullptr,
    const char* region = "전국") {
    std::string line = "{\"notice_number\":\"";
    line += notice;
    line += "\",\"notice_name\":\"";
    line += name;
    line += "\",\"ordering_agency\":\"";
    line += agency;
    line += "\",\"region\":\"";
    line += region;
    line += "\",\"open_date\":\"";
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
    const char* lower = nullptr,
    const char* region = "전국") {
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
        out << JsonlLine(notice, name, agency, companies[i], i + 1, rate, open_date, lower, region) << "\n";
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
        WriteSixCompanyNotice(
            out,
            "JEON-000",
            "전주 정밀발굴조사 용역",
            "전주시",
            "2025-09-20 11:00:00",
            "99.65000",
            nullptr,
            "전북");
        WriteSixCompanyNotice(
            out,
            "GB-000",
            "경주 사정동 정밀발굴조사 용역",
            "경주시",
            "2025-09-21 11:00:00",
            "99.65000",
            nullptr,
            "경상북도");
    }

    const auto merged = nara::MergeJsonlIntoStore(jsonl, db);
    if (!merged.ok) {
        std::cerr << "FAIL: merge error " << merged.error << "\n";
        return 1;
    }
    if (merged.notices != 3) {
        std::cerr << "FAIL: preprocess should keep 전국+경북 only, got " << merged.notices << "\n";
        return 1;
    }

    const auto rows = nara::LoadParticipants(db);
    bool has_keep = false;
    bool has_new = false;
    bool has_tiny = false;
    bool has_impact = false;
    bool has_converted = false;
    bool has_jeon = false;
    bool has_gb = false;
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
        if (row.notice_number == "JEON-000") {
            has_jeon = true;
        }
        if (row.notice_number == "GB-000") {
            has_gb = row.region == "경북";
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
    if (has_jeon) {
        return Fail("전처리 후 전북은 전국/경북에서 제외");
    }
    if (!has_gb) {
        return Fail("경상북도 jsonl must store as 경북");
    }

    {
        const auto dup_jsonl = dir / "dup.jsonl";
        const auto dup_db = dir / "dup.db";
        if (!nara::EnsureParticipantsSchema(dup_db)) {
            return Fail("dup schema");
        }
        {
            std::ofstream out(dup_jsonl, std::ios::binary);
            WriteSixCompanyNotice(
                out, "DUP1-000", "중복 정밀발굴조사 용역", "울산광역시", "2026-08-14 11:00:00", "99.65000");
            WriteSixCompanyNotice(
                out, "DUP1-000", "중복 정밀발굴조사 용역", "울산광역시", "2026-08-14 11:00:00", "99.65000");
        }
        const auto dup_merged = nara::MergeJsonlIntoStore(dup_jsonl, dup_db);
        if (!dup_merged.ok) {
            std::cerr << "FAIL: dup merge " << dup_merged.error << "\n";
            return 1;
        }
        const auto dup_rows = nara::LoadParticipants(dup_db);
        int dup_count = 0;
        int dup_rank1 = 0;
        std::string last_rank1;
        for (const auto& row : dup_rows) {
            if (row.notice_number != "DUP1-000") {
                continue;
            }
            ++dup_count;
            if (row.rank == 1) {
                ++dup_rank1;
                last_rank1 = row.company_name;
            }
        }
        if (dup_count != 6) {
            std::cerr << "dup_count " << dup_count << "\n";
            return Fail("keyword-overlap jsonl must store one row per company, not two");
        }
        if (dup_rank1 != 1 || last_rank1.find("춘추") == std::string::npos) {
            std::cerr << "dup_rank1 " << dup_rank1 << " " << last_rank1 << "\n";
            return Fail("한 공고 rank==1 은 한 업체만");
        }
        const auto again = nara::MergeJsonlIntoStore(dup_jsonl, dup_db);
        if (!again.ok) {
            return Fail("second merge of the same jsonl must succeed");
        }
        int again_count = 0;
        for (const auto& row : nara::LoadParticipants(dup_db)) {
            if (row.notice_number == "DUP1-000") {
                ++again_count;
            }
        }
        if (again_count != 6) {
            std::cerr << "again_count " << again_count << "\n";
            return Fail("업데이트 재병합이 참가자를 또 복제하면 안 된다");
        }
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

    const auto empty_db = dir / "empty-max.db";
    if (!nara::EnsureParticipantsSchema(empty_db) || !nara::MaxParticipantOpenDay(empty_db).empty()) {
        return Fail("empty sqlite has no max open_date");
    }
    if (nara::MaxParticipantOpenDay(db).size() < 10) {
        return Fail("merged sqlite reports the latest stored open_date");
    }
    const auto gap_cmd = nara::AwardCollectGapCommandLine(
        (dir / "nara_award_collect.exe").string(), "2026-08-21", "2026-08-29", jsonl_path, env_path);
    if (gap_cmd.find("--from-date 2026-08-21") == std::string::npos
        || gap_cmd.find("--to-date 2026-08-29") == std::string::npos
        || gap_cmd.find("--start-month") != std::string::npos) {
        return Fail("gap collect command must skip stored months");
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
    if (std::filesystem::exists(live_jsonl) && std::filesystem::file_size(live_jsonl) > 0) {
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

    const auto hangul = dir / L"동국문화유산연구원";
    std::filesystem::create_directories(hangul);
    const auto hangul_exe = hangul / "nara_award_collect.exe";
    const auto hangul_jsonl = hangul / "cpp_award_collect.jsonl";
    const auto hangul_env = hangul / ".env";
    {
        std::ofstream out(hangul_exe, std::ios::binary);
        out << "x";
    }
    {
        std::ofstream out(hangul_env);
        out << "SERVICE_KEY=test\n";
    }
    if (!nara::PathExistsWide(hangul_exe) || !nara::PathExistsWide(hangul_env)) {
        return Fail("PathExistsWide must see collect exe and .env in a 한글 folder");
    }
    const auto acp = hangul_exe.string();
    wchar_t as_utf8[MAX_PATH] = {};
    MultiByteToWideChar(CP_UTF8, 0, acp.c_str(), -1, as_utf8, MAX_PATH);
    if (GetFileAttributesW(as_utf8) != INVALID_FILE_ATTRIBUTES) {
        return Fail("ACP path decoded as UTF-8 must not be treated as a valid collect path");
    }
    const auto gap_w =
        nara::AwardCollectGapCommandLineW(hangul_exe, "2026-08-27", "2026-09-07", hangul_jsonl, hangul_env);
    if (gap_w.find(L"동국문화유산연구원") == std::wstring::npos
        || gap_w.find(L"--from-date 2026-08-27") == std::wstring::npos
        || gap_w.find(L"--to-date 2026-09-07") == std::wstring::npos) {
        return Fail("wide gap command must keep the 한글 folder and the missing-day window");
    }

    // M3: Lossless 25-column persistence + RepairMissingAdjustRatesInStore + UpcomingBids table
    {
        const auto rich_db = dir / "rich.db";
        const auto rich_jsonl = dir / "rich.jsonl";
        if (!nara::EnsureParticipantsSchema(rich_db)) {
            return Fail("rich schema");
        }
        {
            std::ofstream out(rich_jsonl, std::ios::binary);
            const char* corps[] = {
                "충청문화재연구원",
                "백제문화재연구원",
                "금강문화재연구원",
                "한울문화재연구원",
                "고대문화재연구원",
                "동국문화재연구원",
            };
            for (int i = 0; i < 6; ++i) {
                const bool missing_rate = (i == 5);
                out << "{\"notice_number\":\"R26BK01676511-001\""
                    << ",\"notice_name\":\"부여 가림성 발굴조사 용역\""
                    << ",\"ordering_agency\":\"부여군\""
                    << ",\"region\":\"전국\""
                    << ",\"open_date\":\"2026-08-18 11:00:00\""
                    << ",\"rank\":" << (i + 1)
                    << ",\"company_name\":\"" << corps[i] << "\""
                    << ",\"biz_no\":\"505-82-0000" << (i + 1) << "\""
                    << ",\"bid_amount\":" << (i == 5 ? 86441380 : 86614880)
                    << ",\"bid_rate_vs_estimate\":" << (i == 5 ? 87.569 : 87.745);
                if (!missing_rate) {
                    out << ",\"adjust_rate\":99.85000";
                }
                out << ",\"lower_rate\":87.745}\n";
            }
        }
        const auto rich_merged = nara::MergeJsonlIntoStore(rich_jsonl, rich_db);
        if (!rich_merged.ok || rich_merged.notices != 1) {
            return Fail("rich jsonl merge must succeed");
        }
        const int repaired = nara::RepairMissingAdjustRatesInStore(rich_db);
        if (repaired < 0) {
            return Fail("RepairMissingAdjustRatesInStore must succeed");
        }
        const auto rich_rows = nara::LoadParticipants(rich_db);
        bool found_dongguk_repaired = false;
        for (const auto& r : rich_rows) {
            if (r.notice_number == "R26BK01676511-001" && r.company_name.find("동국") != std::string::npos) {
                if (r.biz_no == "505-82-00006" && r.bid_amount && *r.bid_amount == 86441380
                    && r.adjust_rate && *r.adjust_rate > 99.60 && *r.adjust_rate < 99.85) {
                    found_dongguk_repaired = true;
                }
            }
        }
        if (!found_dongguk_repaired) {
            return Fail("MergeJsonlIntoStore + RepairMissingAdjustRatesInStore must preserve biz_no/bid_amount and restore NULL adjust_rate");
        }

        std::vector<nara::UpcomingBidNotice> upcoming = {
            {"한국문화유산협회", "KAAH-69", "화성 남양성지 문화유산 시굴조사 용역", "한국문화유산협회", "전국", "2026-10-28 14:00", "2026-10-28 10:00", 88500000.0, 80454545.0},
            {"국방전자조달", "2026-국방-001", "육군 OO부대 매장유산 지표조사 용역", "육군제1234부대", "경북", "2026-10-25 11:00", "2026-10-25 10:00", 45000000.0, std::nullopt},
        };
        if (!nara::SaveUpcomingBidsToStore(upcoming, rich_db)) {
            return Fail("SaveUpcomingBidsToStore must store multi-source upcoming bids");
        }
        const auto loaded_all = nara::LoadUpcomingBidsFromStore(rich_db, "전국");
        const auto loaded_gb = nara::LoadUpcomingBidsFromStore(rich_db, "경북");
        if (loaded_all.size() != 1 || loaded_all[0].notice_number != "KAAH-69"
            || loaded_gb.size() != 1 || loaded_gb[0].notice_number != "2026-국방-001") {
            return Fail("LoadUpcomingBidsFromStore must separate 전국 and 경북 upcoming bids");
        }
    }

    std::cerr << "ok sqlite store merge and portable paths\n";
    return 0;
}
