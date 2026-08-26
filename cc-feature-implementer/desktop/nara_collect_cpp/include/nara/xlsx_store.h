#pragma once

#include <filesystem>
#include <string>
#include <vector>

namespace nara {

struct Sheet {
    std::vector<std::vector<std::string>> cells;

    std::string at(int row, int col) const;
};

Sheet ReadFirstSheet(const std::filesystem::path& xlsx_path);

}  // namespace nara
