#include "nara/igunsul_convert.h"

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
    using nara::BuildDisplayMap;
    using nara::BuildShortLabel;
    using nara::CanonicalizeCompany;
    using nara::CompanyIdentity;

    if (CanonicalizeCompany("재단법인 한울문화유산연구원") != "한울문화재연구원") {
        return Fail("한울 문화유산 개명은 한울문화재연구원으로 합친다");
    }
    if (CanonicalizeCompany("한울문화재연구원") != "한울문화재연구원") {
        return Fail("한울문화재연구원 stays");
    }
    if (CanonicalizeCompany("재단법인 동국문화유산연구원") != "동국문화재연구원") {
        return Fail("동국 문화유산 개명은 동국문화재연구원으로 합친다");
    }

    const auto hanguk_heritage = CanonicalizeCompany("재단법인 한국문화유산연구원");
    const auto hanguk_jae = CanonicalizeCompany("재단법인 한국문화재연구원");
    if (hanguk_heritage == hanguk_jae) {
        return Fail("한국문화유산연구원 must not collapse into 한국문화재연구원");
    }
    if (hanguk_heritage.find("한국문화유산연구원") == std::string::npos) {
        return Fail("한국문화유산연구원 keeps 유산 in the identity");
    }
    if (hanguk_jae != "한국문화재연구원") {
        return Fail("한국문화재연구원 stays 한국문화재연구원");
    }

    nara::ParticipantRow stored_wrong;
    stored_wrong.company_name = "재단법인 한국문화유산연구원";
    stored_wrong.normalized_company_name = "한국문화재연구원";
    nara::ParticipantRow stored_jae;
    stored_jae.company_name = "재단법인 한국문화재연구원";
    stored_jae.normalized_company_name = "한국문화재연구원";
    if (CompanyIdentity(stored_wrong) == CompanyIdentity(stored_jae)) {
        return Fail("CompanyIdentity prefers raw company_name so stale sqlite normalized does not merge 한국");
    }

    const auto unique = BuildDisplayMap({"동아세아문화재연구원", "가경문화재연구원"});
    if (unique.at("동아세아문화재연구원") != "동아") {
        return Fail("unique two-letter label is 동아");
    }
    if (unique.at("가경문화재연구원") != "가경") {
        return Fail("unique two-letter label is 가경");
    }

    const auto conflict = BuildDisplayMap({"동아세아문화재연구원", "동아문화재연구원"});
    if (conflict.at("동아세아문화재연구원") != "동세") {
        std::cerr << "got " << conflict.at("동아세아문화재연구원") << "\n";
        return Fail("동아세아 vs 동아 compressed unique is 동세");
    }
    if (conflict.at("동아문화재연구원") != "동문") {
        std::cerr << "got " << conflict.at("동아문화재연구원") << "\n";
        return Fail("동아 compressed unique is 동문");
    }

    const auto jeju = BuildDisplayMap({"제주문화재연구원", "제주고고학연구소"});
    if (jeju.at("제주문화재연구원") == jeju.at("제주고고학연구소")) {
        return Fail("제주문화재연구원 and 제주고고학연구소 must not share a chip label");
    }
    if (jeju.at("제주문화재연구원").size() < 2 || jeju.at("제주고고학연구소").size() < 2) {
        return Fail("conflict labels stay readable");
    }

    const auto hanguk = BuildDisplayMap({
        "한국문화재연구원",
        "한국문화유산연구원",
        "한국문물연구원",
        "한국고고환경연구소",
        "한국선사문화연구원",
    });
    std::vector<std::string> labels;
    for (const auto& name : {"한국문화재연구원", "한국문화유산연구원", "한국문물연구원", "한국고고환경연구소",
                             "한국선사문화연구원"}) {
        const auto found = hanguk.find(name);
        if (found == hanguk.end()) {
            return Fail("한국 family key missing after canonicalize");
        }
        const auto& label = found->second;
        if (label.empty()) {
            return Fail("한국 family label is empty");
        }
        for (const auto& seen : labels) {
            if (seen == label) {
                std::cerr << "duplicate label " << label << "\n";
                return Fail("한국 family chip labels must be unique");
            }
        }
        labels.push_back(label);
    }

    if (BuildShortLabel("한울문화재연구원") != "한울") {
        return Fail("BuildShortLabel 한울");
    }

    if (CanonicalizeCompany("(재)세종문화유산재단") != "세종문화유산재단") {
        std::cerr << "got " << CanonicalizeCompany("(재)세종문화유산재단") << "\n";
        return Fail("(재)세종문화유산재단 keeps 유산재단");
    }
    if (CanonicalizeCompany("재단법인 세계문화유산연구재단") != "세계문화유산연구재단") {
        return Fail("재단법인 세계문화유산연구재단 keeps 연구재단");
    }
    if (CanonicalizeCompany("(재)백제문화재단") != "백제문화재단") {
        return Fail("(재)백제문화재단 keeps 문화재단");
    }
    if (CanonicalizeCompany("(재)경북문화재단 문화유산원") != CanonicalizeCompany("(재)경북문화재단")) {
        return Fail("(재)경북문화재단 문화유산원 and (재)경북문화재단 must canonicalize to the same identity");
    }
    if (CanonicalizeCompany("(재)경기문화재단 경기역사문화유산원") != CanonicalizeCompany("(재)경기역사문화유산원")
        || CanonicalizeCompany("(재)경기역사문화유산원") != CanonicalizeCompany("경기문화재연구원")) {
        return Fail("경기역사문화유산원 / 경기문화재연구원 alias merge");
    }
    if (CanonicalizeCompany("(재)울산연구원 문화유산연구실") != CanonicalizeCompany("(재)울산연구원")) {
        return Fail("(재)울산연구원 문화유산연구실 merges with 울산연구원");
    }
    if (CanonicalizeCompany("(재)경남연구원 경남학센터") != CanonicalizeCompany("(재)경남연구원")) {
        return Fail("(재)경남연구원 경남학센터 merges with 경남연구원");
    }
    if (CanonicalizeCompany("(재)전남문화재단 국가유산연구소") != CanonicalizeCompany("(재)전라남도문화재단")) {
        return Fail("(재)전남문화재단 국가유산연구소 merges with 전라남도문화재단");
    }

    std::cerr << "ok company name identity and display map\n";
    return 0;
}
