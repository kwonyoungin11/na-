#pragma once

#include "nara/design_principles.h"
#include "nara/chart_theme.h"

#include <string>
#include <vector>

namespace nara {
namespace design_page {
using design_principles::LayoutDesignPrinciples;
using design_principles::PrincipleBlockId;

using chart_theme::kDesignBodyArgb;
using chart_theme::kDesignBodySize;
using chart_theme::kDesignHeadingArgb;
using chart_theme::kDesignHeadingSize;
using chart_theme::kDesignLeadArgb;
using chart_theme::kDesignLeadBarW;
using chart_theme::kDesignLeadSize;
using chart_theme::kDesignLineHeight;
using chart_theme::kDesignMeasureMax;
using chart_theme::kDesignPadX;
using chart_theme::kDesignPadY;
using chart_theme::kDesignParaGap;
using chart_theme::kDesignQuoteFillArgb;
using chart_theme::kDesignQuotePad;
using chart_theme::kDesignRuleArgb;
using chart_theme::kDesignRuleH;
using chart_theme::kDesignSectionGap;
using chart_theme::kDesignWheelStep;

enum class BlockKind { Lead, Heading, Body, Quote, Rule };

struct Block {
    BlockKind kind;
    const wchar_t* text;
};

constexpr wchar_t kDesignLead[] = L"이 프로그램은 낙찰을 예측하지 않습니다.";

constexpr Block kBlocks[] = {
    {BlockKind::Lead, kDesignLead},
    {BlockKind::Body,
     L"나라장터 복수예가에서 이미 끝난 공고의 투찰 점을 모아, 누가 어느 기초대비사정률에 붙는지를 보여 주는 지도입니다."},
    {BlockKind::Body,
     L"새 공고를 열면 그 공고의 개찰일을 기준으로 최근 1년 점을 다시 잘라, 이번 판에서 피해야 할 집과 볼 구간만 다시 칠합니다."},
    {BlockKind::Body,
     L"축은 기초대비사정률 99에서 101입니다. 데이터는 앱에 로드된 1년 공고뿐입니다. 5년 원장, 전기간 평균, 사정률 맞히기, 낙찰가 한 점 추천은 이 페이지의 설계에 없습니다."},
    {BlockKind::Body,
     L"점 하나는 한 업체가 한 공고에 넣은 기초대비사정률입니다. 1등만 모으지 않습니다. 1등, 2등, 3등, 순위 없음까지 같은 점입니다. 이 앱의 목적은 “다음에 1등이 어디서 생기는가”가 아니라 “지금 누가 어디에 반복해서 앉아 있는가”입니다."},
    {BlockKind::Body,
     L"복수예가는 투찰 전에 예정가격이 열리지 않습니다. 열리지 않은 값을 정답처럼 찍는 화면은 지도가 아니라 광고입니다. 이 페이지는 그 광고와 같은 문장을 앱 안에 들이지 않기 위해 있습니다."},
    {BlockKind::Rule, L""},

    {BlockKind::Heading, L"이 앱이 일부러 하지 않는 것"},
    {BlockKind::Body,
     L"사정률을 찍지 않습니다. 사정률은 투찰이 끝난 뒤에야 생깁니다. 15개 예가 번호 가운데 많이 선택된 네 개의 평균이 예정가격이 되고, 그 예정가격을 기초금액으로 나눈 값이 사정률입니다. 투찰 순간에 사정률은 없습니다. 없는 숫자를 있는 것처럼 찍는 기능은 넣지 않습니다."},
    {BlockKind::Body,
     L"예가, 낙찰가, 최적 투찰률을 맞히지 않습니다. “추천 99.61”, “적중률 00%”, “슈퍼컴퓨터 산출값” 같은 문장을 화면에 쓰지 않습니다. 한 점을 투찰값으로 내보내면 그 점이 바로 집이 됩니다. 집은 다른 업체의 왼쪽 자리입니다."},
    {BlockKind::Body,
     L"5년 평균, 1등 평균, 시장에서 표가 가장 많이 모인 칸을 정답 칸으로 쓰지 않습니다. 표가 모인 칸은 1등이 가장 잘 나오는 칸이 아닙니다. 사람들이 많이 앉은 칸에 같이 앉는 일을 안전이라고 부르지 않습니다. 그 칸은 많이 남아서 서로 비싸게 붙는 칸입니다."},
    {BlockKind::Body,
     L"미달이 싫다고 100 위로 올리는 기능도 없습니다. 오른쪽은 순위가 남을 가능성이 커 보이지만, 남는 표끼리 가격에서 집니다. 이 앱은 미달 비율을 이유로 사용 구간을 자사 집 오른쪽이나 100 위로 열지 않습니다."},
    {BlockKind::Body,
     L"발주처별로 지도를 쪼개 다른 정답을 만들지 않습니다. 이전 공고에서 나온 구간을 다음 공고에 복사하지 않습니다. 계산은 공고가 바뀔 때마다 처음부터 다시 합니다. 한 번 계산해 두고 한 달 동안 같은 숫자를 붙여 두는 방식은 사용하지 않습니다."},

    {BlockKind::Heading, L"공고를 열 때마다 하는 계산"},
    {BlockKind::Body,
     L"기준일은 그 공고의 개찰일입니다. 개찰일이 없으면 오늘입니다. 사용하는 점은 앱에 로드된 1년 공고 가운데 개찰일이 기준일 이전인 점, 기초대비사정률이 99 이상 101 이하인 점입니다. 기준일보다 미래의 공고는 계산에 넣지 않습니다. 앱에 없는 더 오래된 연도를 상상해서 평균을 만들지 않습니다."},
    {BlockKind::Body, L"그 점 집합으로 네 가지를 다시 만듭니다."},
    {BlockKind::Body,
     L"첫째, 동국의 1년 구간입니다. 동국이 그 기간에 넣은 모든 점의 아래 사분위와 위 사분위입니다. 1등 점만 모은 값이 아닙니다. 점이 여덟 개보다 적으면 구간을 만들지 않습니다. 부족한 표본을 평균값으로 메우지 않습니다. 표본이 부족하면 부족하다고 말하면 됩니다."},
    {BlockKind::Body,
     L"둘째, 체크한 업체의 집입니다. 같은 1년, 같은 축에서 출전 공고가 여덟 건 이상이고, 점의 사분위 너비가 0.50보다 좁으면 그 업체의 아래 사분위부터 위 사분위까지를 집으로 봅니다. 너비가 0.50 이상이면 축 전체에 뿌린 것으로 보고 가리지 않습니다. 집이 없다고 시장 평균 칸을 대신 넣지 않습니다. 체크를 끄면 그 집도 사라집니다. 전체 시장을 한 칸으로 요약하지 않습니다."},
    {BlockKind::Body,
     L"셋째, 부착 업체입니다. 동국 구간이 이미 집일 때, 그 집의 왼쪽 0.20 안에 점이 반복되는 업체입니다. 자사 집을 알고 왼쪽에 붙는 자리입니다. 이 업체들은 평균가 추천을 따라서가 아니라, 보이는 집을 기준으로 움직인 자리에 가깝습니다."},
    {BlockKind::Body,
     L"넷째, 이번 공고의 사용 구간입니다. 화면에 “이번 판에서 볼 곳”으로 칠하는 유일한 강조 구간입니다. 한 점이 아니라 구간입니다. 중간값을 투찰값으로 바꿔 출력하지 않습니다."},

    {BlockKind::Heading, L"자사 집이 좁을 때"},
    {BlockKind::Body,
     L"동국 1년 점의 너비가 0.15보다 작으면 그 구간은 넣을 곳이 아닙니다. 이미 공개된 집입니다. 같은 1년 원장을 보는 업체는 그 집을 알고 왼쪽에 앉을 수 있습니다. 좁은 구간을 그대로 “동국이 가야 할 곳”으로 칠하면, 앱이 자사 좌표를 경쟁에게 정리해 주는 일이 됩니다."},
    {BlockKind::Body,
     L"로드된 1년 원장 기준으로 동국 점은 대략 99.56에서 99.65 사이에 붙어 있습니다. 너비는 약 0.08입니다. 이 상태에서는 매 공고가 자사 집 분기입니다. 6개월 중앙값과 1년 중앙값의 차이도 거의 없습니다. 습관이 고정되어 있다는 뜻입니다."},
    {BlockKind::Body,
     L"그때 화면은 이렇게 칠합니다. 동국 99.56–99.65는 금지입니다. 사용 구간은 그 집의 왼쪽만 엽니다. 집 안과 집의 오른쪽은 열지 않습니다. 체크한 집이나 부착 업체가 왼쪽 사용 구간을 덮으면 그 집의 왼쪽을 한 번 더 자릅니다. 직전 동국 투찰과 같은 네 자리는 사용 구간에서 뺍니다. 남은 폭이 0.05보다 좁으면 “겹침”만 표시하고 숫자를 찍지 않습니다."},
    {BlockKind::Body,
     L"같은 구간은 습관입니다. 같은 점은 먹이입니다. 앱이 공고마다 거의 같은 띠를 보여 주는 것은 오류가 아닙니다. 1년 습관이 그 자리에 붙어 있기 때문입니다. 다만 그 띠를 “추천 투찰값”으로 바꾸지 않습니다. 좁아진 띠는 숨기지 않고 집으로 표시한 뒤, 그 왼쪽만 사용 구간으로 다시 그립니다."},
    {BlockKind::Body,
     L"너비가 0.15 이상으로 벌어지면, 그때만 동국 구간 안을 사용 구간의 출발점으로 되돌립니다. 6개월 중앙값이 1년 중앙값과 0.20 이상 벌어지면 경고만 띄웁니다. 경고가 났다고 시장에서 표가 많은 칸으로 옮기지 않습니다. 습관이 움직이기 시작했다는 표시일 뿐입니다."},

    {BlockKind::Heading, L"참여 인원"},
    {BlockKind::Body,
     L"한 업체의 집을 정확히 비워 얻는 양은, 그 업체가 이번에도 그 집에 앉을 때만 생깁니다. 판에 남은 업체가 많을수록 그 양은 작습니다. 대략 서른두 곳 이상이면 한 곳 가리기로 0.1퍼센트포인트를 담보하기 어렵습니다. 열다섯 곳 안팎이면 이야기가 달라질 수 있습니다."},
    {BlockKind::Body,
     L"그래서 화면에는 확률 퍼센트를 쓰지 않고 참여 인원만 참고로 둡니다. 칸을 0.02 움직이는 일보다, 이번 판에 몇 곳이 붙는지를 먼저 보는 것이 맞습니다. 큰 판에서 집을 조금 비우는 일을 낙찰 전략의 전부라고 부르지 않습니다. 작은 판에서 집이 짧고 출전이 잦은 곳 하나를 정확히 가리는 일은, 그 전제 위에서만 의미가 있습니다."},

    {BlockKind::Heading, L"화면에 남는 줄"},
    {BlockKind::Body, L"자사 집일 때는 예를 들어 이렇게 씁니다."},
    {BlockKind::Quote, L"자사집 99.56–99.65 | 사용 99.41–99.56 | 부착 n | N≈"},
    {BlockKind::Body,
     L"자사 집이 아닐 때는 띠와 사용 구간과 집 개수와 참여 인원만 씁니다. 추천, 최적, 적중, 확률이라는 단어는 이 줄에 없습니다. 숫자가 두 개 나란히 있으면 그것은 구간입니다. 가운데를 찍으라는 뜻이 아닙니다."},

    {BlockKind::Heading, L"광고하는 사정률 추천의 문제"},
    {BlockKind::Body,
     L"시중에 입찰 분석 업체가 많습니다. 대부분 슈퍼컴퓨터, 인공지능, 빅데이터, 높은 적중률을 내세우고 회원을 모아 돈을 받습니다. 상품의 핵심은 보통 “이번 공고의 사정률”이거나 “이번에 넣을 한 점”입니다. 화면은 정밀해 보이고, 숫자는 소수점 아래까지 떨어집니다. 그 정밀함이 예가를 열지는 않습니다."},
    {BlockKind::Body,
     L"문제는 성능이 조금 부족한 것이 아닙니다. 맞힐 대상이 투찰 전에 존재하지 않는다는 것입니다."},
    {BlockKind::Body,
     L"사정률은 예가가 열린 뒤에 생깁니다. 예가는 미리 정해 둔 열다섯 개 번호 가운데 많이 찍힌 값으로 만들어집니다. 그 다수결은 회원사 컴퓨터 안에 있지 않습니다. 사정률을 추천 숫자로 주는 순간, 아직 안 열린 값을 아는 척하는 것입니다. 컴퓨터가 클수록 그 척이 더 믿겨 보일 뿐입니다."},
    {BlockKind::Body,
     L"축도 다릅니다. 업체가 넣는 것은 투찰 금액입니다. 이 앱의 축은 기초대비사정률입니다. 사정률 99점대를 그대로 기초대비 축에 옮기면, 다른 물건의 숫자를 한 점으로 고정하게 됩니다. 그 점을 여러 고객에게 같은 달, 같은 유형으로 뿌리면 그 점이 집이 됩니다. 집을 만든 추천은 다음 달부터 더 잘 틀립니다. 틀리면 더 비싼 등급, 더 많은 데이터, 더 큰 컴퓨터가 해결책으로 나옵니다."},
    {BlockKind::Body,
     L"원장이 보여 주는 모양도 추천과 반대입니다. 표는 100.2 근처에 많이 모입니다. 그 칸은 1등 비율이 낮습니다. 1등 자리는 해마다 한 칸에 고정되지 않습니다. 평균가, 평균 사정률, 1등 평균에 맞추라는 말은 표가 이미 쌓인 산의 한가운데로 사람을 모읍니다. 그 자리는 하한에는 잘 안 걸리고 1등은 잘 안 나옵니다. 광고가 “안전한 자리”라고 부르는 곳이 바로 그 산입니다."},
    {BlockKind::Body,
     L"광고의 적중률은 대개 낙찰이 아닙니다. 사정률과의 거리, 상위권 진입, 잘 맞은 공고만 남긴 표본을 적중이라고 부릅니다. 예순 곳이 붙는 판에서 낙찰의 기본값은 대략 2퍼센트입니다. 그 기본값을 수십 퍼센트로 올려 주는 사정률 숫자는 이 1년 원장에 없습니다. 근접을 적중으로 바꿔 부르면 실적은 항상 좋아 보입니다. 계약은 근접이 아니라 1등만 가져갑니다."},
    {BlockKind::Body,
     L"사람들이 그 상품에 모이는 이유는 슈퍼컴퓨터가 예가를 열어서가 아닙니다. 미달이 두렵고, 옆 업체가 같은 숫자를 받는다고 생각하면 안 쓰는 쪽이 손해 같고, 한 숫자를 받으면 이번 패의 책임이 프로그램으로 옮겨 가기 때문입니다. 맞으면 프로그램 덕이고 지면 예가가 빗나갔다는 구조는, 판매에는 유리하고 다음 투찰에는 유리하지 않습니다."},
    {BlockKind::Body,
     L"한 점을 여러 회원에게 같은 값으로 주면 회원끼리도 집이 됩니다. 추천을 따르는 사람이 늘수록 그 칸의 유효 경쟁은 늘어납니다. 상품이 잘 팔릴수록 그 상품의 숫자는 더 나빠진다는 뜻입니다. 이것은 컴퓨터가 부족해서가 아니라, 한 점을 팔아서입니다."},

    {BlockKind::Heading, L"이 앱이 그 상품과 다른 점"},
    {BlockKind::Body,
     L"이 앱은 사정률을 팔지 않습니다. 없는 값을 있는 척 찍지 않습니다. 컴퓨터를 크게 만들어 평균가를 더 정밀하게 맞히겠다고 하지 않습니다. 적중률 표를 걸어 회원을 모으지 않습니다."},
    {BlockKind::Body,
     L"대신 세 가지만 정직하게 보여 줍니다. 누가 1년 동안 어디에 붙었는가. 동국이 이미 너무 좁게 붙어 집이 되었는가. 이번 공고에서 그 집과 경쟁 집을 왼쪽으로 가리면 남는 구간이 어디인가."},
    {BlockKind::Body,
     L"남는 구간이 거의 없으면 억지로 한 점을 만들지 않습니다. 겹쳤다고 말하면 됩니다. 한 점을 만들어야 유료 상품처럼 보이지만, 그 한 점이 바로 다음 먹이입니다."},
    {BlockKind::Body,
     L"계산은 매 공고 다시 합니다. 다만 1년 습관이 안 움직이면 구간도 거의 같습니다. 그게 정상입니다. 매 공고 다른 숫자를 찍어 주는 화면은 분석이 아니라 난수이거나, 없는 사정률을 흉내 내는 일입니다."},
    {BlockKind::Body,
     L"체크를 바꾸면 집과 사용 구간만 다시 그립니다. 공고를 바꾸면 점 집합부터 다시 자릅니다. 저장해 둔 “이번 달의 정답 칸”은 없습니다."},

    {BlockKind::Heading, L"한 줄"},
    {BlockKind::Body,
     L"이 페이지의 설계는 단순합니다. 예측하지 말고, 1년 전체 점으로 집을 그리고, 자사 집이 좁으면 그 집을 숨기지 말고 왼쪽으로만 비우십시오. 슈퍼컴퓨터가 주는 사정률 한 점은 그 반대입니다. 없는 값을 찍고, 찍힌 점으로 집을 만들고, 이미 사람이 모인 칸으로 더 모읍니다. 이 앱의 한 페이지는 그 숫자를 따라가지 않기 위해 있습니다. 지도는 전체를 보여 주고, 공고가 바뀌면 집을 다시 그리고, 좁아진 자사 좌표는 추천이 아니라 금지로 표시합니다. 그것이 이 화면의 전부입니다."},
};

struct DesignLine {
    BlockKind kind = BlockKind::Body;
    std::wstring text;
    int x = 0;
    int y = 0;
    int w = 0;
    int h = 0;
    int bg_x = 0;
    int bg_y = 0;
    int bg_w = 0;
    int bg_h = 0;
    bool bold = false;
    float size = 13.0f;
    uint32_t argb = 0;
};

struct DesignLayout {
    int content_h = 0;
    int origin_x = 0;
    int inner_w = 0;
    std::vector<DesignLine> lines;
};

inline std::wstring PlainText() {
    std::wstring out;
    for (const auto& block : kBlocks) {
        if (block.text == nullptr || block.text[0] == 0) {
            continue;
        }
        if (!out.empty()) {
            out += L'\n';
        }
        out += block.text;
    }
    return out;
}

inline float DesignGlyphWidth(wchar_t ch, float size) {
    if (ch == L' ') {
        return size * 0.34f;
    }
    if (ch < 0x80) {
        return size * 0.56f;
    }
    return size * 1.04f;
}

inline int DesignLineHeight(float size) {
    return (std::max)(18, static_cast<int>(std::lround(static_cast<double>(size) * static_cast<double>(kDesignLineHeight))));
}

inline std::vector<std::wstring> WrapDesignText(const wchar_t* text, int width, float size) {
    std::vector<std::wstring> lines;
    if (text == nullptr || text[0] == 0) {
        return lines;
    }
    const float max_w = static_cast<float>((std::max)(24, width));
    std::wstring line;
    float line_w = 0.0f;
    int last_space = -1;
    auto flush = [&](size_t cut) {
        lines.push_back(line.substr(0, cut));
        std::wstring rest = line.substr(cut);
        while (!rest.empty() && rest.front() == L' ') {
            rest.erase(rest.begin());
        }
        line = std::move(rest);
        line_w = 0.0f;
        last_space = -1;
        for (wchar_t ch : line) {
            line_w += DesignGlyphWidth(ch, size);
            if (ch == L' ') {
                last_space = static_cast<int>(line.size()) - 1;
            }
        }
    };
    for (const wchar_t* p = text; *p != 0; ++p) {
        if (*p == L'\n') {
            lines.push_back(line);
            line.clear();
            line_w = 0.0f;
            last_space = -1;
            continue;
        }
        const float gw = DesignGlyphWidth(*p, size);
        if (!line.empty() && line_w + gw > max_w) {
            const size_t cut = last_space > 0 ? static_cast<size_t>(last_space) : line.size();
            flush(cut);
        }
        if (*p == L' ') {
            last_space = static_cast<int>(line.size());
        }
        line.push_back(*p);
        line_w += gw;
    }
    if (!line.empty() || lines.empty()) {
        lines.push_back(line);
    }
    return lines;
}

inline float SizeFor(BlockKind kind) {
    if (kind == BlockKind::Lead) {
        return kDesignLeadSize;
    }
    if (kind == BlockKind::Heading) {
        return kDesignHeadingSize;
    }
    return kDesignBodySize;
}

inline uint32_t ColorFor(BlockKind kind) {
    if (kind == BlockKind::Lead) {
        return kDesignLeadArgb;
    }
    if (kind == BlockKind::Heading || kind == BlockKind::Quote) {
        return kDesignHeadingArgb;
    }
    return kDesignBodyArgb;
}

inline DesignLayout LayoutDesignPage(int view_w) {
    DesignLayout out;
    const int safe_w = (std::max)(1, view_w);
    out.inner_w = (std::max)(160, (std::min)(kDesignMeasureMax, safe_w - 2 * kDesignPadX));
    out.origin_x = (std::max)(kDesignPadX, (safe_w - out.inner_w) / 2);
    int y = kDesignPadY;
    bool first = true;
    for (const auto& block : kBlocks) {
        if (block.kind == BlockKind::Rule) {
            DesignLine line;
            line.kind = BlockKind::Rule;
            line.x = out.origin_x;
            line.y = y;
            line.w = out.inner_w;
            line.h = kDesignRuleH + 10;
            line.bg_x = out.origin_x;
            line.bg_y = y + 4;
            line.bg_w = out.inner_w;
            line.bg_h = kDesignRuleH;
            line.argb = kDesignRuleArgb;
            out.lines.push_back(line);
            y += line.h + kDesignParaGap;
            first = false;
            continue;
        }
        if (!first && block.kind == BlockKind::Heading) {
            y += kDesignSectionGap;
        }
        first = false;
        const float size = SizeFor(block.kind);
        const int lh = DesignLineHeight(size);
        const bool quote = block.kind == BlockKind::Quote;
        const int text_w = quote ? (std::max)(80, out.inner_w - 2 * kDesignQuotePad) : out.inner_w;
        const auto wrapped = WrapDesignText(block.text, text_w, size);
        const int quote_top = y;
        const int first_line = static_cast<int>(out.lines.size());
        if (quote) {
            y += kDesignQuotePad;
        }
        if (wrapped.empty()) {
            continue;
        }
        for (const auto& part : wrapped) {
            DesignLine line;
            line.kind = block.kind;
            line.text = part;
            line.size = size;
            line.bold = block.kind == BlockKind::Lead || block.kind == BlockKind::Heading;
            line.argb = ColorFor(block.kind);
            line.h = lh;
            line.w = text_w;
            line.x = out.origin_x + (quote ? kDesignQuotePad : 0);
            line.y = y;
            if (block.kind == BlockKind::Lead) {
                line.bg_x = line.x - 10;
                line.bg_y = line.y;
                line.bg_w = kDesignLeadBarW;
                line.bg_h = line.h;
            }
            out.lines.push_back(std::move(line));
            y += lh;
        }
        if (quote) {
            y += kDesignQuotePad;
            if (first_line >= 0 && first_line < static_cast<int>(out.lines.size())) {
                auto& head = out.lines[static_cast<size_t>(first_line)];
                head.bg_x = out.origin_x;
                head.bg_y = quote_top;
                head.bg_w = out.inner_w;
                head.bg_h = y - quote_top;
            }
        }
        y += kDesignParaGap;
    }
    out.content_h = y + kDesignPadY;
    return out;
}

inline int ClampDesignScroll(int y, int content_h, int view_h) {
    const int max_y = (std::max)(0, content_h - view_h);
    if (y < 0) {
        return 0;
    }
    if (y > max_y) {
        return max_y;
    }
    return y;
}

inline int DesignScrollDelta(int wheel_delta) { return -wheel_delta * kDesignWheelStep / 120; }

}  // namespace design_page
}  // namespace nara
