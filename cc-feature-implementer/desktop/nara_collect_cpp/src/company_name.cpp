#include "nara/igunsul_convert.h"

#include <algorithm>
#include <cctype>
#include <string>
#include <vector>

namespace nara {
namespace {

std::string TrimCopy(std::string value) {
    auto not_space = [](unsigned char ch) { return !std::isspace(ch); };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), not_space));
    value.erase(std::find_if(value.rbegin(), value.rend(), not_space).base(), value.end());
    return value;
}

std::string ReplaceAllCopy(std::string value, const std::string& from, const std::string& to) {
    size_t pos = 0;
    while ((pos = value.find(from, pos)) != std::string::npos) {
        value.replace(pos, from.size(), to);
        pos += to.size();
    }
    return value;
}

}  // namespace

std::string CanonicalizeCompany(const std::string& name) {
    auto output = TrimCopy(name);
    const std::vector<std::string> removals = {
        "(재)", "(재단)", "재단법인", "재단", "(주)", "주식회사", "(사)", "사단법인"};
    for (const auto& token : removals) {
        output = ReplaceAllCopy(output, token, "");
    }
    output = ReplaceAllCopy(output, "문화유산연구원", "문화재연구원");
    output = TrimCopy(ReplaceAllCopy(output, "  ", " "));
    return output;
}

std::string BuildShortLabel(const std::string& name) {
    auto compact = CanonicalizeCompany(name);
    const std::vector<std::string> suffixes = {
        "문화재연구원", "문화유산연구원", "문화연구원", "연구원"};
    for (const auto& suffix : suffixes) {
        if (compact.size() > suffix.size() && compact.rfind(suffix) == compact.size() - suffix.size()) {
            compact.erase(compact.size() - suffix.size());
            break;
        }
    }
    compact = TrimCopy(compact);
    if (compact.empty()) {
        return {};
    }
    size_t i = 0;
    int taken = 0;
    while (i < compact.size() && taken < 2) {
        const unsigned char lead = static_cast<unsigned char>(compact[i]);
        size_t n = 1;
        if ((lead & 0x80) == 0) {
            n = 1;
        } else if ((lead & 0xE0) == 0xC0) {
            n = 2;
        } else if ((lead & 0xF0) == 0xE0) {
            n = 3;
        } else if ((lead & 0xF8) == 0xF0) {
            n = 4;
        }
        i += n;
        ++taken;
    }
    return compact.substr(0, i);
}

}  // namespace nara
