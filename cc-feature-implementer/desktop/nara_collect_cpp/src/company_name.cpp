#include "nara/igunsul_convert.h"

#include <algorithm>
#include <cctype>
#include <map>
#include <set>
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

size_t Utf8CharSize(unsigned char lead) {
    if ((lead & 0x80) == 0) {
        return 1;
    }
    if ((lead & 0xE0) == 0xC0) {
        return 2;
    }
    if ((lead & 0xF0) == 0xE0) {
        return 3;
    }
    if ((lead & 0xF8) == 0xF0) {
        return 4;
    }
    return 1;
}

std::vector<std::string> Utf8Chars(const std::string& text) {
    std::vector<std::string> chars;
    size_t i = 0;
    while (i < text.size()) {
        size_t n = Utf8CharSize(static_cast<unsigned char>(text[i]));
        if (i + n > text.size()) {
            n = text.size() - i;
        }
        chars.push_back(text.substr(i, n));
        i += n;
    }
    return chars;
}

std::string JoinChars(const std::vector<std::string>& chars, size_t count) {
    std::string out;
    const size_t n = (std::min)(count, chars.size());
    for (size_t i = 0; i < n; ++i) {
        out += chars[i];
    }
    return out;
}

bool StartsWithIgnoreAsciiCase(const std::string& value, const std::string& prefix) {
    if (prefix.size() > value.size()) {
        return false;
    }
    for (size_t i = 0; i < prefix.size(); ++i) {
        const unsigned char a = static_cast<unsigned char>(value[i]);
        const unsigned char b = static_cast<unsigned char>(prefix[i]);
        if (std::tolower(a) != std::tolower(b)) {
            return false;
        }
    }
    return true;
}

std::string HangulOnly(const std::string& text) {
    std::string out;
    size_t i = 0;
    while (i < text.size()) {
        const size_t n = Utf8CharSize(static_cast<unsigned char>(text[i]));
        uint32_t cp = static_cast<unsigned char>(text[i]);
        if (n == 2 && i + 1 < text.size()) {
            cp = ((text[i] & 0x1F) << 6) | (text[i + 1] & 0x3F);
        } else if (n == 3 && i + 2 < text.size()) {
            cp = ((text[i] & 0x0F) << 12) | ((text[i + 1] & 0x3F) << 6) | (text[i + 2] & 0x3F);
        } else if (n == 4 && i + 3 < text.size()) {
            cp = ((text[i] & 0x07) << 18) | ((text[i + 1] & 0x3F) << 12) | ((text[i + 2] & 0x3F) << 6) |
                 (text[i + 3] & 0x3F);
        }
        if (cp >= 0xAC00 && cp <= 0xD7A3) {
            out.append(text, i, n);
        }
        i += n;
    }
    return out;
}

std::string CompactStem(const std::string& canonical) {
    auto compact = ReplaceAllCopy(canonical, " ", "");
    const std::vector<std::string> suffixes = {
        "문화재연구원",
        "문화유산연구원",
        "문화연구원",
        "문화유산원",
        "연구원",
        "고고학연구소",
        "고고문화연구원",
        "역사문화연구원",
    };
    for (const auto& suffix : suffixes) {
        if (compact.size() > suffix.size() && compact.rfind(suffix) == compact.size() - suffix.size()) {
            compact.erase(compact.size() - suffix.size());
            break;
        }
    }
    const auto hangul = HangulOnly(compact);
    return hangul.empty() ? compact : hangul;
}

std::string CompressedUniqueLabel(const std::vector<std::string>& chars) {
    if (chars.empty()) {
        return {};
    }
    if (chars.size() <= 2) {
        return JoinChars(chars, chars.size());
    }
    if (chars.size() == 3) {
        return chars[0] + chars[2];
    }
    return chars[0] + chars[2] + chars.back();
}

}  // namespace

std::string CanonicalizeCompany(const std::string& name) {
    auto output = TrimCopy(name);
    const std::vector<std::string> removals = {
        "(재)", "(재단)", "재단법인", "(주)", "주식회사", "㈜", "(사)", "사단법인"};
    for (const auto& token : removals) {
        output = ReplaceAllCopy(output, token, "");
    }
    output = TrimCopy(ReplaceAllCopy(output, "  ", " "));
    if (StartsWithIgnoreAsciiCase(output, "재단 ")) {
        output = TrimCopy(output.substr(std::string("재단 ").size()));
    }
    const auto collapsed = ReplaceAllCopy(output, " ", "");
    if (collapsed == "경북문화재단문화유산원" || collapsed == "경북문화문화유산원" || collapsed == "경북문화"
        || collapsed == "경북문화재단") {
        return "경북문화재단 문화유산원";
    }
    if (collapsed == "경기문화재단경기역사문화유산원" || collapsed == "경기역사문화유산원"
        || collapsed == "경기문화재연구원" || collapsed == "경기문화재단경기문화재연구원") {
        return "경기문화재연구원";
    }
    if (collapsed == "울산연구원문화유산연구실" || collapsed == "울산발전연구원"
        || collapsed == "울산연구원") {
        return "울산연구원";
    }
    if (collapsed == "경남연구원경남학센터" || collapsed == "경남연구원역사문화센터"
        || collapsed == "경남발전연구원" || collapsed == "경남연구원") {
        return "경남연구원";
    }
    if (collapsed == "전남문화재단국가유산연구소" || collapsed == "전라남도문화재단"
        || collapsed == "전라남도문화" || collapsed == "전남문화재단") {
        return "전라남도문화재단";
    }
    if (collapsed == "경상북도문화관광공사문화유산원" || collapsed == "경상북도문화관광공사") {
        return "경상북도문화관광공사";
    }
    if (collapsed == "세종문화유산") {
        return "세종문화유산재단";
    }
    if (collapsed == "세계문화유산연구") {
        return "세계문화유산연구재단";
    }
    if (collapsed.find("한국문화유산연구원") == std::string::npos) {
        output = ReplaceAllCopy(output, "문화유산연구원", "문화재연구원");
    }
    return TrimCopy(ReplaceAllCopy(output, "  ", " "));
}

std::string CompanyIdentity(const ParticipantRow& row) {
    if (!row.company_name.empty()) {
        return CanonicalizeCompany(row.company_name);
    }
    return CanonicalizeCompany(row.normalized_company_name);
}

std::string BuildShortLabel(const std::string& name) {
    auto compact = CompactStem(CanonicalizeCompany(name));
    compact = TrimCopy(compact);
    if (compact.empty()) {
        return {};
    }
    return JoinChars(Utf8Chars(compact), 2);
}

std::map<std::string, std::string> BuildDisplayMap(const std::vector<std::string>& names) {
    std::vector<std::string> canonical_names;
    std::set<std::string> seen;
    for (const auto& name : names) {
        const auto key = CanonicalizeCompany(name);
        if (key.empty() || !seen.insert(key).second) {
            continue;
        }
        canonical_names.push_back(key);
    }

    std::map<std::string, std::string> raw_compact;
    std::map<std::string, std::string> compact;
    for (const auto& key : canonical_names) {
        raw_compact[key] = ReplaceAllCopy(key, " ", "");
        compact[key] = CompactStem(key);
    }

    std::map<std::string, std::string> result;
    for (const auto& key : canonical_names) {
        const auto& stem = compact[key];
        const auto& raw = raw_compact[key];
        if (stem.empty() || raw.empty()) {
            result[key] = {};
            continue;
        }
        const auto raw_chars = Utf8Chars(raw);
        size_t unique_len = (std::min)(static_cast<size_t>(2), raw_chars.size());
        while (unique_len <= raw_chars.size()) {
            const auto prefix = JoinChars(raw_chars, unique_len);
            bool unique = true;
            for (const auto& other : canonical_names) {
                if (other == key) {
                    continue;
                }
                if (StartsWithIgnoreAsciiCase(raw_compact[other], prefix)) {
                    unique = false;
                    break;
                }
            }
            if (unique) {
                break;
            }
            ++unique_len;
        }
        const auto unique_prefix = Utf8Chars(JoinChars(raw_chars, (std::min)(unique_len, raw_chars.size())));
        auto label = CompressedUniqueLabel(unique_prefix);
        if (label.empty()) {
            label = BuildShortLabel(key);
        }
        result[key] = label;
    }
    return result;
}

std::string DisplayLabelFor(const std::string& name, const std::map<std::string, std::string>& labels) {
    const auto key = CanonicalizeCompany(name);
    const auto found = labels.find(key);
    if (found != labels.end() && !found->second.empty()) {
        return found->second;
    }
    return BuildShortLabel(key.empty() ? name : key);
}

}  // namespace nara
