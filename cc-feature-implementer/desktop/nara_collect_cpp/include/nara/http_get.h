#pragma once

#include <string>

namespace nara {

std::string HttpGet(const std::string& host, const std::string& path_and_query);

}  // namespace nara
