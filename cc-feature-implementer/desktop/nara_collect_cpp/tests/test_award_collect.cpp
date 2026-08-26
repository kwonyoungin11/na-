#include "nara/award_collect.h"

#include <cmath>
#include <iostream>
#include <string>
#include <vector>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    const auto from_bid = nara::FromBidRateVsLowerLimit(86.447, 86.745);
    if (!from_bid || std::fabs(*from_bid - 99.65647) > 0.001) {
        std::cerr << "FAIL: FromBidRateVsLowerLimit " << (from_bid ? std::to_string(*from_bid) : "null") << "\n";
        return 1;
    }

    const auto rate = nara::ComputeBaseAdjustRate(123403300.0, 143400000.0, 86.245);
    if (!rate || std::fabs(*rate - 99.78004) > 0.02) {
        std::cerr << "FAIL: 기초대비사정률 expected ~99.78 got "
                  << (rate ? std::to_string(*rate) : "null") << "\n";
        return 1;
    }

    nara::AwardNotice notice;
    notice.notice_number = "R26BK01673928-000";
    notice.notice_name = "창녕-고암 국도건설공사 문화재 정밀발굴조사용역";
    notice.region = "전국";
    notice.companies = {
        {"춘추문화재연구원", 1, 99.78004},
        {"전북문화재연구원", 2, 99.78123},
        {"경강문화재연구원", 3, 99.78364},
        {"한울문화재연구원", 4, 99.78686},
        {"고대문화재연구원", 5, 99.79235},
        {"동국문화재연구원", 62, 99.39721},
    };
    if (!nara::PassesCollectRules(notice)) {
        return Fail("6+ 문화재연구원 전국 발굴조사 should pass");
    }

    nara::AwardNotice impact = notice;
    impact.notice_name = "금강 공주권역 국가유산영향진단 용역";
    if (nara::PassesCollectRules(impact)) {
        return Fail("영향진단 must be rejected");
    }

    nara::AwardNotice tiny = notice;
    tiny.companies.resize(5);
    if (nara::PassesCollectRules(tiny)) {
        return Fail("5 companies must be rejected");
    }

    const auto months = nara::CollectMonths("2026-02", "2026-08");
    if (months.size() != 7) {
        std::cerr << "FAIL: Feb-Aug inclusive should be 7 months, got " << months.size() << "\n";
        return 1;
    }
    const auto year = nara::CollectMonths("2025-08", "2026-08");
    if (year.size() != 13) {
        std::cerr << "FAIL: 1-year inclusive Aug-Aug should be 13 months, got " << year.size() << "\n";
        return 1;
    }
    if (months.front() != "2026-02" || months.back() != "2026-08") {
        return Fail("month range bounds");
    }
    const auto windows = nara::CollectThreeMonthWindows("2025-08", "2026-08");
    if (windows.size() != 5) {
        std::cerr << "FAIL: 1년은 3개월 창 5개여야 한다 got " << windows.size() << "\n";
        return 1;
    }
    if (windows[0].start_month != "2025-08" || windows[0].end_month != "2025-10") {
        return Fail("first window Aug-Oct");
    }
    if (windows.back().start_month != "2026-08" || windows.back().end_month != "2026-08") {
        return Fail("last window is the leftover month");
    }
    const auto budget_only = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"bidNtceNo\":\"R26BK1\",\"asignBdgtAmt\":\"76769000\"}]}}}");
    if (nara::ParseBaseAmount(budget_only)) {
        return Fail("asignBdgtAmt is not 기초예가");
    }

    const auto body = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"bidNtceNo\":\"R26BK1\",\"bidNtceNm\":\"발굴조사\",\"opengDt\":\"2026-03-01 11:00:00\"}]}}}");
    const auto items = nara::ParseJsonObjectArray(body, "items");
    if (items.size() != 1 || items[0].find("R26BK1") == std::string::npos) {
        return Fail("ParseJsonObjectArray should extract items objects");
    }

    const auto detail = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"opengRank\":\"1\",\"prcbdrNm\":\"재단법인 해원문화유산연구원\",\"bidprcAmt\":\"67547000\",\"bidprcrt\":\"87.745\"}]}}}");
    const auto corps = nara::ParseResponseItems(detail);
    if (corps.size() != 1) {
        return Fail("ParseResponseItems should read items array");
    }
    if (nara::JsonNumber(corps[0], "opengRank") != 1) {
        return Fail("opengRank 1 should parse");
    }
    if (!nara::JsonNumber(corps[0], "bidprcrt") || std::fabs(*nara::JsonNumber(corps[0], "bidprcrt") - 87.745) > 0.001) {
        return Fail("bidprcrt 87.745 should parse");
    }

    const auto notice_json = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"bidNtceNo\":\"R25BK00994054\",\"sucsfbidLwltRate\":\"87.745\"}]}}}");
    const auto lower = nara::ParseLowerRate(notice_json);
    if (!lower || std::fabs(*lower - 87.745) > 0.001) {
        return Fail("ParseLowerRate should read sucsfbidLwltRate");
    }

    const auto base_json = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"bidNtceNo\":\"R26BK1\",\"bssAmt\":\"143400000\"}]}}}");
    const auto parsed_base = nara::ParseBaseAmount(base_json);
    if (!parsed_base || std::fabs(*parsed_base - 143400000.0) > 0.5) {
        return Fail("ParseBaseAmount should read bssAmt");
    }

    nara::AwardNotice filled;
    filled.lower_rate = lower;
    nara::ApplyDetailCompanies(filled, detail);
    if (filled.companies.size() != 1 || filled.companies[0].rank != 1) {
        return Fail("ApplyDetailCompanies should set rank");
    }
    if (filled.companies[0].adjust_rate) {
        return Fail("예가대비 bidprcrt/하한만으로는 adjust_rate를 100.0으로 쓰지 않는다");
    }

    nara::AwardNotice with_base;
    with_base.base_amount = 143400000.0;
    with_base.lower_rate = 86.245;
    const auto detail_with_amt = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"opengRank\":\"1\",\"prcbdrNm\":\"춘추문화재연구원\","
        "\"bidprcAmt\":\"123403300\",\"bidprcrt\":\"87.745\"}]}}}");
    nara::ApplyDetailCompanies(with_base, detail_with_amt);
    if (with_base.companies.size() != 1 || !with_base.companies[0].adjust_rate
        || std::fabs(*with_base.companies[0].adjust_rate - 99.78004) > 0.02) {
        std::cerr << "FAIL: 기초예가 공식 adjust_rate "
                  << (with_base.companies[0].adjust_rate ? std::to_string(*with_base.companies[0].adjust_rate)
                                                         : "null")
                  << "\n";
        return 1;
    }

    std::cerr << "ok award collect rules\n";
    return 0;
}
