#include "nara/igunsul_convert.h"

#include <fstream>
#include <iostream>
#include <string>

int main(int argc, char** argv) {
    std::filesystem::path dir;
    std::filesystem::path out;
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        if (arg == "--dir" && i + 1 < argc) {
            dir = argv[++i];
        } else if (arg == "--out" && i + 1 < argc) {
            out = argv[++i];
        }
    }
    if (dir.empty()) {
        std::cerr << "usage: nara_igunsul_ingest --dir <fixture-dir> --out <jsonl>\n";
        return 2;
    }
    try {
        const auto rows = nara::ConvertDirectory(dir);
        std::ostream* dest = &std::cout;
        std::ofstream file;
        if (!out.empty()) {
            file.open(out, std::ios::binary);
            if (!file) {
                std::cerr << "cannot write " << out.string() << "\n";
                return 1;
            }
            dest = &file;
        }
        for (const auto& row : rows) {
            (*dest) << nara::ToJsonLine(row) << "\n";
        }
        dest->flush();
        std::cerr << "rows=" << rows.size() << "\n";
        return 0;
    } catch (const std::exception& ex) {
        std::cerr << "error: " << ex.what() << "\n";
        return 1;
    }
}
