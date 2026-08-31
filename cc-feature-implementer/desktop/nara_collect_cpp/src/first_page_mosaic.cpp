#include "nara/first_page_mosaic.h"

#include "nara/chart_theme.h"
#include "nara/dwrite_text.h"
#include "nara/first_page_dash.h"
#include "nara/notice_rules.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <string>
#include <vector>

namespace nara {
namespace {

constexpr int kRowH = 28;
constexpr int kHeaderH = 24;

std::wstring Utf8ToWide(const std::string& text) {
    if (text.empty()) {
        return {};
    }
    const int count = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring wide(count > 0 ? count - 1 : 0, L'\0');
    if (count > 1) {
        MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, wide.data(), count);
    }
    return wide;
}

void RoundRectPath(Gdiplus::GraphicsPath& path, Gdiplus::RectF r, float radius) {
    const float rad = (std::min)(radius, (std::min)(r.Width, r.Height) / 2.0f);
    if (rad < 0.5f) {
        path.AddRectangle(r);
        return;
    }
    const float d = rad * 2.0f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
    path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
    path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
    path.CloseFigure();
}

void FillRoundCard(Gdiplus::Graphics& g, const chart_theme::RectI& box, float radius) {
    Gdiplus::RectF r(static_cast<Gdiplus::REAL>(box.x), static_cast<Gdiplus::REAL>(box.y),
                     static_cast<Gdiplus::REAL>(box.w), static_cast<Gdiplus::REAL>(box.h));
    Gdiplus::GraphicsPath path;
    RoundRectPath(path, r, radius);
    Gdiplus::SolidBrush white(Gdiplus::Color(255, 255, 255, 255));
    g.FillPath(&white, &path);
}

Gdiplus::RectF DipBox(const chart_theme::RectI& box) {
    return {static_cast<Gdiplus::REAL>(box.x), static_cast<Gdiplus::REAL>(box.y),
            static_cast<Gdiplus::REAL>(box.w), static_cast<Gdiplus::REAL>(box.h)};
}

void PanelHead(const chart_theme::RectI& panel, const wchar_t* title, const wchar_t* cap) {
    dwrite_text::QueueBox(static_cast<float>(panel.x + 10), static_cast<float>(panel.y + 6),
                          static_cast<float>(panel.w - 20), 16, title, 12.0f, chart_theme::kTextArgb,
                          dwrite_text::Align::Near, dwrite_text::Align::Near, true);
    if (cap && cap[0]) {
        dwrite_text::QueueBox(static_cast<float>(panel.x + 10), static_cast<float>(panel.y + 22),
                              static_cast<float>(panel.w - 20), 14, cap, 10.0f, chart_theme::kSubtleTextArgb,
                              dwrite_text::Align::Near, dwrite_text::Align::Near, false, true, true);
    }
}

chart_theme::RectI PlotInner(const chart_theme::RectI& panel, int top) {
    return {panel.x + 10, panel.y + top, (std::max)(8, panel.w - 20), (std::max)(8, panel.h - top - 8)};
}

void DrawSpark(Gdiplus::Graphics& g, const chart_theme::RectI& box, const std::vector<double>& data, uint32_t argb) {
    if (data.size() < 2) {
        return;
    }
    double lo = data[0];
    double hi = data[0];
    for (double v : data) {
        lo = (std::min)(lo, v);
        hi = (std::max)(hi, v);
    }
    const double span = hi - lo > 1e-9 ? hi - lo : 1.0;
    std::vector<Gdiplus::PointF> pts;
    pts.reserve(data.size());
    for (size_t i = 0; i < data.size(); ++i) {
        const float x = static_cast<float>(box.x)
                        + static_cast<float>(i) / static_cast<float>(data.size() - 1) * static_cast<float>(box.w);
        const float y = static_cast<float>(box.y + box.h)
                        - static_cast<float>((data[i] - lo) / span) * static_cast<float>(box.h);
        pts.push_back({x, y});
    }
    Gdiplus::Pen pen(Gdiplus::Color(argb), 1.4f);
    g.DrawLines(&pen, pts.data(), static_cast<INT>(pts.size()));
}

float PlotBottom(const chart_theme::RectI& plot) {
    return static_cast<float>(plot.y + plot.h - chart_theme::kMosaicXLabelH);
}

void DrawYGrid(Gdiplus::Graphics& g, const chart_theme::RectI& plot, double ymin, double ymax, int digits) {
    const float left = static_cast<float>(plot.x + chart_theme::kMosaicAxisPad);
    const float right = static_cast<float>(plot.x + plot.w);
    const float top = static_cast<float>(plot.y);
    const float bot = PlotBottom(plot);
    Gdiplus::Pen grid{Gdiplus::Color(chart_theme::kMosaicGridArgb), 1.0f};
    Gdiplus::Pen axis{Gdiplus::Color(chart_theme::kMosaicAxisArgb), 1.0f};
    for (int i = 0; i < chart_theme::kMosaicYTickN; ++i) {
        const double v = chart_theme::MosaicYTick(ymin, ymax, i);
        const float t = static_cast<float>(i) / static_cast<float>(chart_theme::kMosaicYTickN - 1);
        const float y = top + t * (bot - top);
        g.DrawLine(&grid, left, y, right, y);
        wchar_t buf[24];
        if (digits <= 0) {
            swprintf_s(buf, L"%.0f", v);
        } else {
            swprintf_s(buf, L"%.*f", digits, v);
        }
        dwrite_text::QueueBox(static_cast<float>(plot.x), y - 7, static_cast<float>(chart_theme::kMosaicAxisPad - 3), 14,
                              buf, 9.0f, chart_theme::kSubtleTextArgb, dwrite_text::Align::Far,
                              dwrite_text::Align::Center, false);
    }
    g.DrawLine(&axis, left, bot, right, bot);
}

void DrawVBars(Gdiplus::Graphics& g, const chart_theme::RectI& plot, const int* values, int n, int home, int peak,
               const std::vector<std::wstring>& xlabels) {
    int maxn = 1;
    for (int i = 0; i < n; ++i) {
        maxn = (std::max)(maxn, values[i]);
    }
    DrawYGrid(g, plot, 0, static_cast<double>(maxn), 0);
    const float left = static_cast<float>(plot.x + chart_theme::kMosaicAxisPad);
    const float inner_w = static_cast<float>((std::max)(8, plot.w - chart_theme::kMosaicAxisPad));
    const float gap = 3.0f;
    const float bw = (inner_w - gap * static_cast<float>(n - 1)) / static_cast<float>(n);
    const float bot = PlotBottom(plot);
    const float usable = (std::max)(8.0f, bot - static_cast<float>(plot.y));
    for (int i = 0; i < n; ++i) {
        const float bh = (std::max)(2.0f, static_cast<float>(values[i]) * usable / static_cast<float>(maxn));
        const float x = left + static_cast<float>(i) * (bw + gap);
        const float y = bot - bh;
        const uint32_t fill = i == home ? 0xFF0E7490u : i == peak ? 0xFF334155u : 0xFFCBD5E1u;
        Gdiplus::SolidBrush brush{Gdiplus::Color(fill)};
        g.FillRectangle(&brush, x, y, bw, bh);
        if (values[i] > 0) {
            wchar_t buf[12];
            swprintf_s(buf, L"%d", values[i]);
            dwrite_text::QueueBox(x, y - 12, bw, 12, buf, 9.0f, chart_theme::kTextArgb, dwrite_text::Align::Center,
                                  dwrite_text::Align::Near, true);
        }
        std::wstring lab;
        if (i < static_cast<int>(xlabels.size())) {
            lab = xlabels[static_cast<size_t>(i)];
        }
        if (!lab.empty()) {
            dwrite_text::QueueBox(x - 2, bot + 1, bw + 4, 12, lab.c_str(), 8.0f,
                                  i == home ? 0xFF0E7490u : chart_theme::kSubtleTextArgb, dwrite_text::Align::Center,
                                  dwrite_text::Align::Near, true);
        }
    }
}

void DrawHBars(Gdiplus::Graphics& g, const chart_theme::RectI& plot, const std::vector<NamedCount>& rows,
               const uint32_t* colors, int color_n) {
    if (rows.empty()) {
        return;
    }
    int maxn = 1;
    for (const auto& row : rows) {
        maxn = (std::max)(maxn, row.n);
    }
    const float name_w = 36.0f;
    const float left = static_cast<float>(plot.x) + name_w;
    const float right = static_cast<float>(plot.x + plot.w - 28);
    const float top = static_cast<float>(plot.y + 12);
    const float bot = static_cast<float>(plot.y + plot.h);
    Gdiplus::Pen grid{Gdiplus::Color(chart_theme::kMosaicGridArgb), 1.0f};
    for (int i = 0; i < chart_theme::kMosaicYTickN; ++i) {
        const float t = static_cast<float>(i) / static_cast<float>(chart_theme::kMosaicYTickN - 1);
        const float x = left + t * (right - left);
        const double v = chart_theme::MosaicYTick(0, static_cast<double>(maxn), chart_theme::kMosaicYTickN - 1 - i);
        g.DrawLine(&grid, x, top, x, bot);
        wchar_t buf[12];
        swprintf_s(buf, L"%.0f", v);
        dwrite_text::QueueBox(x - 16, static_cast<float>(plot.y), 32, 12, buf, 9.0f, chart_theme::kSubtleTextArgb,
                              dwrite_text::Align::Center, dwrite_text::Align::Near, false);
    }
    const float rh = (bot - top) / static_cast<float>(rows.size());
    for (size_t i = 0; i < rows.size(); ++i) {
        const float y = top + static_cast<float>(i) * rh + 2.0f;
        const float h = (std::max)(6.0f, rh - 5.0f);
        const float bar_w = (right - left) * static_cast<float>(rows[i].n) / static_cast<float>(maxn);
        const uint32_t fill = colors[static_cast<int>(i) % color_n];
        Gdiplus::SolidBrush brush{Gdiplus::Color(fill)};
        g.FillRectangle(&brush, left, y, bar_w, h);
        dwrite_text::QueueBox(static_cast<float>(plot.x), y, name_w - 2, h, Utf8ToWide(rows[i].label).c_str(), 11.0f,
                              chart_theme::kTextArgb, dwrite_text::Align::Near, dwrite_text::Align::Center, true, true,
                              true);
        wchar_t buf[12];
        swprintf_s(buf, L"%d", rows[i].n);
        dwrite_text::QueueBox(left + bar_w + 4, y, 28, h, buf, 11.0f, chart_theme::kTextArgb, dwrite_text::Align::Near,
                              dwrite_text::Align::Center, true);
    }
}

void MapSeries(const chart_theme::RectI& plot, const std::vector<double>& xs, double ymin, double ymax,
               std::vector<Gdiplus::PointF>& pts) {
    pts.clear();
    if (xs.size() < 2) {
        return;
    }
    const float left = static_cast<float>(plot.x + chart_theme::kMosaicAxisPad);
    const float span = static_cast<float>((std::max)(8, plot.w - chart_theme::kMosaicAxisPad));
    const float top = static_cast<float>(plot.y);
    const float bot = PlotBottom(plot);
    const float usable = (std::max)(8.0f, bot - top);
    pts.reserve(xs.size());
    for (size_t i = 0; i < xs.size(); ++i) {
        const float x = left + static_cast<float>(i) / static_cast<float>(xs.size() - 1) * span;
        const float y = bot - static_cast<float>((xs[i] - ymin) / (ymax - ymin + 1e-9)) * usable;
        pts.push_back({x, y});
    }
}

void DrawXLabels(const chart_theme::RectI& plot, const std::vector<std::wstring>& xlabels) {
    if (xlabels.size() < 2) {
        return;
    }
    const float left = static_cast<float>(plot.x + chart_theme::kMosaicAxisPad);
    const float span = static_cast<float>((std::max)(8, plot.w - chart_theme::kMosaicAxisPad));
    const float y = PlotBottom(plot) + 1;
    const size_t n = xlabels.size();
    const size_t step = n > 8 ? (n - 1) / 6 : 1;
    for (size_t i = 0; i < n; ++i) {
        if (i != 0 && i != n - 1 && (i % step) != 0) {
            continue;
        }
        const float x = left + static_cast<float>(i) / static_cast<float>(n - 1) * span;
        dwrite_text::QueueBox(x - 12, y, 24, 12, xlabels[i].c_str(), 8.0f, chart_theme::kSubtleTextArgb,
                              dwrite_text::Align::Center, dwrite_text::Align::Near, false);
    }
}

void DrawLines(Gdiplus::Graphics& g, const chart_theme::RectI& plot, const std::vector<double>& a, uint32_t ca,
               const std::vector<double>& b, uint32_t cb, double ymin, double ymax,
               const std::vector<std::wstring>& xlabels) {
    DrawYGrid(g, plot, ymin, ymax, 2);
    auto stroke = [&](const std::vector<double>& xs, uint32_t argb) {
        std::vector<Gdiplus::PointF> pts;
        MapSeries(plot, xs, ymin, ymax, pts);
        if (pts.size() < 2) {
            return;
        }
        Gdiplus::Pen pen{Gdiplus::Color(argb), 1.6f};
        g.DrawLines(&pen, pts.data(), static_cast<INT>(pts.size()));
    };
    stroke(a, ca);
    stroke(b, cb);
    DrawXLabels(plot, xlabels);
}

void DrawArea(Gdiplus::Graphics& g, const chart_theme::RectI& plot, const std::vector<double>& xs, double ymin,
              double ymax, uint32_t argb, const std::vector<std::wstring>& xlabels) {
    DrawYGrid(g, plot, ymin, ymax, 2);
    std::vector<Gdiplus::PointF> pts;
    MapSeries(plot, xs, ymin, ymax, pts);
    if (pts.size() < 2) {
        return;
    }
    const float left = pts.front().X;
    const float right = pts.back().X;
    const float base = PlotBottom(plot);
    Gdiplus::GraphicsPath path;
    path.AddLines(pts.data(), static_cast<INT>(pts.size()));
    path.AddLine(right, base, left, base);
    path.CloseFigure();
    Gdiplus::SolidBrush fill{Gdiplus::Color((argb & 0x00FFFFFFu) | 0x29000000u)};
    g.FillPath(&fill, &path);
    Gdiplus::Pen pen{Gdiplus::Color(argb), 1.5f};
    g.DrawLines(&pen, pts.data(), static_cast<INT>(pts.size()));
    DrawXLabels(plot, xlabels);
}

void DrawDonut(Gdiplus::Graphics& g, const chart_theme::RectI& plot, int a, int b, int c) {
    const int sum = (std::max)(1, a + b + c);
    const float cx = static_cast<float>(plot.x + 44);
    const float cy = static_cast<float>(plot.y + plot.h / 2);
    const float r = (std::min)(34.0f, static_cast<float>(plot.h) / 2.0f - 4.0f);
    auto slice = [&](int n, uint32_t argb, float start) {
        const float sweep = 360.0f * static_cast<float>(n) / static_cast<float>(sum);
        if (n > 0) {
            Gdiplus::SolidBrush brush{Gdiplus::Color(argb)};
            g.FillPie(&brush, cx - r, cy - r, r * 2, r * 2, start - 90.0f, sweep);
        }
        return start + sweep;
    };
    float ang = 0;
    ang = slice(a, 0xFFCA8A04u, ang);
    ang = slice(b, 0xFF0E7490u, ang);
    slice(c, 0xFF94A3B8u, ang);
    Gdiplus::SolidBrush hole(Gdiplus::Color(255, 255, 255, 255));
    g.FillEllipse(&hole, cx - r * 0.55f, cy - r * 0.55f, r * 1.1f, r * 1.1f);
    wchar_t l1[32];
    wchar_t l2[32];
    wchar_t l3[32];
    swprintf_s(l1, L"1위  %d", a);
    swprintf_s(l2, L"10위 안  %d", b);
    swprintf_s(l3, L"그 외  %d", c);
    dwrite_text::QueueBox(cx + r + 10, cy - 22, 120, 14, l1, 11.0f, chart_theme::kTextArgb, dwrite_text::Align::Near,
                          dwrite_text::Align::Near, true);
    dwrite_text::QueueBox(cx + r + 10, cy - 6, 120, 14, l2, 11.0f, chart_theme::kTextArgb, dwrite_text::Align::Near,
                          dwrite_text::Align::Near, true);
    dwrite_text::QueueBox(cx + r + 10, cy + 10, 120, 14, l3, 11.0f, chart_theme::kTextArgb, dwrite_text::Align::Near,
                          dwrite_text::Align::Near, true);
}

void DrawMiniDonut(Gdiplus::Graphics& g, float cx, float cy, float r, int rank1, int notice, bool dong) {
    const int all = (std::max)(1, notice);
    Gdiplus::SolidBrush rest{Gdiplus::Color(0xFFCBD5E1u)};
    g.FillEllipse(&rest, cx - r, cy - r, r * 2, r * 2);
    if (rank1 > 0) {
        const float sweep = 360.0f * static_cast<float>(rank1) / static_cast<float>(all);
        Gdiplus::SolidBrush win{Gdiplus::Color(dong ? chart_theme::kDonggukArgb : 0xFFCA8A04u)};
        g.FillPie(&win, cx - r, cy - r, r * 2, r * 2, -90.0f, sweep);
    }
    Gdiplus::SolidBrush hole{Gdiplus::Color(255, 255, 255, 255)};
    g.FillEllipse(&hole, cx - r * 0.52f, cy - r * 0.52f, r * 1.04f, r * 1.04f);
}

void PaintRegion(Gdiplus::Graphics& g, const chart_theme::RectI& slot, const std::string& token) {
    const auto seg = chart_theme::SplitRegionSegment(slot);
    const int on = chart_theme::RegionIndexFromToken(token);
    Gdiplus::RectF outer(static_cast<Gdiplus::REAL>(slot.x), static_cast<Gdiplus::REAL>(slot.y),
                         static_cast<Gdiplus::REAL>(slot.w), static_cast<Gdiplus::REAL>(slot.h));
    Gdiplus::GraphicsPath shell;
    RoundRectPath(shell, outer, static_cast<float>(chart_theme::kRegionSegmentRadius));
    Gdiplus::SolidBrush idle_fill{Gdiplus::Color(chart_theme::kCompanyPanelArgb)};
    g.FillPath(&idle_fill, &shell);
    const Gdiplus::GraphicsState saved = g.Save();
    g.SetClip(&shell);
    const auto& active = on == 0 ? seg.nationwide : seg.gyeongbuk;
    Gdiplus::SolidBrush on_fill{Gdiplus::Color(chart_theme::kAccentSubtleArgb)};
    g.FillRectangle(&on_fill, Gdiplus::RectF(static_cast<Gdiplus::REAL>(active.x), static_cast<Gdiplus::REAL>(active.y),
                                             static_cast<Gdiplus::REAL>(active.w), static_cast<Gdiplus::REAL>(active.h)));
    g.Restore(saved);
    Gdiplus::Pen border{Gdiplus::Color(chart_theme::kComboStrokeArgb), chart_theme::kComboStrokeW};
    g.DrawPath(&border, &shell);
    dwrite_text::QueueBox(static_cast<float>(seg.nationwide.x), static_cast<float>(seg.nationwide.y),
                          static_cast<float>(seg.nationwide.w), static_cast<float>(seg.nationwide.h),
                          chart_theme::RegionLabelAt(0), chart_theme::kFontCaption,
                          on == 0 ? chart_theme::kAccentStrongArgb : chart_theme::kSubtleTextArgb,
                          dwrite_text::Align::Center, dwrite_text::Align::Center, true);
    dwrite_text::QueueBox(static_cast<float>(seg.gyeongbuk.x), static_cast<float>(seg.gyeongbuk.y),
                          static_cast<float>(seg.gyeongbuk.w), static_cast<float>(seg.gyeongbuk.h),
                          chart_theme::RegionLabelAt(1), chart_theme::kFontCaption,
                          on == 1 ? chart_theme::kAccentStrongArgb : chart_theme::kSubtleTextArgb,
                          dwrite_text::Align::Center, dwrite_text::Align::Center, true);
}

}  // namespace

void PaintMosaicFirstPage(Gdiplus::Graphics& g, MosaicPaintIn& in) {
    const auto& table = *in.table;
    const auto dash = BuildFirstPageDash(table, *in.band);
    const auto firms = BuildFirmStats(in.snapshot ? *in.snapshot : std::vector<ParticipantRow>{});
    const auto mosaic = chart_theme::LayoutMainMosaic(in.w, in.h, static_cast<int>(firms.size()));

    dwrite_text::QueueBox(static_cast<float>(mosaic.brand.x), static_cast<float>(mosaic.brand.y + 4),
                          static_cast<float>(mosaic.brand.w), 18, L"동국 낙찰", 15.0f, chart_theme::kTextArgb,
                          dwrite_text::Align::Near, dwrite_text::Align::Near, true);
    const auto span = FormatDataSpan(table);
    dwrite_text::QueueBox(static_cast<float>(mosaic.brand.x), static_cast<float>(mosaic.brand.y + 22),
                          static_cast<float>(mosaic.brand.w), 14, L"낙찰만", 10.0f, chart_theme::kSubtleTextArgb,
                          dwrite_text::Align::Near, dwrite_text::Align::Near, false);

    std::vector<double> spark_n;
    std::vector<double> spark_win;
    std::vector<double> spark_rank;
    for (const auto& m : dash.months) {
        spark_n.push_back(static_cast<double>(m.notice_n));
        spark_win.push_back(static_cast<double>(m.rank1_n));
        spark_rank.push_back(m.rank_median);
    }

    auto paint_kpi = [&](const chart_theme::RectI& box, const wchar_t* caption, const wchar_t* value,
                         const std::vector<double>& spark, uint32_t spark_argb) {
        FillRoundCard(g, box, 8.0f);
        dwrite_text::QueueBox(static_cast<float>(box.x + 8), static_cast<float>(box.y + 3), 70, 12, caption, 10.0f,
                              chart_theme::kSubtleTextArgb, dwrite_text::Align::Near, dwrite_text::Align::Near, false);
        dwrite_text::QueueBox(static_cast<float>(box.x + 8), static_cast<float>(box.y + 15), 70, 16, value, 13.0f,
                              chart_theme::kTextArgb, dwrite_text::Align::Near, dwrite_text::Align::Near, true);
        DrawSpark(g, {box.Right() - 56, box.y + 8, 48, 20}, spark, spark_argb);
    };
    wchar_t notice_n[32];
    swprintf_s(notice_n, L"%d", dash.notice_n);
    std::string latest_kpi = dash.latest_open.size() >= 10 ? dash.latest_open.substr(5, 5) : dash.latest_open;
    wchar_t win_n[32];
    swprintf_s(win_n, L"%d", dash.dong_rank1_n);
    wchar_t med_n[32];
    swprintf_s(med_n, L"%d위", dash.rank_median);
    paint_kpi(mosaic.kpis.notices, L"공고", notice_n, spark_n, 0xFF0E7490u);
    paint_kpi(mosaic.kpis.latest, L"최신", Utf8ToWide(latest_kpi.empty() ? "-" : latest_kpi).c_str(), spark_n,
              0xFF0E7490u);
    paint_kpi(mosaic.kpis.dong_wins, L"동국 1위", win_n, spark_win, 0xFFE11D48u);
    paint_kpi(mosaic.kpis.dong_median, L"중앙", med_n, spark_rank, 0xFF0F172Au);

    dwrite_text::QueueBox(static_cast<float>(mosaic.toolbar.region_label.x),
                          static_cast<float>(mosaic.toolbar.region_label.y),
                          static_cast<float>(mosaic.toolbar.region_label.w),
                          static_cast<float>(mosaic.toolbar.region_label.h), L"지역", 12.0f, 0xFF6B7280u,
                          dwrite_text::Align::Far, dwrite_text::Align::Center, false);
    PaintRegion(g, mosaic.toolbar.region_combo, in.region_token);

    FillRoundCard(g, mosaic.firms, 12.0f);
    {
        wchar_t head[96];
        swprintf_s(head, L"%s 참가가능 · 공고 %d · 업체 %d · 원=1위/참여", Utf8ToWide(in.region_token).c_str(),
                   dash.notice_n, static_cast<int>(firms.size()));
        dwrite_text::QueueBox(static_cast<float>(mosaic.firms.x + 12), static_cast<float>(mosaic.firms.y + 6),
                              static_cast<float>(mosaic.firms.w - 24), 16, head, 13.0f, chart_theme::kTextArgb,
                              dwrite_text::Align::Near, dwrite_text::Align::Near, true);
        const auto span_w = Utf8ToWide(span.empty() ? std::string("개찰일 없음") : span);
        dwrite_text::QueueBox(static_cast<float>(mosaic.firms.x + 12), static_cast<float>(mosaic.firms.y + 24),
                              static_cast<float>(mosaic.firms.w - 24), 16, span_w.c_str(), 13.0f,
                              chart_theme::kAccentStrongArgb, dwrite_text::Align::Near, dwrite_text::Align::Near, true);
        const chart_theme::RectI board{mosaic.firms.x + 8, mosaic.firms.y + 44, mosaic.firms.w - 16,
                                       (std::max)(8, mosaic.firms.h - 50)};
        const auto grid = chart_theme::LayoutFirmGrid(board, static_cast<int>(firms.size()));
        for (int i = 0; i < grid.n; ++i) {
            const auto cell = chart_theme::FirmCellRect(grid, i);
            const auto& firm = firms[static_cast<size_t>(i)];
            const float r = static_cast<float>(chart_theme::kMainFirmDonut) * 0.5f;
            const float cx = static_cast<float>(cell.x + 10) + r;
            const float cy = static_cast<float>(cell.y) + static_cast<float>(cell.h) * 0.5f;
            DrawMiniDonut(g, cx, cy, r, firm.rank1_n, firm.notice_n, firm.dongguk);
            const float tx = cx + r + 8;
            const float tw = static_cast<float>(cell.Right()) - tx - 4;
            const uint32_t name_c = firm.dongguk ? chart_theme::kDonggukArgb : chart_theme::kTextArgb;
            dwrite_text::QueueBox(tx, cy - 16, tw, 16, Utf8ToWide(firm.label).c_str(), 12.0f, name_c,
                                  dwrite_text::Align::Near, dwrite_text::Align::Near, true, true, true);
            wchar_t nums[32];
            swprintf_s(nums, L"참여 %d  1위 %d", firm.notice_n, firm.rank1_n);
            dwrite_text::QueueBox(tx, cy + 2, tw, 14, nums, 10.0f, chart_theme::kSubtleTextArgb,
                                  dwrite_text::Align::Near, dwrite_text::Align::Near, false, true, true);
        }
    }

    FillRoundCard(g, mosaic.table, 12.0f);
    dwrite_text::QueueBox(static_cast<float>(mosaic.table.x + 12), static_cast<float>(mosaic.table.y + 8), 280, 18,
                          L"동국 참여 낙찰공고", 13.0f, chart_theme::kTextArgb, dwrite_text::Align::Near,
                          dwrite_text::Align::Near, true);
    dwrite_text::QueueBox(static_cast<float>(mosaic.table.Right() - 420), static_cast<float>(mosaic.table.y + 10), 400,
                          16, Utf8ToWide(in.status).c_str(), 10.0f, chart_theme::kSubtleTextArgb, dwrite_text::Align::Far,
                          dwrite_text::Align::Near, false, true, true);

    Gdiplus::SolidBrush headerBg(Gdiplus::Color(255, 238, 242, 246));
    Gdiplus::SolidBrush stripe(Gdiplus::Color(255, 248, 250, 252));
    Gdiplus::SolidBrush select(Gdiplus::Color(255, 255, 247, 237));
    const int table_top = mosaic.table_grid.y;
    const int table_bottom = mosaic.table_grid.Bottom();
    if (in.table_rect) {
        *in.table_rect = {mosaic.table_grid.x, table_top, mosaic.table_grid.Right(), table_bottom};
    }
    const int grid_w = mosaic.table_grid.w;
    const int cols[] = {mosaic.table_grid.x,
                        mosaic.table_grid.x + grid_w * 12 / 100,
                        mosaic.table_grid.x + grid_w * 42 / 100,
                        mosaic.table_grid.x + grid_w * 50 / 100,
                        mosaic.table_grid.x + grid_w * 62 / 100,
                        mosaic.table_grid.x + grid_w * 76 / 100,
                        mosaic.table_grid.Right()};
    const wchar_t* headers[] = {L"개찰일", L"공고명", L"지역", L"1위 사정률", L"1위 업체", L"동국순위"};
    g.FillRectangle(&headerBg, mosaic.table_grid.x, table_top, mosaic.table_grid.w, kHeaderH);
    for (int c = 0; c < 6; ++c) {
        dwrite_text::QueueBox(static_cast<float>(cols[c] + 6), static_cast<float>(table_top + 4),
                              static_cast<float>(cols[c + 1] - cols[c] - 10), 16, headers[c], 11.0f, 0xFF6B7280u,
                              dwrite_text::Align::Near, dwrite_text::Align::Near, true);
    }
    g.SetClip(Gdiplus::Rect(mosaic.table_grid.x, table_top + kHeaderH, mosaic.table_grid.w,
                            (std::max)(1, table_bottom - table_top - kHeaderH)));
    const int visible = (std::max)(1, (table_bottom - table_top - kHeaderH) / kRowH);
    const int max_scroll = (std::max)(0, static_cast<int>(table.size()) - visible);
    if (in.scroll) {
        *in.scroll = std::clamp(*in.scroll, 0, max_scroll);
    }
    const int scroll = in.scroll ? *in.scroll : 0;
    for (int i = 0; i < visible; ++i) {
        const int idx = scroll + i;
        if (idx >= static_cast<int>(table.size())) {
            break;
        }
        const int y = table_top + kHeaderH + i * kRowH;
        if (idx == in.selected) {
            g.FillRectangle(&select, mosaic.table_grid.x, y, mosaic.table_grid.w, kRowH);
        } else if (i % 2 == 1) {
            g.FillRectangle(&stripe, mosaic.table_grid.x, y, mosaic.table_grid.w, kRowH);
        }
        const auto& row = table[static_cast<size_t>(idx)];
        char award_txt[24];
        if (row.has_award_rate) {
            std::snprintf(award_txt, sizeof(award_txt), "%.3f", row.award_rate);
        } else {
            std::snprintf(award_txt, sizeof(award_txt), "-");
        }
        std::string day = row.open_date.size() >= 10 ? row.open_date.substr(5, 5) : row.open_date;
        const std::wstring cells[] = {
            Utf8ToWide(day),
            Utf8ToWide(row.notice_name),
            Utf8ToWide(row.region),
            Utf8ToWide(award_txt),
            Utf8ToWide(row.award_company.empty() ? "-" : row.award_company),
            Utf8ToWide(row.rank_label),
        };
        for (int c = 0; c < 6; ++c) {
            Gdiplus::RectF clip(static_cast<Gdiplus::REAL>(cols[c] + 6), static_cast<Gdiplus::REAL>(y + 4),
                                static_cast<Gdiplus::REAL>(cols[c + 1] - cols[c] - 10), 18);
            g.SetClip(clip);
            dwrite_text::QueueBox(clip.X, clip.Y, clip.Width, clip.Height, cells[c].c_str(), 12.0f, 0xFF111111u,
                                  dwrite_text::Align::Near, dwrite_text::Align::Near, false, true, true);
            g.SetClip(Gdiplus::Rect(mosaic.table_grid.x, table_top + kHeaderH, mosaic.table_grid.w,
                                    (std::max)(1, table_bottom - table_top - kHeaderH)));
        }
    }
    g.ResetClip();

    std::string replay = "복기 · 이미 개찰된 결과";
    if (!table.empty() && in.selected >= 0 && in.selected < static_cast<int>(table.size())) {
        const auto& sel = table[static_cast<size_t>(in.selected)];
        char ar[24];
        if (sel.has_award_rate) {
            std::snprintf(ar, sizeof(ar), "%.3f", sel.award_rate);
        } else {
            std::snprintf(ar, sizeof(ar), "-");
        }
        char dr[24];
        std::snprintf(dr, sizeof(dr), "%.3f", sel.dongguk_rate);
        replay = "복기 · 1위 " + (sel.award_company.empty() ? std::string("-") : sel.award_company) + " " + ar
                 + "  · 동국 " + dr + " · " + sel.rank_label;
        std::string cover;
        double cover_r = 0;
        for (const auto& row : *in.snapshot) {
            if (row.notice_number != sel.notice_number || !row.adjust_rate || IsDonggukCompany(row)) {
                continue;
            }
            const double r = *row.adjust_rate;
            if (r + 1e-12 < kWatchHomeLo || r >= kWatchHomeHi) {
                continue;
            }
            if (!BandWatchIsRegular(*in.band, row)) {
                continue;
            }
            if (cover.empty() || r < cover_r) {
                cover = row.company_name.empty() ? row.normalized_company_name : row.company_name;
                cover_r = r;
            }
        }
        if (!cover.empty()) {
            char cr[24];
            std::snprintf(cr, sizeof(cr), "%.3f", cover_r);
            replay += "  · 바로 왼쪽 고정 " + cover + " " + cr;
        }
        replay += "  · 전장 맵에서 칸을 봅니다";
    }
    Gdiplus::RectF replay_r = DipBox(mosaic.replay);
    Gdiplus::GraphicsPath replay_path;
    RoundRectPath(replay_path, replay_r, 10);
    Gdiplus::SolidBrush replay_fill(Gdiplus::Color(255, 15, 23, 42));
    g.FillPath(&replay_fill, &replay_path);
    dwrite_text::QueueBox(replay_r.X + 12, replay_r.Y, replay_r.Width - 24, replay_r.Height, Utf8ToWide(replay).c_str(),
                          11.0f, 0xFFE2E8F0u, dwrite_text::Align::Near, dwrite_text::Align::Center, true, true, true);
}

}  // namespace nara
