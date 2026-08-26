#include "nara/xlsx_store.h"

#include "miniz.h"

#include <cctype>
#include <cstring>
#include <fstream>
#include <stdexcept>
#include <vector>

namespace nara {
namespace {

std::vector<unsigned char> ReadAll(const std::filesystem::path& path) {
    std::ifstream in(path, std::ios::binary);
    if (!in) {
        throw std::runtime_error("cannot open " + path.string());
    }
    return {std::istreambuf_iterator<char>(in), std::istreambuf_iterator<char>()};
}

std::string DecodeXml(const std::string& text) {
    std::string out;
    out.reserve(text.size());
    for (size_t i = 0; i < text.size(); ++i) {
        if (text[i] == '&') {
            if (text.compare(i, 4, "&lt;") == 0) {
                out.push_back('<');
                i += 3;
            } else if (text.compare(i, 4, "&gt;") == 0) {
                out.push_back('>');
                i += 3;
            } else if (text.compare(i, 5, "&amp;") == 0) {
                out.push_back('&');
                i += 4;
            } else if (text.compare(i, 6, "&quot;") == 0) {
                out.push_back('"');
                i += 5;
            } else if (text.compare(i, 6, "&apos;") == 0) {
                out.push_back('\'');
                i += 5;
            } else {
                out.push_back(text[i]);
            }
        } else {
            out.push_back(text[i]);
        }
    }
    return out;
}

void ParseRef(const std::string& ref, int& col, int& row) {
    col = 0;
    row = 0;
    size_t i = 0;
    while (i < ref.size() && std::isalpha(static_cast<unsigned char>(ref[i]))) {
        col = col * 26 + (std::toupper(static_cast<unsigned char>(ref[i])) - 'A' + 1);
        ++i;
    }
    col -= 1;
    row = std::stoi(ref.substr(i)) - 1;
}

std::string ExtractAttr(const std::string& attrs, const std::string& key) {
    const std::string needle = key + "=\"";
    const auto pos = attrs.find(needle);
    if (pos == std::string::npos) {
        return {};
    }
    const auto start = pos + needle.size();
    const auto end = attrs.find('"', start);
    if (end == std::string::npos) {
        return {};
    }
    return attrs.substr(start, end - start);
}

std::string ExtractTagText(const std::string& inner, const std::string& tag) {
    const std::string open = "<" + tag;
    auto pos = inner.find(open);
    if (pos == std::string::npos) {
        return {};
    }
    pos = inner.find('>', pos);
    if (pos == std::string::npos) {
        return {};
    }
    const auto end = inner.find("</" + tag + ">", pos);
    if (end == std::string::npos) {
        return {};
    }
    return DecodeXml(inner.substr(pos + 1, end - pos - 1));
}

std::vector<std::string> ParseSharedStrings(const std::string& xml) {
    std::vector<std::string> strings;
    size_t pos = 0;
    while (true) {
        auto si = xml.find("<si", pos);
        if (si == std::string::npos) {
            break;
        }
        auto si_end = xml.find("</si>", si);
        if (si_end == std::string::npos) {
            break;
        }
        const auto block = xml.substr(si, si_end - si);
        std::string text;
        size_t tpos = 0;
        while (true) {
            auto tstart = block.find("<t", tpos);
            if (tstart == std::string::npos) {
                break;
            }
            auto gt = block.find('>', tstart);
            if (gt == std::string::npos) {
                break;
            }
            auto tend = block.find("</t>", gt);
            if (tend == std::string::npos) {
                break;
            }
            text += DecodeXml(block.substr(gt + 1, tend - gt - 1));
            tpos = tend + 4;
        }
        strings.push_back(text);
        pos = si_end + 5;
    }
    return strings;
}

Sheet ParseSheetXml(const std::string& xml, const std::vector<std::string>& shared) {
    Sheet sheet;
    size_t pos = 0;
    while (true) {
        auto start = xml.find("<c ", pos);
        if (start == std::string::npos) {
            start = xml.find("<c>", pos);
        }
        if (start == std::string::npos) {
            break;
        }
        auto gt = xml.find('>', start);
        if (gt == std::string::npos) {
            break;
        }
        std::string attrs = xml.substr(start, gt - start);
        std::string inner;
        if (xml[gt - 1] == '/') {
            pos = gt + 1;
        } else {
            auto close = xml.find("</c>", gt);
            if (close == std::string::npos) {
                break;
            }
            inner = xml.substr(gt + 1, close - gt - 1);
            pos = close + 4;
        }
        const auto ref = ExtractAttr(attrs, "r");
        if (ref.empty()) {
            continue;
        }
        int col = 0;
        int row = 0;
        ParseRef(ref, col, row);
        if (row < 0 || col < 0) {
            continue;
        }
        const auto type = ExtractAttr(attrs, "t");
        auto value = ExtractTagText(inner, "t");
        if (value.empty()) {
            value = ExtractTagText(inner, "v");
        }
        if (type == "s" && !value.empty() && !shared.empty()) {
            try {
                const int index = std::stoi(value);
                if (index >= 0 && index < static_cast<int>(shared.size())) {
                    value = shared[index];
                }
            } catch (...) {
            }
        }
        if (static_cast<int>(sheet.cells.size()) <= row) {
            sheet.cells.resize(row + 1);
        }
        if (static_cast<int>(sheet.cells[row].size()) <= col) {
            sheet.cells[row].resize(col + 1);
        }
        sheet.cells[row][col] = value;
    }
    return sheet;
}

std::string ExtractZipFile(mz_zip_archive& zip, const char* name) {
    size_t size = 0;
    void* data = mz_zip_reader_extract_file_to_heap(&zip, name, &size, 0);
    if (!data) {
        return {};
    }
    std::string out(static_cast<char*>(data), size);
    mz_free(data);
    return out;
}

std::string FindSheetXml(mz_zip_archive& zip) {
    auto xml = ExtractZipFile(zip, "xl/worksheets/sheet1.xml");
    if (!xml.empty()) {
        return xml;
    }
    const mz_uint count = mz_zip_reader_get_num_files(&zip);
    for (mz_uint i = 0; i < count; ++i) {
        mz_zip_archive_file_stat stat;
        if (!mz_zip_reader_file_stat(&zip, i, &stat) || stat.m_is_directory) {
            continue;
        }
        const std::string name = stat.m_filename;
        if (name.find("xl/worksheets/sheet") != std::string::npos && name.size() >= 4 &&
            name.compare(name.size() - 4, 4, ".xml") == 0) {
            size_t size = 0;
            void* data = mz_zip_reader_extract_to_heap(&zip, i, &size, 0);
            if (!data) {
                continue;
            }
            std::string out(static_cast<char*>(data), size);
            mz_free(data);
            return out;
        }
    }
    return {};
}

}  // namespace

std::string Sheet::at(int row, int col) const {
    if (row < 0 || col < 0 || row >= static_cast<int>(cells.size())) {
        return {};
    }
    if (col >= static_cast<int>(cells[row].size())) {
        return {};
    }
    return cells[row][col];
}

Sheet ReadFirstSheet(const std::filesystem::path& xlsx_path) {
    const auto bytes = ReadAll(xlsx_path);
    mz_zip_archive zip;
    std::memset(&zip, 0, sizeof(zip));
    if (!mz_zip_reader_init_mem(&zip, bytes.data(), bytes.size(), 0)) {
        throw std::runtime_error("not a zip/xlsx: " + xlsx_path.string());
    }
    const auto shared_xml = ExtractZipFile(zip, "xl/sharedStrings.xml");
    const auto sheet_xml = FindSheetXml(zip);
    mz_zip_reader_end(&zip);
    if (sheet_xml.empty()) {
        throw std::runtime_error("worksheet xml not found: " + xlsx_path.string());
    }
    const auto shared = shared_xml.empty() ? std::vector<std::string>{} : ParseSharedStrings(shared_xml);
    return ParseSheetXml(sheet_xml, shared);
}

}  // namespace nara
