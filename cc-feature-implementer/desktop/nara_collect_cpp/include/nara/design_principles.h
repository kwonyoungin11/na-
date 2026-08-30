#pragma once

#include "nara/chart_theme.h"

#include <cmath>
#include <string>
#include <vector>

namespace nara {
namespace design_principles {

constexpr const wchar_t* kEpilogueParagraphs[] = {
    L"이 앱을 만든 까닭은 동국의 다음 한 장을 지키기 위해서다.",
    L"예가를 맞히겠다는 화면이 아니라, 동국이 이미 앉아 버린 칸을 외면하지 않기 위한 화면이다. 위에는 그 칸을 한 점으로 팔아 넘기는 상품을 두고, 아래에는 동국이 그 상품을 거절하는 이유를 둔다.",
    L"시중의 추천은 슈퍼컴퓨터와 적중률로 한 점을 판다. 그 점의 이름은 대개 사정률이다. 사정률은 예가가 열린 뒤에야 생긴다. 동국이 도장을 찍는 순간에는 그 숫자가 없다. 없는 값을 여러 곳이 같이 받으면 그 값이 집이 된다. 집이 된 칸에 표가 쌓이면 1등은 그 칸에서 나오지 않는다. 적중이라는 말은 대부분 낙찰이 아니라 사정률과의 거리다. 동국이 가져가야 하는 것은 거리가 아니라 계약이다.",
    L"그 한 점이 사람을 모으는 자리는 기초대비사정률 100 근처다. 표가 이미 두꺼운 산이다. 그 산을 안전이라고 부르는 것은 하한에 안 걸린다는 말일 뿐이다. 안 걸리고 남는 표는 서로 비싸게 붙는다. 동국이 그 산에 같이 앉으면, 분석을 한 것이 아니라 남이 만든 집에 세를 든 것이다.",
    L"그래서 이 앱은 예측기가 아니다. 동국이 로드한 1년의 전체 업체, 전체 순위, 전체 투찰 점을 같은 점으로 찍는다. 1등만 모아 동국에게 칸을 주지 않는다. 공고를 열면 그 개찰일로 1년을 다시 자른다. 동국 담당자에게 한 점을 투찰값으로 건네지 않는다. 한 점을 받는 습관이 바로 저 산으로 가는 길이다.",
    L"동국 구간 T는 동국이 그 1년에 넣은 전체 점의 가운데 절반이다. 지금 창의 보기는 99.56에서 99.65다. 보기일 뿐, 동국의 영원한 자리가 아니다. 너비가 0.15보다 좁으면 그 구간은 이미 집이다. 자랑할 자리가 아니다. 남이 볼 수 있는 동국의 좌표다. 이 앱은 그 좌표를 숨기지 않는다. 숨기는 순간 동국만 모르고 남은 안다.",
    L"그 집 안에 들어와 동국보다 오른쪽에 앉은 표는 먹이다. 이 그림을 크게 그리는 이유는 남을 가르치기 위해서다. T에 들어가면 동국에게 먹힌다는 것이 보이면, 그 칸을 기피하게 된다. 기피가 생기면 T는 비로소 동국의 칸이 된다. 기피가 생기기 전에 T 전체를 사용 구간으로 칠하면, 이 앱이 동국의 집을 시장에 공표하는 일이 된다. 그것은 최선을 다하는 분석이 아니다.",
    L"위험한 쪽은 T의 오른쪽이 아니라 왼쪽이다. T 왼쪽 0.20에 붙는 집이 있으면 동국이 먹이가 된다. 1년 원장에서 동국이 이긴 판은 그 왼쪽을 비웠을 때와 겹친다. 그러므로 좁아진 자사 좌표를 화면 한가운데에 “추천”으로 두지 않는다. 금지로 두고, 왼쪽을 본다.",
    L"이번 판에서 동국이 볼 칸은 한 점이 아니다. T 안에 아직 침입이 있으면 왼쪽 변만 지킨다. 남이 T를 비우기 시작한 뒤에야 T 안을 쓴다. 그때도 직전과 같은 네 자리는 구멍으로 남긴다. 같은 점은 동국을 다시 집으로 만든다. 남은 폭이 너무 좁으면 숫자를 만들지 않고 겹침이라고만 적는다. 억지로 한 점을 만드는 일은 동국에게 가장 비싼 거짓말이다.",
    L"계산은 공고마다 다시 한다. 기준일은 그 공고의 개찰일이다. 점은 앱에 있는 1년만 쓴다. 어제 구간을 오늘 공고에 붙이지 않는다. 한 집을 가리는 일은 그 집이 이번에도 앉을 때만 의미가 있다. 판이 크면 그 이득은 작다. 동국의 낙찰을 칸 0.02로 설명하지 않는다. 이번 판에 몇 곳이 붙는지를 먼저 본다.",
    L"동국이 이 앱에 요구하는 것은 한 줄이다.",
    L"T에 들어오면 먹이가 되게 보여 T를 비우고, 빈 T를 동국의 칸으로 유지하라.",
    L"예측하지 마라. 1년 전체 점을 봐라. 좁으면 집이다. 움직일 때는 왼쪽만 봐라. 공고가 바뀌면 다시 그려라.",
    L"이 창은 차트의 집을 계산하지 않는다. 차트가 동국의 집을 그리고, 이 창은 왜 그 집을 숨기지 않는지만 말한다.",
    L"숨기지 않는 것이 동국에게 최선을 다하는 일이다.",
};

inline int EpilogueParagraphCount() {
    return static_cast<int>(sizeof(kEpilogueParagraphs) / sizeof(kEpilogueParagraphs[0]));
}

inline std::wstring EpiloguePlain() {
    std::wstring out;
    for (int i = 0; i < EpilogueParagraphCount(); ++i) {
        if (!out.empty()) {
            out += L'\n';
        }
        out += kEpilogueParagraphs[static_cast<size_t>(i)];
    }
    return out;
}

inline float EpilogueGlyphWidth(wchar_t ch, float size) {
    if (ch == L' ') {
        return size * 0.34f;
    }
    if (ch < 0x80) {
        return size * 0.56f;
    }
    return size * 1.02f;
}

inline std::vector<std::wstring> WrapEpilogue(const wchar_t* text, int width, float size) {
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
            line_w += EpilogueGlyphWidth(ch, size);
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
        const float gw = EpilogueGlyphWidth(*p, size);
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
    lines.push_back(line);
    return lines;
}

enum class EpilogueKind { Eyebrow, Lead, Body, Quote, Close, Rule };

struct EpilogueLine {
    std::wstring text;
    EpilogueKind kind = EpilogueKind::Body;
    int x = 0;
    int y = 0;
    int w = 0;
    int h = 0;
    int bg_x = 0;
    int bg_y = 0;
    int bg_w = 0;
    int bg_h = 0;
    float size = 13.0f;
    uint32_t argb = 0;
    bool lead = false;
    bool bold = false;
};

struct EpilogueLayout {
    std::vector<EpilogueLine> lines;
    int content_h = 0;
    bool has_figure = false;
    bool numbered = false;
    bool has_eyebrow = false;
    bool has_lead_bar = false;
    bool has_quote = false;
};

inline EpilogueKind KindForParagraph(int index, int count) {
    if (index == 0) {
        return EpilogueKind::Lead;
    }
    if (index == 11) {
        return EpilogueKind::Quote;
    }
    if (index == count - 1) {
        return EpilogueKind::Close;
    }
    return EpilogueKind::Body;
}

inline EpilogueLayout BuildEpilogue(int view_w, float lead_size, float body_size, float quote_size, float line_mul,
                                   int para_gap) {
    using chart_theme::kDesignRuleArgb;
    using chart_theme::kEpilogueAccentArgb;
    using chart_theme::kEpilogueEyebrow;
    using chart_theme::kEpilogueLeadBarW;
    using chart_theme::kEpilogueQuotePad;
    using chart_theme::kSubtleTextArgb;
    using chart_theme::kTextArgb;

    EpilogueLayout out;
    out.has_figure = false;
    out.numbered = false;
    const int inner_w = (std::max)(40, view_w);
    int y = 0;

    {
        EpilogueLine eye;
        eye.kind = EpilogueKind::Eyebrow;
        eye.text = kEpilogueEyebrow;
        eye.size = 11.0f;
        eye.argb = kEpilogueAccentArgb;
        eye.bold = true;
        eye.x = 0;
        eye.y = y;
        eye.w = inner_w;
        eye.h = 16;
        out.lines.push_back(std::move(eye));
        y += 20;
        out.has_eyebrow = true;
    }

    const int n = EpilogueParagraphCount();
    const int last = n - 1;
    for (int i = 0; i < n; ++i) {
        const auto kind = KindForParagraph(i, n);
        if (kind == EpilogueKind::Quote || kind == EpilogueKind::Close) {
            EpilogueLine rule;
            rule.kind = EpilogueKind::Rule;
            rule.y = y;
            rule.h = 10;
            rule.w = inner_w;
            rule.bg_x = 0;
            rule.bg_y = y + 4;
            rule.bg_w = inner_w;
            rule.bg_h = 1;
            rule.argb = kDesignRuleArgb;
            out.lines.push_back(std::move(rule));
            y += 10;
        }

        const float size = kind == EpilogueKind::Lead ? lead_size
            : (kind == EpilogueKind::Quote || kind == EpilogueKind::Close ? quote_size : body_size);
        const int lh =
            (std::max)(16, static_cast<int>(std::lround(static_cast<double>(size) * static_cast<double>(line_mul))));
        const int bar = kind == EpilogueKind::Lead ? kEpilogueLeadBarW + 10 : 0;
        const int qpad = kind == EpilogueKind::Quote ? kEpilogueQuotePad : 0;
        const int text_x = bar + qpad;
        const int text_w = (std::max)(40, inner_w - text_x - qpad);
        if (qpad > 0) {
            y += qpad;
        }
        const int block_top = y;
        const int first = static_cast<int>(out.lines.size());
        const auto wrapped = WrapEpilogue(kEpilogueParagraphs[static_cast<size_t>(i)], text_w, size);
        for (const auto& part : wrapped) {
            EpilogueLine line;
            line.kind = kind;
            line.text = part;
            line.x = text_x;
            line.y = y;
            line.w = text_w;
            line.h = lh;
            line.size = size;
            line.lead = kind == EpilogueKind::Lead;
            line.bold = kind != EpilogueKind::Body;
            line.argb = kind == EpilogueKind::Quote ? kEpilogueAccentArgb : kTextArgb;
            if (kind == EpilogueKind::Close) {
                line.argb = kTextArgb;
            }
            out.lines.push_back(std::move(line));
            y += lh;
        }
        if (qpad > 0) {
            y += qpad;
        }
        if (first < static_cast<int>(out.lines.size())) {
            auto& head = out.lines[static_cast<size_t>(first)];
            if (kind == EpilogueKind::Lead) {
                head.bg_x = 0;
                head.bg_y = block_top;
                head.bg_w = kEpilogueLeadBarW;
                head.bg_h = y - block_top;
                out.has_lead_bar = true;
            } else if (kind == EpilogueKind::Quote) {
                head.bg_x = 0;
                head.bg_y = block_top - qpad;
                head.bg_w = inner_w;
                head.bg_h = y - (block_top - qpad);
                out.has_quote = true;
            }
        }
        if (i != last) {
            y += para_gap;
        }
        (void)kSubtleTextArgb;
    }
    out.content_h = y;
    return out;
}

inline EpilogueLayout LayoutEpilogue(int view_w, int view_h) {
    const int safe_h = (std::max)(120, view_h);
    float lead = 19.0f;
    float body = 13.0f;
    float quote = 15.0f;
    float mul = 1.38f;
    int gap = 7;
    auto laid = BuildEpilogue(view_w, lead, body, quote, mul, gap);
    int guard = 0;
    while (laid.content_h > safe_h && guard < 36) {
        body = (std::max)(11.0f, body - 0.12f);
        lead = (std::max)(18.0f, lead - 0.12f);
        quote = (std::max)(13.0f, quote - 0.12f);
        mul = (std::max)(1.22f, mul - 0.012f);
        gap = (std::max)(4, gap - 1);
        laid = BuildEpilogue(view_w, lead, body, quote, mul, gap);
        ++guard;
    }
    while (laid.content_h + 36 < safe_h && guard < 72) {
        const float next_mul = mul + 0.015f;
        const int next_gap = gap + 1;
        auto taller = BuildEpilogue(view_w, lead, body, quote, next_mul, next_gap);
        if (taller.content_h > safe_h) {
            break;
        }
        mul = next_mul;
        gap = next_gap;
        laid = std::move(taller);
        ++guard;
    }
    if (!laid.lines.empty() && laid.content_h < safe_h) {
        const int slack = safe_h - laid.content_h;
        laid.lines.back().h += slack;
        laid.content_h = safe_h;
    }
    return laid;
}

inline int ClampPrincipleScroll(int y, int content_h, int view_h) {
    const int max_y = (std::max)(0, content_h - view_h);
    if (y < 0) {
        return 0;
    }
    if (y > max_y) {
        return max_y;
    }
    return y;
}

inline int PrincipleScrollDelta(int wheel_delta) {
    return -wheel_delta * chart_theme::kDesignWheelStep / 120;
}

}  // namespace design_principles
}  // namespace nara
