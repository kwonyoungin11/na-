#include "nara/sqlite_store.h"

#include "nara/award_collect.h"
#include "nara/notice_rules.h"

#include <winsqlite/winsqlite3.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <map>
#include <string>
#include <vector>

namespace nara {
namespace {

constexpr const char* kWorkspaceDashboardDb = "A:\\c\\nara-workspace\\app\\nara-dashboard.db";
constexpr const char* kWorkspaceEnv = "A:\\c\\.env";

const char* kCreateParticipantsSql =
    "CREATE TABLE IF NOT EXISTS participants ("
    "notice_no TEXT NOT NULL,"
    "notice_number TEXT NOT NULL,"
    "notice_name TEXT NOT NULL,"
    "ordering_agency TEXT NOT NULL,"
    "region TEXT NOT NULL,"
    "rank INTEGER NOT NULL,"
    "company_name TEXT NOT NULL,"
    "normalized_company_name TEXT NOT NULL,"
    "open_date TEXT NOT NULL,"
    "bid_date TEXT NULL,"
    "bid_amount INTEGER NULL,"
    "bid_rate_vs_estimate REAL NULL,"
    "bid_rate_vs_base REAL NULL,"
    "adjust_rate REAL NULL,"
    "estimate_price INTEGER NULL,"
    "lower_limit_price INTEGER NULL,"
    "base_amount INTEGER NULL,"
    "price_adjustment_rate REAL NULL,"
    "lower_rate REAL NULL,"
    "lower_diff INTEGER NULL,"
    "biz_no TEXT NULL,"
    "ceo_name TEXT NULL,"
    "remark TEXT NULL,"
    "is_awarded INTEGER NOT NULL DEFAULT 0,"
    "updated_at TEXT NOT NULL"
    ");";

const char* kDedupeParticipantsSql =
    "DELETE FROM participants WHERE rowid NOT IN ("
    "SELECT MIN(rowid) FROM participants GROUP BY notice_number, company_name);";

const char* kUniqueNoticeCompanySql =
    "CREATE UNIQUE INDEX IF NOT EXISTS idx_participants_notice_company "
    "ON participants(notice_number, company_name);";

std::vector<ParticipantRow> UniqueByNoticeCompany(std::vector<ParticipantRow> rows) {
    std::map<std::string, ParticipantRow> unique;
    for (auto& row : rows) {
        auto id = CompanyIdentity(row);
        if (id.empty()) {
            id = CanonicalizeCompany(row.company_name);
        }
        if (id.empty()) {
            continue;
        }
        const auto notice = row.notice_number.empty() ? row.notice_no : row.notice_number;
        const auto key = notice + "\x1f" + id;
        auto found = unique.find(key);
        if (found == unique.end()) {
            unique.emplace(key, std::move(row));
            continue;
        }
        if (row.rank > 0 && (found->second.rank <= 0 || row.rank < found->second.rank)) {
            found->second = std::move(row);
        }
    }
    std::vector<ParticipantRow> out;
    out.reserve(unique.size());
    for (auto& pair : unique) {
        out.push_back(std::move(pair.second));
    }
    return out;
}

const char* EnvOrNull(const char* name) {
    const char* value = std::getenv(name);
    if (value == nullptr || value[0] == '\0') {
        return nullptr;
    }
    return value;
}

std::filesystem::path CurrentModuleDir() {
    wchar_t module[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, module, MAX_PATH);
    return std::filesystem::path(module).parent_path();
}

std::string PathString(const std::filesystem::path& path) {
    return path.lexically_normal().string();
}

std::string PathUtf8(const std::filesystem::path& path) {
    const auto u8 = path.lexically_normal().u8string();
    return std::string(u8.begin(), u8.end());
}

std::string QuoteArg(const std::string& value) {
    return "\"" + value + "\"";
}

std::optional<double> ColumnDouble(sqlite3_stmt* stmt, int col) {
    if (sqlite3_column_type(stmt, col) == SQLITE_NULL) {
        return std::nullopt;
    }
    return sqlite3_column_double(stmt, col);
}

std::optional<long long> ColumnInt64(sqlite3_stmt* stmt, int col) {
    if (sqlite3_column_type(stmt, col) == SQLITE_NULL) {
        return std::nullopt;
    }
    return sqlite3_column_int64(stmt, col);
}

std::string ColumnText(sqlite3_stmt* stmt, int col) {
    const unsigned char* text = sqlite3_column_text(stmt, col);
    return text ? reinterpret_cast<const char*>(text) : std::string{};
}

void BindOptDouble(sqlite3_stmt* stmt, int index, const std::optional<double>& value) {
    if (value) {
        sqlite3_bind_double(stmt, index, *value);
    } else {
        sqlite3_bind_null(stmt, index);
    }
}

void BindOptInt64(sqlite3_stmt* stmt, int index, const std::optional<long long>& value) {
    if (value) {
        sqlite3_bind_int64(stmt, index, *value);
    } else {
        sqlite3_bind_null(stmt, index);
    }
}

std::string NowStamp() {
    std::time_t now = std::time(nullptr);
    std::tm local{};
    localtime_s(&local, &now);
    char buf[32];
    std::strftime(buf, sizeof(buf), "%Y-%m-%d %H:%M:%S", &local);
    return buf;
}

std::string NoticeNoOf(const std::string& notice_number) {
    if (notice_number.empty()) {
        return {};
    }
    const auto dash = notice_number.rfind('-');
    if (dash == std::string::npos || dash == 0) {
        return notice_number;
    }
    const auto suffix = notice_number.substr(dash + 1);
    if (suffix.empty()
        || !std::all_of(suffix.begin(), suffix.end(), [](unsigned char ch) { return std::isdigit(ch); })) {
        return notice_number;
    }
    if (notice_number[0] == 'R' || notice_number[0] == 'r' || suffix.size() <= 3) {
        return notice_number.substr(0, dash);
    }
    return notice_number;
}

std::string NormalizeStoredRegion(std::string region) {
    if (region.find("경상북") != std::string::npos || region.find("경북") != std::string::npos) {
        return "경북";
    }
    if (region.empty() || region.find("전국") != std::string::npos) {
        return "전국";
    }
    return region;
}

std::optional<double> ChartAdjustRate(const std::optional<double>& raw, const std::optional<double>& lower) {
    if (!raw) {
        return std::nullopt;
    }
    if (*raw >= 80.0 && *raw <= 95.0) {
        return std::nullopt;
    }
    return raw;
}

bool ExecSql(sqlite3* db, const char* sql, std::string* error) {
    char* err = nullptr;
    const int rc = sqlite3_exec(db, sql, nullptr, nullptr, &err);
    if (rc != SQLITE_OK) {
        if (error) {
            *error = err ? err : "sqlite exec failed";
        }
        sqlite3_free(err);
        return false;
    }
    return true;
}

std::vector<ParticipantRow> LoadJsonlRows(const std::filesystem::path& jsonl_path) {
    std::vector<ParticipantRow> rows;
    std::ifstream in(jsonl_path);
    if (!in) {
        return rows;
    }
    std::string line;
    while (std::getline(in, line)) {
        if (!line.empty() && line.back() == '\r') {
            line.pop_back();
        }
        if (line.empty()) {
            continue;
        }
        const auto notice = JsonString(line, "notice_number");
        const auto company = JsonString(line, "company_name");
        if (notice.empty() || company.empty()) {
            continue;
        }
        ParticipantRow row;
        row.notice_number = notice;
        row.notice_no = NoticeNoOf(notice);
        row.notice_name = JsonString(line, "notice_name");
        row.ordering_agency = JsonString(line, "ordering_agency");
        row.region = CollectRegionToken(JsonString(line, "region"));
        if (row.region != "전국" && row.region != "경북") {
            continue;
        }
        row.rank = static_cast<int>(JsonNumber(line, "rank").value_or(0));
        row.company_name = company;
        row.normalized_company_name = CanonicalizeCompany(company);
        row.open_date = JsonString(line, "open_date");
        std::replace(row.open_date.begin(), row.open_date.end(), '/', '-');
        row.lower_rate = JsonNumber(line, "lower_rate");
        row.biz_no = JsonString(line, "biz_no");
        row.bid_date = JsonString(line, "bid_date");
        const auto bid_amount = JsonNumber(line, "bid_amount");
        if (bid_amount) {
            row.bid_amount = static_cast<long long>(std::llround(*bid_amount));
        }
        const auto base_amount = JsonNumber(line, "base_amount");
        if (base_amount) {
            row.base_amount = static_cast<long long>(std::llround(*base_amount));
        }
        const auto estimate_price = JsonNumber(line, "estimate_price");
        if (estimate_price) {
            row.estimate_price = static_cast<long long>(std::llround(*estimate_price));
        }
        const auto lower_limit_price = JsonNumber(line, "lower_limit_price");
        if (lower_limit_price) {
            row.lower_limit_price = static_cast<long long>(std::llround(*lower_limit_price));
        }
        row.bid_rate_vs_estimate = JsonNumber(line, "bid_rate_vs_estimate");
        row.price_adjustment_rate = JsonNumber(line, "price_adjustment_rate");
        const auto raw_rate = JsonNumber(line, "adjust_rate");
        if (raw_rate && *raw_rate >= 80.0 && *raw_rate <= 95.0) {
            row.bid_rate_vs_base = raw_rate;
        }
        if (bid_amount && base_amount && row.lower_rate) {
            row.adjust_rate = ComputeBaseAdjustRate(*bid_amount, *base_amount, *row.lower_rate);
        } else {
            row.adjust_rate = ChartAdjustRate(raw_rate, row.lower_rate);
        }
        row.is_awarded = row.rank == 1;
        rows.push_back(std::move(row));
    }
    return rows;
}

}  // namespace

std::string WorkspaceDashboardDbPath() {
    if (std::filesystem::exists(kWorkspaceDashboardDb)) {
        return kWorkspaceDashboardDb;
    }
    return DefaultDashboardDbPath();
}

std::string DefaultAppDir() {
    if (const char* env = EnvOrNull("NARA_APP_DIR")) {
        return env;
    }
    return PathString(CurrentModuleDir());
}

std::string DefaultDashboardDbPath() {
    if (const char* env = EnvOrNull("NARA_DASHBOARD_DB")) {
        return env;
    }
    const auto portable = std::filesystem::path(DefaultAppDir()) / "nara-dashboard.db";
    if (EnvOrNull("NARA_APP_DIR") || std::filesystem::exists(portable)) {
        return PathString(portable);
    }
    if (std::filesystem::exists(kWorkspaceDashboardDb)) {
        return kWorkspaceDashboardDb;
    }
    return PathString(portable);
}

std::string DefaultAwardJsonlPath() {
    if (const char* env = EnvOrNull("NARA_AWARD_JSONL")) {
        return env;
    }
    return PathString(std::filesystem::path(DefaultDashboardDbPath()).parent_path() / "cpp_award_collect.jsonl");
}

std::string DefaultServiceKeyEnvPath() {
    if (const char* env = EnvOrNull("NARA_ENV")) {
        return env;
    }
    const auto portable = std::filesystem::path(DefaultAppDir()) / ".env";
    if (EnvOrNull("NARA_APP_DIR") || std::filesystem::exists(portable)) {
        return PathString(portable);
    }
    if (std::filesystem::exists(kWorkspaceEnv)) {
        return kWorkspaceEnv;
    }
    return PathString(portable);
}

std::string AwardCollectCommandLine(
    const std::string& exe_path,
    const std::string& start_month,
    const std::string& end_month,
    const std::string& jsonl_path,
    const std::string& env_path) {
    return QuoteArg(exe_path) + " --start-month " + start_month + " --end-month " + end_month + " --out "
           + QuoteArg(jsonl_path) + " --env " + QuoteArg(env_path);
}

std::string AwardCollectGapCommandLine(
    const std::string& exe_path,
    const std::string& from_date,
    const std::string& to_date,
    const std::string& jsonl_path,
    const std::string& env_path) {
    return QuoteArg(exe_path) + " --from-date " + from_date + " --to-date " + to_date + " --out "
           + QuoteArg(jsonl_path) + " --env " + QuoteArg(env_path);
}

namespace {

std::wstring QuoteArgW(const std::filesystem::path& path) {
    return L"\"" + path.lexically_normal().wstring() + L"\"";
}

std::wstring AsciiWide(const std::string& value) {
    return std::wstring(value.begin(), value.end());
}

}  // namespace

std::wstring AwardCollectCommandLineW(
    const std::filesystem::path& exe_path,
    const std::string& start_month,
    const std::string& end_month,
    const std::filesystem::path& jsonl_path,
    const std::filesystem::path& env_path) {
    return QuoteArgW(exe_path) + L" --start-month " + AsciiWide(start_month) + L" --end-month " + AsciiWide(end_month)
           + L" --out " + QuoteArgW(jsonl_path) + L" --env " + QuoteArgW(env_path);
}

std::wstring AwardCollectGapCommandLineW(
    const std::filesystem::path& exe_path,
    const std::string& from_date,
    const std::string& to_date,
    const std::filesystem::path& jsonl_path,
    const std::filesystem::path& env_path) {
    return QuoteArgW(exe_path) + L" --from-date " + AsciiWide(from_date) + L" --to-date " + AsciiWide(to_date)
           + L" --out " + QuoteArgW(jsonl_path) + L" --env " + QuoteArgW(env_path);
}

bool PathExistsWide(const std::filesystem::path& path) {
    return GetFileAttributesW(path.lexically_normal().c_str()) != INVALID_FILE_ATTRIBUTES;
}

std::string MaxParticipantOpenDay(const std::filesystem::path& db_path) {
    sqlite3* db = nullptr;
    if (sqlite3_open(PathUtf8(db_path).c_str(), &db) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return {};
    }
    sqlite3_stmt* stmt = nullptr;
    const char* sql = "SELECT MAX(substr(open_date,1,10)) FROM participants;";
    std::string day;
    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) == SQLITE_OK) {
        if (sqlite3_step(stmt) == SQLITE_ROW) {
            if (const auto* text = sqlite3_column_text(stmt, 0)) {
                day = reinterpret_cast<const char*>(text);
            }
        }
        sqlite3_finalize(stmt);
    }
    sqlite3_close(db);
    return day;
}

bool EnsureParticipantsSchema(const std::filesystem::path& db_path) {
    std::error_code ec;
    std::filesystem::create_directories(db_path.parent_path(), ec);
    sqlite3* db = nullptr;
    if (sqlite3_open(PathUtf8(db_path).c_str(), &db) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return false;
    }
    if (!ExecSql(db, kCreateParticipantsSql, nullptr)) {
        sqlite3_close(db);
        return false;
    }
    if (!ExecSql(db, kDedupeParticipantsSql, nullptr)) {
        sqlite3_close(db);
        return false;
    }
    const bool ok = ExecSql(db, kUniqueNoticeCompanySql, nullptr);
    sqlite3_close(db);
    if (ok) {
        RepairMissingAdjustRatesInStore(db_path);
    }
    return ok;
}

void EnsurePortableDatabase() {
    const auto app = std::filesystem::path(DefaultAppDir());
    const auto dest = app / "nara-dashboard.db";
    const auto dest_jsonl = app / "cpp_award_collect.jsonl";
    const auto workspace_jsonl = std::filesystem::path(kWorkspaceDashboardDb).parent_path() / "cpp_award_collect.jsonl";
    std::error_code ec;
    if (!std::filesystem::exists(dest_jsonl) && std::filesystem::exists(workspace_jsonl)) {
        std::filesystem::copy_file(workspace_jsonl, dest_jsonl, ec);
        ec.clear();
    }
    if (std::filesystem::exists(dest)) {
        EnsureParticipantsSchema(dest);
        return;
    }
    if (std::filesystem::exists(kWorkspaceDashboardDb)) {
        std::filesystem::copy_file(kWorkspaceDashboardDb, dest, ec);
        if (!ec) {
            return;
        }
    }
    EnsureParticipantsSchema(dest);
}

std::vector<ParticipantRow> LoadParticipants(const std::filesystem::path& db_path) {
    std::vector<ParticipantRow> rows;
    sqlite3* db = nullptr;
    if (sqlite3_open_v2(PathUtf8(db_path).c_str(), &db, SQLITE_OPEN_READONLY, nullptr) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return rows;
    }
    sqlite3_stmt* stmt = nullptr;
    const char* sql =
        "SELECT notice_no, notice_number, notice_name, ordering_agency, region, rank, "
        "company_name, normalized_company_name, open_date, bid_date, "
        "bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate, "
        "estimate_price, lower_limit_price, base_amount, price_adjustment_rate, "
        "lower_rate, biz_no, is_awarded FROM participants;";
    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        sqlite3_close(db);
        return rows;
    }
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        ParticipantRow row;
        row.notice_no = ColumnText(stmt, 0);
        row.notice_number = ColumnText(stmt, 1);
        row.notice_name = ColumnText(stmt, 2);
        row.ordering_agency = ColumnText(stmt, 3);
        row.region = ColumnText(stmt, 4);
        row.rank = sqlite3_column_int(stmt, 5);
        row.company_name = ColumnText(stmt, 6);
        row.normalized_company_name = ColumnText(stmt, 7);
        row.open_date = ColumnText(stmt, 8);
        row.bid_date = ColumnText(stmt, 9);
        row.bid_amount = ColumnInt64(stmt, 10);
        row.bid_rate_vs_estimate = ColumnDouble(stmt, 11);
        row.bid_rate_vs_base = ColumnDouble(stmt, 12);
        row.adjust_rate = ColumnDouble(stmt, 13);
        row.estimate_price = ColumnInt64(stmt, 14);
        row.lower_limit_price = ColumnInt64(stmt, 15);
        row.base_amount = ColumnInt64(stmt, 16);
        row.price_adjustment_rate = ColumnDouble(stmt, 17);
        row.lower_rate = ColumnDouble(stmt, 18);
        row.biz_no = ColumnText(stmt, 19);
        row.is_awarded = sqlite3_column_int(stmt, 20) != 0;
        rows.push_back(std::move(row));
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return UniqueByNoticeCompany(std::move(rows));
}

JsonlMergeResult MergeJsonlIntoStore(
    const std::filesystem::path& jsonl_path,
    const std::filesystem::path& db_path) {
    JsonlMergeResult result;
    if (!std::filesystem::exists(jsonl_path)) {
        result.ok = true;
        return result;
    }
    if (!EnsureParticipantsSchema(db_path)) {
        result.error = "sqlite schema failed";
        return result;
    }

    auto incoming = ApplyPersistRules(LoadJsonlRows(jsonl_path));
    std::map<std::string, std::vector<ParticipantRow>> by_notice;
    for (auto& row : incoming) {
        if (row.notice_number.empty()) {
            continue;
        }
        by_notice[row.notice_number].push_back(std::move(row));
    }

    sqlite3* db = nullptr;
    if (sqlite3_open(PathUtf8(db_path).c_str(), &db) != SQLITE_OK) {
        result.error = db ? sqlite3_errmsg(db) : "sqlite open failed";
        if (db) {
            sqlite3_close(db);
        }
        return result;
    }
    if (!ExecSql(db, "BEGIN;", &result.error)) {
        sqlite3_close(db);
        return result;
    }

    sqlite3_stmt* del = nullptr;
    sqlite3_stmt* ins = nullptr;
    const char* delete_sql =
        "DELETE FROM participants WHERE notice_number=? OR notice_no=? OR notice_number=? OR notice_no=?;";
    const char* insert_sql =
        "INSERT INTO participants("
        "notice_no, notice_number, notice_name, ordering_agency, region, rank,"
        "company_name, normalized_company_name, open_date, bid_date,"
        "bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate,"
        "estimate_price, lower_limit_price, base_amount, price_adjustment_rate,"
        "lower_rate, lower_diff, biz_no, ceo_name, remark, is_awarded, updated_at)"
        "VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);";
    if (sqlite3_prepare_v2(db, delete_sql, -1, &del, nullptr) != SQLITE_OK
        || sqlite3_prepare_v2(db, insert_sql, -1, &ins, nullptr) != SQLITE_OK) {
        result.error = sqlite3_errmsg(db);
        sqlite3_finalize(del);
        sqlite3_finalize(ins);
        sqlite3_close(db);
        return result;
    }

    const auto stamp = NowStamp();
    int row_count = 0;
    for (const auto& [notice, raw_items] : by_notice) {
        const auto items = UniqueByNoticeCompany(raw_items);
        const auto notice_no = items.empty() ? NoticeNoOf(notice) : items.front().notice_no;
        sqlite3_reset(del);
        sqlite3_bind_text(del, 1, notice.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(del, 2, notice.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(del, 3, notice_no.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(del, 4, notice_no.c_str(), -1, SQLITE_TRANSIENT);
        if (sqlite3_step(del) != SQLITE_DONE) {
            result.error = sqlite3_errmsg(db);
            sqlite3_finalize(del);
            sqlite3_finalize(ins);
            sqlite3_close(db);
            return result;
        }
        for (const auto& row : items) {
            sqlite3_reset(ins);
            sqlite3_bind_text(ins, 1, row.notice_no.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 2, row.notice_number.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 3, row.notice_name.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 4, row.ordering_agency.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 5, row.region.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_int(ins, 6, row.rank);
            sqlite3_bind_text(ins, 7, row.company_name.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 8, row.normalized_company_name.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 9, row.open_date.c_str(), -1, SQLITE_TRANSIENT);
            if (!row.bid_date.empty()) {
                sqlite3_bind_text(ins, 10, row.bid_date.c_str(), -1, SQLITE_TRANSIENT);
            } else {
                sqlite3_bind_null(ins, 10);
            }
            BindOptInt64(ins, 11, row.bid_amount);
            BindOptDouble(ins, 12, row.bid_rate_vs_estimate);
            BindOptDouble(ins, 13, row.bid_rate_vs_base);
            BindOptDouble(ins, 14, row.adjust_rate);
            BindOptInt64(ins, 15, row.estimate_price);
            BindOptInt64(ins, 16, row.lower_limit_price);
            BindOptInt64(ins, 17, row.base_amount);
            BindOptDouble(ins, 18, row.price_adjustment_rate);
            BindOptDouble(ins, 19, row.lower_rate);
            sqlite3_bind_null(ins, 20);
            sqlite3_bind_text(ins, 21, row.biz_no.c_str(), -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 22, "", -1, SQLITE_TRANSIENT);
            sqlite3_bind_text(ins, 23, "", -1, SQLITE_TRANSIENT);
            sqlite3_bind_int(ins, 24, row.is_awarded ? 1 : 0);
            sqlite3_bind_text(ins, 25, stamp.c_str(), -1, SQLITE_TRANSIENT);
            if (sqlite3_step(ins) != SQLITE_DONE) {
                result.error = sqlite3_errmsg(db);
                sqlite3_finalize(del);
                sqlite3_finalize(ins);
                sqlite3_close(db);
                return result;
            }
            ++row_count;
        }
    }

    sqlite3_finalize(del);
    sqlite3_finalize(ins);
    if (!ExecSql(db, "COMMIT;", &result.error)) {
        sqlite3_close(db);
        return result;
    }
    sqlite3_close(db);
    RepairMissingAdjustRatesInStore(db_path);
    result.ok = true;
    result.notices = static_cast<int>(by_notice.size());
    result.rows = row_count;
    return result;
}

int RepairMissingAdjustRatesInStore(const std::filesystem::path& db_path) {
    sqlite3* db = nullptr;
    if (sqlite3_open(PathUtf8(db_path).c_str(), &db) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return -1;
    }
    struct NullCandidate {
        long long rowid = 0;
        std::string notice_number;
        std::optional<double> bid_amount;
        std::optional<double> bid_rate;
        std::optional<double> adjust_rate;
        int rank = 0;
    };
    std::map<std::string, std::vector<NullCandidate>> by_notice;
    sqlite3_stmt* sel = nullptr;
    const char* sel_sql =
        "SELECT rowid, notice_number, bid_amount, COALESCE(bid_rate_vs_estimate, bid_rate_vs_base), adjust_rate, rank "
        "FROM participants WHERE notice_number IN ("
        "SELECT DISTINCT notice_number FROM participants WHERE adjust_rate IS NULL);";
    if (sqlite3_prepare_v2(db, sel_sql, -1, &sel, nullptr) == SQLITE_OK) {
        while (sqlite3_step(sel) == SQLITE_ROW) {
            NullCandidate c;
            c.rowid = sqlite3_column_int64(sel, 0);
            c.notice_number = ColumnText(sel, 1);
            c.bid_amount = ColumnDouble(sel, 2);
            c.bid_rate = ColumnDouble(sel, 3);
            c.adjust_rate = ColumnDouble(sel, 4);
            c.rank = sqlite3_column_int(sel, 5);
            by_notice[c.notice_number].push_back(std::move(c));
        }
        sqlite3_finalize(sel);
    }
    if (by_notice.empty()) {
        sqlite3_close(db);
        return 0;
    }

    int updated = 0;
    ExecSql(db, "BEGIN;", nullptr);
    sqlite3_stmt* upd = nullptr;
    const char* upd_sql = "UPDATE participants SET adjust_rate=? WHERE rowid=?;";
    if (sqlite3_prepare_v2(db, upd_sql, -1, &upd, nullptr) == SQLITE_OK) {
        for (auto& [notice, rows] : by_notice) {
            std::optional<double> anchor_rate;
            std::optional<double> anchor_amount;
            std::optional<double> anchor_bid_rate;
            for (const auto& r : rows) {
                if (r.adjust_rate && *r.adjust_rate >= 95.0 && *r.adjust_rate <= 105.0) {
                    if ((r.bid_amount && *r.bid_amount > 0.0) || (r.bid_rate && *r.bid_rate > 0.0)) {
                        anchor_rate = r.adjust_rate;
                        anchor_amount = r.bid_amount;
                        anchor_bid_rate = r.bid_rate;
                        if (r.rank == 1) {
                            break;
                        }
                    }
                }
            }
            if (!anchor_rate) {
                continue;
            }
            for (const auto& r : rows) {
                if (r.adjust_rate) {
                    continue;
                }
                std::optional<double> inferred;
                if (r.bid_amount && anchor_amount && *anchor_amount > 0.0) {
                    inferred = std::round(((*r.bid_amount / *anchor_amount) * *anchor_rate) * 100000.0) / 100000.0;
                } else if (r.bid_rate && anchor_bid_rate && *anchor_bid_rate > 0.0) {
                    inferred = std::round(((*r.bid_rate / *anchor_bid_rate) * *anchor_rate) * 100000.0) / 100000.0;
                }
                if (inferred) {
                    sqlite3_reset(upd);
                    sqlite3_bind_double(upd, 1, *inferred);
                    sqlite3_bind_int64(upd, 2, r.rowid);
                    if (sqlite3_step(upd) == SQLITE_DONE) {
                        ++updated;
                    }
                }
            }
        }
        sqlite3_finalize(upd);
    }
    ExecSql(db, "COMMIT;", nullptr);
    sqlite3_close(db);
    return updated;
}

namespace {

const char* kCreateUpcomingBidsSql =
    "CREATE TABLE IF NOT EXISTS upcoming_bids ("
    "source TEXT NOT NULL,"
    "notice_number TEXT NOT NULL,"
    "notice_name TEXT NOT NULL,"
    "ordering_agency TEXT NOT NULL,"
    "region TEXT NOT NULL,"
    "open_date TEXT NOT NULL,"
    "deadline_date TEXT NULL,"
    "base_amount REAL NULL,"
    "estimate_price REAL NULL,"
    "updated_at TEXT NOT NULL,"
    "PRIMARY KEY(source, notice_number)"
    ");";

}  // namespace

bool SaveUpcomingBidsToStore(
    const std::vector<UpcomingBidNotice>& bids,
    const std::filesystem::path& db_path) {
    std::error_code ec;
    std::filesystem::create_directories(db_path.parent_path(), ec);
    sqlite3* db = nullptr;
    if (sqlite3_open(PathUtf8(db_path).c_str(), &db) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return false;
    }
    if (!ExecSql(db, kCreateUpcomingBidsSql, nullptr) || !ExecSql(db, "BEGIN;", nullptr)) {
        sqlite3_close(db);
        return false;
    }
    sqlite3_stmt* ins = nullptr;
    const char* sql =
        "INSERT OR REPLACE INTO upcoming_bids("
        "source, notice_number, notice_name, ordering_agency, region, "
        "open_date, deadline_date, base_amount, estimate_price, updated_at)"
        "VALUES(?,?,?,?,?,?,?,?,?,?);";
    if (sqlite3_prepare_v2(db, sql, -1, &ins, nullptr) != SQLITE_OK) {
        sqlite3_close(db);
        return false;
    }
    const auto stamp = NowStamp();
    for (const auto& b : bids) {
        const auto reg = NormalizeStoredRegion(b.region);
        if (reg != "전국" && reg != "경북") {
            continue;
        }
        sqlite3_reset(ins);
        sqlite3_bind_text(ins, 1, b.source.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 2, b.notice_number.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 3, b.notice_name.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 4, b.agency.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 5, reg.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 6, b.open_date.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(ins, 7, b.deadline_date.c_str(), -1, SQLITE_TRANSIENT);
        BindOptDouble(ins, 8, b.base_amount);
        BindOptDouble(ins, 9, b.estimate_price);
        sqlite3_bind_text(ins, 10, stamp.c_str(), -1, SQLITE_TRANSIENT);
        if (sqlite3_step(ins) != SQLITE_DONE) {
            sqlite3_finalize(ins);
            sqlite3_close(db);
            return false;
        }
    }
    sqlite3_finalize(ins);
    const bool ok = ExecSql(db, "COMMIT;", nullptr);
    sqlite3_close(db);
    return ok;
}

std::vector<UpcomingBidNotice> LoadUpcomingBidsFromStore(
    const std::filesystem::path& db_path,
    const std::string& region_filter) {
    std::vector<UpcomingBidNotice> out;
    sqlite3* db = nullptr;
    if (sqlite3_open_v2(PathUtf8(db_path).c_str(), &db, SQLITE_OPEN_READONLY, nullptr) != SQLITE_OK) {
        if (db) {
            sqlite3_close(db);
        }
        return out;
    }
    sqlite3_stmt* stmt = nullptr;
    const char* sql =
        "SELECT source, notice_number, notice_name, ordering_agency, region, "
        "open_date, deadline_date, base_amount, estimate_price "
        "FROM upcoming_bids ORDER BY open_date DESC, notice_number DESC;";
    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) == SQLITE_OK) {
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            UpcomingBidNotice b;
            b.source = ColumnText(stmt, 0);
            b.notice_number = ColumnText(stmt, 1);
            b.notice_name = ColumnText(stmt, 2);
            b.agency = ColumnText(stmt, 3);
            b.region = ColumnText(stmt, 4);
            b.open_date = ColumnText(stmt, 5);
            b.deadline_date = ColumnText(stmt, 6);
            b.base_amount = ColumnDouble(stmt, 7);
            b.estimate_price = ColumnDouble(stmt, 8);
            if (!region_filter.empty() && b.region != region_filter) {
                continue;
            }
            out.push_back(std::move(b));
        }
        sqlite3_finalize(stmt);
    }
    sqlite3_close(db);
    return out;
}

}  // namespace nara
