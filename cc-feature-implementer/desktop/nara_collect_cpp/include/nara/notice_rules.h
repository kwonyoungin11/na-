#pragma once

#include "nara/igunsul_convert.h"

#include <string>
#include <vector>

namespace nara {

bool IsExcludedNoticeName(const std::string& notice_name);
bool IsDonggukCompany(const ParticipantRow& row);

std::vector<ParticipantRow> DropExcludedNoticeNames(const std::vector<ParticipantRow>& rows);
std::vector<ParticipantRow> DropNoticesWithTooFewCompanies(const std::vector<ParticipantRow>& rows);
std::vector<ParticipantRow> KeepDonggukParticipatingNotices(const std::vector<ParticipantRow>& rows);
std::vector<ParticipantRow> ApplyPersistRules(const std::vector<ParticipantRow>& rows);
std::vector<ParticipantRow> ApplyChartRules(const std::vector<ParticipantRow>& rows);

}  // namespace nara
