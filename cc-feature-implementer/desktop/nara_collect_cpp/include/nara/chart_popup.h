#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "nara/igunsul_convert.h"

#include <string>
#include <vector>
#include <windows.h>

namespace nara {

using ChartRegionHandler = void (*)(const std::string& region);

void SetChartRegionHandler(ChartRegionHandler handler);
bool ChartWindowIsOpen();
void RefreshChartSnapshot(
    const std::vector<ParticipantRow>& snapshot,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region);

void OpenChartWindow(
    HINSTANCE instance,
    HWND owner,
    const std::vector<ParticipantRow>& snapshot,
    const std::string& start_date,
    const std::string& end_date,
    const std::string& region);

}  // namespace nara
