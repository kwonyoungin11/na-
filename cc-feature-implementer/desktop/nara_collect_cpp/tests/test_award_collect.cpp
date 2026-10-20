#include "nara/award_collect.h"

#include <algorithm>
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

    nara::AwardNotice gyungbuk = notice;
    gyungbuk.region = "경상북도";
    if (!nara::PassesCollectRules(gyungbuk)) {
        return Fail("python 2nd preprocess keeps 경상북도 as 경북");
    }
    nara::AwardNotice jeonbuk = notice;
    jeonbuk.region = "전북";
    if (!nara::PassesCollectRules(jeonbuk)) {
        return Fail("collect keeps all regions; preprocess later keeps 전국/경북 only");
    }
    nara::AwardNotice heritage = notice;
    heritage.companies = {
        {"재단법인 해원문화유산연구원", 1, 99.78},
        {"전북문화유산연구원", 2, 99.79},
        {"경강문화유산연구원", 3, 99.80},
        {"한울문화유산연구원", 4, 99.81},
        {"고대문화유산연구원", 5, 99.82},
        {"동국문화유산연구원", 6, 99.40},
    };
    if (!nara::PassesCollectRules(heritage)) {
        return Fail("python 전처리 is 문화유산연구원 → 문화재연구원 then keep");
    }

    nara::AwardNotice surface = notice;
    surface.notice_name = "지표조사 용역";
    if (!nara::PassesCollectRules(surface)) {
        return Fail("지표조사 용역 + 6 연구원 must pass");
    }
    nara::AwardNotice sample = notice;
    sample.notice_name = "표본조사 용역";
    if (!nara::PassesCollectRules(sample)) {
        return Fail("표본조사 용역 + 6 연구원 must pass");
    }
    nara::AwardNotice heritage_mgmt = notice;
    heritage_mgmt.notice_name = "문화유산 보존관리 용역";
    if (!nara::PassesCollectRules(heritage_mgmt)) {
        return Fail("문화유산 보존관리 용역 (no 발굴) + 6 연구원 must pass");
    }
    if (nara::PassesCollectRules(impact)) {
        return Fail("국가유산영향진단 still false");
    }

    const auto search_keys = nara::CollectSearchKeywords();
    auto has_key = [&](const char* token) {
        return std::find(search_keys.begin(), search_keys.end(), token) != search_keys.end();
    };
    if (!has_key("매장유산") || !has_key("문화재조사") || !has_key("문화유산") || !has_key("발굴조사")
        || !has_key("정밀발굴조사") || !has_key("시굴조사") || !has_key("지표조사") || !has_key("표본조사")
        || !has_key("문화재발굴조사")) {
        return Fail("CollectSearchKeywords must include 매장유산, 문화재조사, 문화유산, and the existing six");
    }
    if (has_key("국가유산")) {
        return Fail("Do not search 국가유산");
    }
    if (nara::ResolveOrderingAgency("", "경주시") != "경주시") {
        return Fail("empty dminsttNm falls back to ntceInsttNm");
    }
    if (nara::ResolveOrderingAgency("대구광역시", "경주시") != "대구광역시") {
        return Fail("non-empty dminsttNm wins over ntceInsttNm");
    }
    if (nara::CollectRegionToken("경상북도") != "경북" || nara::CollectRegionToken("대구,경북") != "경북") {
        return Fail("CollectRegionToken 경상북도/대구,경북 → 경북");
    }
    if (!nara::CollectRegionToken("").empty()) {
        return Fail("empty list region is unknown until PrtcptPsblRgn API");
    }
    if (nara::CollectRegionToken("전국") != "전국") {
        return Fail("전국 stays 전국");
    }
    const auto region_json = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"prtcptPsblRgnNm\":\"경상북도\"}]}}}");
    if (nara::ParseParticipantRegion(region_json) != "경북") {
        return Fail("python extract_region_name reads prtcptPsblRgnNm");
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
    if (nara::FormatYmd(2026, 8, 28) != "2026-08-28") {
        return Fail("FormatYmd must follow OS local Y-M-D, not a hardcoded 2024");
    }
    if (nara::YmdMinusMonths(2026, 8, 28, 12) != "2025-08-28") {
        return Fail("1년 창은 오늘-12개월~오늘 (2026-08-28 → 2025-08-28)");
    }
    if (nara::YmdMinusMonths(2024, 8, 28, 12) != "2023-08-28") {
        return Fail("wrong OS clock 2024 would produce the 2023-2024 footer");
    }
    if (nara::kCollectLookbackMonths != 12) {
        return Fail("수집·표시 lookback은 1년(12개월)");
    }
    const auto span = nara::LookbackCollectMonthSpan(2026, 8);
    if (span.start_month != "2025-08" || span.end_month != "2026-08") {
        std::cerr << "FAIL: 2026-08 1년 기본값 got " << span.start_month << "~" << span.end_month << "\n";
        return 1;
    }
    const auto six = nara::LookbackCollectMonthSpan(2026, 8, 6);
    if (six.start_month != "2026-02") {
        return Fail("6-month helper is only for range math, not the app default");
    }
    if (nara::AddDaysYmd("2026-08-20", 1) != "2026-08-21" || nara::AddDaysYmd("2026-08-31", 1) != "2026-09-01") {
        return Fail("AddDaysYmd rolls the calendar");
    }
    if (nara::YmdToInqryBegin("2026-08-21") != "202608210000" || nara::YmdToInqryEnd("2026-08-29") != "202608292359") {
        return Fail("OpenAPI inqry dates are YYYYMMDD0000..2359");
    }
    const auto gap = nara::PlanMissingCollect("2026-08-20 11:00:00", "2026-08-29", 12);
    if (gap.need != nara::CollectNeed::GapFromStore || gap.from_date != "2026-08-20" || gap.to_date != "2026-08-29") {
        return Fail("업데이트 must include the last stored open_date so same-day 낙찰 is fetched");
    }
    const auto current = nara::PlanMissingCollect("2026-08-29", "2026-08-29", 12);
    if (current.need != nara::CollectNeed::GapFromStore || current.from_date != "2026-08-29"
        || current.to_date != "2026-08-29") {
        return Fail("당일 개찰이 저장돼 있어도 오늘 구간은 다시 조회한다");
    }
    const auto future = nara::PlanMissingCollect("2026-09-01", "2026-08-29", 12);
    if (future.need != nara::CollectNeed::AlreadyCurrent) {
        return Fail("stored open_date after today has nothing left to fetch");
    }
    const auto empty = nara::PlanMissingCollect("", "2026-08-29", 12);
    if (empty.need != nara::CollectNeed::FullLookback || empty.from_date != "2025-08-29") {
        return Fail("empty sqlite still uses the 12-month lookback");
    }
    const auto windows = nara::CollectThreeMonthWindows(span.start_month, span.end_month);
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

    const auto prepar_object = std::string(
        "{\"response\":{\"body\":{\"items\":{\"item\":{\"bssamt\":\"143400000\",\"plnprc\":\"123800000\"}}}}}");
    const auto prepar_base = nara::ParseBaseAmount(prepar_object);
    if (!prepar_base || std::fabs(*prepar_base - 143400000.0) > 0.5) {
        return Fail("python extract_prepar_price_summary reads single item.bssamt");
    }

    nara::AwardNotice bidprcrt_only;
    bidprcrt_only.lower_rate = 86.245;
    const auto fake100 = std::string(
        "{\"response\":{\"body\":{\"items\":[{\"opengRank\":\"1\",\"prcbdrNm\":\"춘추문화재연구원\","
        "\"bidprcAmt\":\"123403300\",\"bidprcrt\":\"100.014\"}]}}}");
    nara::ApplyDetailCompanies(bidprcrt_only, fake100);
    if (!bidprcrt_only.companies.empty() && bidprcrt_only.companies[0].adjust_rate) {
        return Fail("python calculate_row never stores bidprcrt as 기초대비사정률");
    }

    const auto list_q = nara::BuildOpengListQuery("202508010000", "202508312359", "발굴조사", 1);
    if (list_q.find("inqryDiv=2") == std::string::npos) {
        return Fail("python list search uses 개찰일시 inqryDiv=2");
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

    // M2-1: OpenAPI totalCount & detail page count
    const auto paged_body = std::string(
        "{\"response\":{\"body\":{\"totalCount\":135,\"numOfRows\":50,\"pageNo\":1,\"items\":[]}}}");
    if (nara::ExtractOpenApiTotalCount(paged_body) != 135) {
        return Fail("ExtractOpenApiTotalCount must parse response.body.totalCount");
    }
    if (nara::ComputeOpenApiPageCount(135, 50) != 3 || nara::ComputeOpenApiPageCount(100, 100) != 1) {
        return Fail("ComputeOpenApiPageCount ceiling division");
    }

    // M2-2: Matching bidNtceOrd when multiple orders exist
    const auto multi_ord_body = std::string(
        "{\"response\":{\"body\":{\"items\":["
        "{\"bidNtceNo\":\"R26BK01676511\",\"bidNtceOrd\":\"000\",\"sucsfbidLwltRate\":\"87.745\",\"bssAmt\":\"100000000\"},"
        "{\"bidNtceNo\":\"R26BK01676511\",\"bidNtceOrd\":\"001\",\"sucsfbidLwltRate\":\"86.745\",\"bssAmt\":\"120000000\"}"
        "]}}}");
    const auto matched_ord = nara::SelectMatchingNoticeItem(nara::ParseResponseItems(multi_ord_body), "001");
    if (nara::JsonString(matched_ord, "bidNtceOrd") != "001"
        || !nara::ParseLowerRateForOrder(multi_ord_body, "001")
        || std::fabs(*nara::ParseLowerRateForOrder(multi_ord_body, "001") - 86.745) > 0.001) {
        return Fail("SelectMatchingNoticeItem / ParseLowerRateForOrder must pick bidNtceOrd 001");
    }

    // M2-3: Infer missing company adjust_rate from valid anchor in the same notice
    nara::AwardNotice partial_notice;
    partial_notice.notice_number = "R26BK01676511-001";
    partial_notice.companies = {
        {"충청문화재연구원", 1, 99.85000, 86614880.0, 87.745, "314-82-00001"},
        {"동국문화재연구원", 19, std::nullopt, 86441380.0, 87.569, "505-82-00002"},
    };
    nara::InferNoticeCompanyAdjustRates(partial_notice);
    if (!partial_notice.companies[1].adjust_rate
        || std::fabs(*partial_notice.companies[1].adjust_rate - (86441380.0 / 86614880.0 * 99.85000)) > 0.0005) {
        return Fail("InferNoticeCompanyAdjustRates must restore missing adjust_rate from anchor bid_amount");
    }

    // M2-4: Multi-source upcoming bid parser (kaah.kr & d2b.go.kr)
    const auto d2b_json = std::string(
        "{\"list\":[{\"bidNtceNo\":\"2026-국방-001\",\"bidNtceNm\":\"육군 OO부대 매장유산 지표조사 용역\","
        "\"dminsttNm\":\"육군제1234부대\",\"opengDt\":\"2026-10-25 11:00\",\"bssamt\":\"45000000\"}]}");
    const auto d2b_bids = nara::ParseD2bUpcomingBids(d2b_json);
    if (d2b_bids.size() != 1 || d2b_bids[0].source != "국방전자조달"
        || d2b_bids[0].notice_name.find("매장유산") == std::string::npos) {
        return Fail("ParseD2bUpcomingBids should extract cultural heritage bids from d2b JSON");
    }

    const auto kaah_html = std::string(
        "<table><tr><th>입찰명</th><td>화성 남양성지 문화유산 시굴조사 용역</td></tr>"
        "<tr><th>기초금액</th><td>88,500,000원</td></tr>"
        "<tr><th>개찰일시</th><td>2026-10-28 14:00</td></tr></table>");
    const auto kaah_bid = nara::ParseKaahNoticeDetailHtml("KAAH-69", kaah_html);
    if (!kaah_bid || kaah_bid->source != "한국문화유산협회" || kaah_bid->notice_number != "KAAH-69"
        || !kaah_bid->base_amount || std::fabs(*kaah_bid->base_amount - 88500000.0) > 1.0) {
        return Fail("ParseKaahNoticeDetailHtml should extract notice_name, base_amount, open_date");
    }

    std::cerr << "ok award collect rules\n";
    return 0;
}
