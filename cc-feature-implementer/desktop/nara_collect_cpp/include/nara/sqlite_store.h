#pragma once

#include "nara/igunsul_convert.h"

#include <filesystem>
#include <string>
#include <vector>

namespace nara {

struct JsonlMergeResult {
    bool ok = false;
    int notices = 0;
    int rows = 0;
    std::string error;
};

std::vector<ParticipantRow> LoadParticipants(const std::filesystem::path& db_path);
std::string DefaultAppDir();
std::string DefaultDashboardDbPath();
std::string DefaultAwardJsonlPath();
std::string DefaultServiceKeyEnvPath();
std::string WorkspaceDashboardDbPath();
std::string AwardCollectCommandLine(
    const std::string& exe_path,
    const std::string& start_month,
    const std::string& end_month,
    const std::string& jsonl_path,
    const std::string& env_path);
std::string AwardCollectGapCommandLine(
    const std::string& exe_path,
    const std::string& from_date,
    const std::string& to_date,
    const std::string& jsonl_path,
    const std::string& env_path);
std::string MaxParticipantOpenDay(const std::filesystem::path& db_path);
bool EnsureParticipantsSchema(const std::filesystem::path& db_path);
void EnsurePortableDatabase();
JsonlMergeResult MergeJsonlIntoStore(
    const std::filesystem::path& jsonl_path,
    const std::filesystem::path& db_path);

}  // namespace nara
