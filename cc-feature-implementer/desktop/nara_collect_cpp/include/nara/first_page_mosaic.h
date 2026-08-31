#pragma once

#include "nara/award_table.h"
#include "nara/dongguk_band.h"

#include <string>
#include <vector>
#include <windows.h>
#include <gdiplus.h>

namespace nara {

struct MosaicPaintIn {
    int w = 0;
    int h = 0;
    const std::vector<AwardNoticeRow>* table = nullptr;
    const BandResult* band = nullptr;
    const std::vector<ParticipantRow>* snapshot = nullptr;
    std::string status;
    std::string region_token;
    int selected = 0;
    int* scroll = nullptr;
    RECT* table_rect = nullptr;
};

void PaintMosaicFirstPage(Gdiplus::Graphics& g, MosaicPaintIn& in);

}  // namespace nara
