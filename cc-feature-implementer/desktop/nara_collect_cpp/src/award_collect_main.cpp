#include "nara/award_collect.h"
#include "nara/http_get.h"

#include <cctype>
#include <chrono>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <map>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

namespace {

std::string UrlEncode(const std::string& value) {
    std::ostringstream out;
    out << std::hex;
    for (unsigned char ch : value) {
        if (std::isalnum(ch) || ch == '-' || ch == '_' || ch == '.' || ch == '~') {
            out << static_cast<char>(ch);
        } else {
            out << '%' << std::uppercase << std::setw(2) << std::setfill('0') << static_cast<int>(ch)
                << std::nouppercase;
        }
    }
    return out.str();
}

std::string JsonEscape(const std::string& raw) {
    std::string out;
    out.reserve(raw.size());
    for (unsigned char ch : raw) {
        switch (ch) {
            case '"':
                out += "\\\"";
                break;
            case '\\':
                out += "\\\\";
                break;
            case '\n':
                out += "\\n";
                break;
            case '\r':
                out += "\\r";
                break;
            case '\t':
                out += "\\t";
                break;
            default:
                out += static_cast<char>(ch);
                break;
        }
    }
    return out;
}

std::string LoadServiceKey(const std::string& env_path) {
    std::ifstream in(env_path);
    std::string line;
    while (std::getline(in, line)) {
        if (!line.empty() && line.back() == '\r') {
            line.pop_back();
        }
        if (!line.empty() && static_cast<unsigned char>(line[0]) == 0xEF) {
            line.erase(0, line.size() >= 3 ? 3 : 0);
        }
        const auto eq = line.find('=');
        if (eq == std::string::npos) {
            continue;
        }
        const auto key = line.substr(0, eq);
        if (key.find("SERVICE_KEY") != std::string::npos) {
            return line.substr(eq + 1);
        }
    }
    return {};
}

std::string MonthStart(const std::string& month) {
    return month.substr(0, 4) + month.substr(5, 2) + "010000";
}

std::string MonthEnd(const std::string& month) {
    const int y = std::stoi(month.substr(0, 4));
    const int m = std::stoi(month.substr(5, 2));
    const int days[12] = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
    int d = days[m - 1];
    if (m == 2 && ((y % 4 == 0 && y % 100 != 0) || y % 400 == 0)) {
        d = 29;
    }
    std::ostringstream out;
    out << month.substr(0, 4) << month.substr(5, 2);
    if (d < 10) {
        out << "0";
    }
    out << d << "2359";
    return out.str();
}

}  // namespace

int main(int argc, char** argv) {
    std::string start_month = "2026-02";
    std::string end_month = "2026-08";
    std::string env_path = ".env";
    std::string out_path = "cpp_award_collect.jsonl";
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        if (arg == "--start-month" && i + 1 < argc) {
            start_month = argv[++i];
        } else if (arg == "--end-month" && i + 1 < argc) {
            end_month = argv[++i];
        } else if (arg == "--env" && i + 1 < argc) {
            env_path = argv[++i];
        } else if (arg == "--out" && i + 1 < argc) {
            out_path = argv[++i];
        }
    }

    const auto key = LoadServiceKey(env_path);
    if (key.empty()) {
        std::cerr << "missing SERVICE_KEY\n";
        return 2;
    }

    const auto months = nara::CollectMonths(start_month, end_month);
    std::cerr << "cpp collect months=" << months.size() << " " << start_month << "~" << end_month
              << " (월 단위 OpenAPI)\n";
    const std::vector<std::string> keywords = {
        "발굴조사", "정밀발굴조사", "시굴조사", "지표조사", "표본조사", "문화재발굴조사"};

    std::ofstream out(out_path, std::ios::binary);
    if (!out) {
        std::cerr << "cannot write " << out_path << "\n";
        return 1;
    }

    int kept = 0;
    int notices = 0;
    struct BidNoticeMeta {
        std::optional<double> lower_rate;
        std::optional<double> base_amount;
    };
    std::map<std::string, BidNoticeMeta> bid_notice_cache;
    for (const auto& month : months) {
        for (const auto& keyword : keywords) {
          for (int page = 1; page <= 20; ++page) {
            std::ostringstream path;
            path << "/1230000/as/ScsbidInfoService/getOpengResultListInfoServcPPSSrch"
                 << "?inqryDiv=1&inqryBgnDt=" << MonthStart(month)
                 << "&inqryEndDt=" << MonthEnd(month)
                 << "&type=json&numOfRows=100&pageNo=" << page
                 << "&bidNtceNm=" << UrlEncode(keyword)
                 << "&ServiceKey=" << UrlEncode(key);
            std::string body;
            try {
                body = nara::HttpGet("apis.data.go.kr", path.str());
            } catch (const std::exception& ex) {
                std::cerr << "WARN " << month << " " << keyword << " p" << page << " " << ex.what() << "\n";
                break;
            }
            auto items = nara::ParseJsonObjectArray(body, "items");
            if (items.empty()) {
                items = nara::ParseJsonObjectArray(body, "item");
            }
            const auto result_msg = nara::JsonString(body, "resultMsg");
            std::cerr << month << " " << keyword << " p" << page << " items=" << items.size()
                      << " bytes=" << body.size() << " msg=" << result_msg << "\n";
            if (items.empty()) {
                if (!body.empty() && page == 1) {
                    std::cerr << "  body=" << body.substr(0, 180) << "\n";
                }
                break;
            }
            for (const auto& item : items) {
                nara::AwardNotice notice;
                notice.notice_number = nara::JsonString(item, "bidNtceNo");
                if (notice.notice_number.empty()) {
                    continue;
                }
                const auto ord = nara::JsonString(item, "bidNtceOrd");
                if (!ord.empty() && notice.notice_number.find('-') == std::string::npos) {
                    notice.notice_number += "-" + ord;
                }
                notice.notice_name = nara::JsonString(item, "bidNtceNm");
                notice.agency = nara::JsonString(item, "dminsttNm");
                notice.region = nara::JsonString(item, "rgnLmtBidLocplcJdgmBssNm");
                if (notice.region.empty()) {
                    notice.region = "전국";
                }
                notice.open_date = nara::JsonString(item, "opengDt");
                notice.base_amount = nara::JsonNumber(item, "bssamt");
                if (!notice.base_amount) {
                    notice.base_amount = nara::JsonNumber(item, "bssAmt");
                }
                notice.lower_rate = nara::JsonNumber(item, "sucsfbidLwltRate");
                ++notices;

                const auto ntce_no = nara::JsonString(item, "bidNtceNo");
                if (!notice.lower_rate || !notice.base_amount) {
                    const auto cached = bid_notice_cache.find(ntce_no);
                    if (cached != bid_notice_cache.end()) {
                        if (!notice.lower_rate) {
                            notice.lower_rate = cached->second.lower_rate;
                        }
                        if (!notice.base_amount) {
                            notice.base_amount = cached->second.base_amount;
                        }
                    } else {
                        std::ostringstream bid_notice;
                        bid_notice << "/1230000/ad/BidPublicInfoService/getBidPblancListInfoServc"
                                   << "?inqryDiv=2&type=json&numOfRows=5&pageNo=1"
                                   << "&bidNtceNo=" << UrlEncode(ntce_no)
                                   << "&ServiceKey=" << UrlEncode(key);
                        try {
                            const auto body = nara::HttpGet("apis.data.go.kr", bid_notice.str());
                            if (!notice.lower_rate) {
                                notice.lower_rate = nara::ParseLowerRate(body);
                            }
                            if (!notice.base_amount) {
                                notice.base_amount = nara::ParseBaseAmount(body);
                            }
                        } catch (const std::exception& ex) {
                            std::cerr << "WARN bid-notice " << notice.notice_number << " " << ex.what() << "\n";
                        }
                        bid_notice_cache[ntce_no] = BidNoticeMeta{notice.lower_rate, notice.base_amount};
                    }
                }

                if (!notice.base_amount) {
                    std::ostringstream prepar;
                    prepar << "/1230000/as/ScsbidInfoService/getOpengResultListInfoServcPreparPcDetail"
                           << "?type=json&numOfRows=10&pageNo=1"
                           << "&bidNtceNo=" << UrlEncode(ntce_no)
                           << "&bidNtceOrd=" << UrlEncode(ord.empty() ? "000" : ord)
                           << "&bidClsfcNo="
                           << UrlEncode(nara::JsonString(item, "bidClsfcNo").empty() ? "0"
                                                                                    : nara::JsonString(item, "bidClsfcNo"))
                           << "&rbidNo="
                           << UrlEncode(nara::JsonString(item, "rbidNo").empty() ? "000"
                                                                                : nara::JsonString(item, "rbidNo"))
                           << "&ServiceKey=" << UrlEncode(key);
                    try {
                        notice.base_amount = nara::ParseBaseAmount(nara::HttpGet("apis.data.go.kr", prepar.str()));
                        bid_notice_cache[ntce_no].base_amount = notice.base_amount;
                    } catch (const std::exception& ex) {
                        std::cerr << "WARN prepar " << notice.notice_number << " " << ex.what() << "\n";
                    }
                    std::this_thread::sleep_for(std::chrono::milliseconds(80));
                }

                std::ostringstream detail;
                detail << "/1230000/as/ScsbidInfoService/getOpengResultListInfoOpengCompt"
                       << "?type=json&numOfRows=100&pageNo=1"
                       << "&bidNtceNo=" << UrlEncode(nara::JsonString(item, "bidNtceNo"))
                       << "&bidNtceOrd=" << UrlEncode(ord.empty() ? "000" : ord)
                       << "&bidClsfcNo=" << UrlEncode(nara::JsonString(item, "bidClsfcNo").empty() ? "0" : nara::JsonString(item, "bidClsfcNo"))
                       << "&rbidNo=" << UrlEncode(nara::JsonString(item, "rbidNo").empty() ? "000" : nara::JsonString(item, "rbidNo"))
                       << "&ServiceKey=" << UrlEncode(key);
                try {
                    nara::ApplyDetailCompanies(notice, nara::HttpGet("apis.data.go.kr", detail.str()));
                } catch (const std::exception& ex) {
                    std::cerr << "WARN detail " << notice.notice_number << " " << ex.what() << "\n";
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(80));

                if (!nara::PassesCollectRules(notice)) {
                    continue;
                }
                for (const auto& company : notice.companies) {
                    out << "{\"notice_number\":\"" << JsonEscape(notice.notice_number)
                        << "\",\"notice_name\":\"" << JsonEscape(notice.notice_name)
                        << "\",\"ordering_agency\":\"" << JsonEscape(notice.agency)
                        << "\",\"region\":\"" << JsonEscape(notice.region)
                        << "\",\"open_date\":\"" << JsonEscape(notice.open_date)
                        << "\",\"rank\":" << company.rank
                        << ",\"company_name\":\"" << JsonEscape(company.name) << "\"";
                    if (company.adjust_rate) {
                        out << ",\"adjust_rate\":" << *company.adjust_rate;
                    }
                    if (company.bid_amount) {
                        out << ",\"bid_amount\":" << *company.bid_amount;
                    }
                    if (notice.base_amount) {
                        out << ",\"base_amount\":" << *notice.base_amount;
                    }
                    if (notice.lower_rate) {
                        out << ",\"lower_rate\":" << *notice.lower_rate;
                    }
                    out << "}\n";
                    ++kept;
                }
            }
            if (items.size() < 100) {
                break;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(80));
          }
        }
    }
    out.flush();
    std::cerr << "cpp collect notices_seen=" << notices << " rows_kept=" << kept << " out=" << out_path << "\n";
    return 0;
}
