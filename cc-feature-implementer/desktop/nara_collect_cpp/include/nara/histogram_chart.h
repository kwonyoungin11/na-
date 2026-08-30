#pragma once

#include "nara/chart_theme.h"
#include "nara/igunsul_convert.h"
#include "nara/scatter_chart.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace nara {

struct HistogramBinSpec {
    double min_rate = 99.0;
    double max_rate = 101.0;
    int bin_count = chart_theme::kHistogramBinCount;
};

struct HistogramSeries {
    std::string key;
    std::string short_label;
    uint32_t argb = 0;
    std::vector<int> counts;
    int total = 0;
};

struct HistogramModel {
    HistogramBinSpec spec;
    HistogramSeries market;
    std::vector<HistogramSeries> companies;
    int market_max = 0;
    int company_max = 0;
};

struct IsoPoint {
    float x = 0;
    float y = 0;
};

struct HistBarGeom {
    IsoPoint front[4];
    IsoPoint top[4];
    IsoPoint side[4];
};

struct HistScene {
    float origin_x = 0;
    float origin_y = 0;
    float bar_w = 8;
    float bar_d = 10;
    float gap = 2;
    float series_gap = 6;
    float cluster_gap = 1.5f;
    int clusters = 1;
    float depth_x = chart_theme::kHistDepthX;
    float depth_y = chart_theme::kHistDepthY;
    float max_h = 100;
};

inline double HistogramBinStep(const HistogramBinSpec& spec) {
    if (spec.bin_count <= 0 || !(spec.max_rate > spec.min_rate)) {
        return 0.01;
    }
    return (spec.max_rate - spec.min_rate) / static_cast<double>(spec.bin_count);
}

inline int HistogramBinIndex(double rate, const HistogramBinSpec& spec) {
    if (spec.bin_count <= 0 || !(spec.max_rate > spec.min_rate)) {
        return -1;
    }
    if (rate < spec.min_rate || rate > spec.max_rate) {
        return -1;
    }
    const double step = HistogramBinStep(spec);
    if (!(step > 0.0)) {
        return -1;
    }
    int idx = static_cast<int>((rate - spec.min_rate) / step);
    if (idx >= spec.bin_count) {
        idx = spec.bin_count - 1;
    }
    if (idx < 0) {
        idx = 0;
    }
    return idx;
}

inline double HistogramBinStart(int index, const HistogramBinSpec& spec) {
    return spec.min_rate + HistogramBinStep(spec) * static_cast<double>((std::max)(0, index));
}

inline double HistogramBinEnd(int index, const HistogramBinSpec& spec) {
    if (index >= spec.bin_count - 1) {
        return spec.max_rate;
    }
    return HistogramBinStart(index + 1, spec);
}

inline bool HistogramIsDongguk(const std::string& key) {
    return CanonicalizeCompany(key).find("동국문화재연구원") != std::string::npos;
}

inline uint32_t HistogramCompanyArgb(const std::string& key) {
    const auto canon = CanonicalizeCompany(key);
    if (HistogramIsDongguk(canon)) {
        return chart_theme::kDonggukArgb;
    }
    int32_t hash = 23;
    for (unsigned char ch : canon) {
        hash = static_cast<int32_t>(hash * 31 + static_cast<int32_t>(ch));
    }
    const int n = static_cast<int>(chart_theme::kCompanyPalette.size());
    int idx = n == 0 ? 0 : hash % n;
    if (idx < 0) {
        idx = -idx;
    }
    return chart_theme::kCompanyPalette[static_cast<size_t>(idx % n)];
}

inline std::vector<std::string> SelectedCompanyKeys(const std::vector<std::string>& keys,
                                                    const std::vector<char>& checked) {
    bool any = false;
    for (char flag : checked) {
        if (flag) {
            any = true;
            break;
        }
    }
    if (!any) {
        return {};
    }
    std::vector<std::string> selected;
    selected.reserve(keys.size());
    const size_t n = (std::min)(keys.size(), checked.size());
    for (size_t i = 0; i < n; ++i) {
        if (checked[i]) {
            selected.push_back(keys[i]);
        }
    }
    return selected;
}

inline HistogramSeries MakeSeries(const std::string& key, int bin_count) {
    HistogramSeries series;
    series.key = CanonicalizeCompany(key);
    series.short_label = BuildShortLabel(series.key.empty() ? key : series.key);
    series.argb = HistogramCompanyArgb(series.key.empty() ? std::string("전체") : series.key);
    series.counts.assign(static_cast<size_t>((std::max)(0, bin_count)), 0);
    return series;
}

inline int SeriesPeak(const HistogramSeries& series) {
    int peak = 0;
    for (int count : series.counts) {
        if (count > peak) {
            peak = count;
        }
    }
    return peak;
}

inline void TrimCompanySeries(std::vector<HistogramSeries>& companies, int max_series) {
    if (max_series <= 0) {
        companies.clear();
        return;
    }
    if (static_cast<int>(companies.size()) <= max_series) {
        return;
    }
    std::stable_sort(companies.begin(), companies.end(),
                     [](const HistogramSeries& a, const HistogramSeries& b) { return a.total > b.total; });
    int dongguk = -1;
    for (int i = 0; i < static_cast<int>(companies.size()); ++i) {
        if (HistogramIsDongguk(companies[static_cast<size_t>(i)].key)) {
            dongguk = i;
            break;
        }
    }
    if (dongguk >= max_series) {
        std::swap(companies[static_cast<size_t>(max_series - 1)], companies[static_cast<size_t>(dongguk)]);
    }
    companies.resize(static_cast<size_t>(max_series));
}

inline HistogramModel BuildHistogram(const std::vector<ParticipantRow>& chips, const HistogramBinSpec& spec,
                                    const std::vector<std::string>& selected_keys, int max_company_series) {
    HistogramModel model;
    model.spec = spec;
    const int bins = (std::max)(1, spec.bin_count);
    model.market = MakeSeries("전체", bins);
    model.market.key.clear();
    model.market.short_label = "전체";
    model.market.argb = chart_theme::kHistMarketMidArgb;

    std::unordered_map<std::string, int> index;
    index.reserve(selected_keys.size() * 2 + 1);
    model.companies.reserve(selected_keys.size());
    for (const auto& raw : selected_keys) {
        const auto key = CanonicalizeCompany(raw);
        if (key.empty() || index.find(key) != index.end()) {
            continue;
        }
        index.emplace(key, static_cast<int>(model.companies.size()));
        model.companies.push_back(MakeSeries(key, bins));
    }

    for (const auto& chip : chips) {
        if (!chip.adjust_rate) {
            continue;
        }
        const int bin = HistogramBinIndex(*chip.adjust_rate, spec);
        if (bin < 0) {
            continue;
        }
        model.market.counts[static_cast<size_t>(bin)] += 1;
        model.market.total += 1;
        if (index.empty()) {
            continue;
        }
        const auto key = CompanyIdentity(chip);
        const auto found = index.find(key);
        if (found == index.end()) {
            continue;
        }
        auto& series = model.companies[static_cast<size_t>(found->second)];
        series.counts[static_cast<size_t>(bin)] += 1;
        series.total += 1;
    }

    TrimCompanySeries(model.companies, max_company_series);
    model.market_max = SeriesPeak(model.market);
    model.company_max = 0;
    for (const auto& series : model.companies) {
        model.company_max = (std::max)(model.company_max, SeriesPeak(series));
    }
    return model;
}

inline int HistogramAxisMax(const HistogramModel& model) {
    if (!model.companies.empty()) {
        return (std::max)(1, model.company_max);
    }
    return (std::max)(1, model.market_max);
}

inline bool HistogramDrawsMarketBars(const HistogramModel& model) { return model.companies.empty(); }

inline int HistogramVisibleSeriesCount(const HistogramModel& model) {
    if (model.companies.empty()) {
        return 1;
    }
    return static_cast<int>(model.companies.size());
}

inline float HistogramBarHeight(int count, int axis_max, float max_h) {
    if (count <= 0 || axis_max <= 0) {
        return 0.0f;
    }
    return (std::max)(3.0f, max_h * (static_cast<float>(count) / static_cast<float>(axis_max)));
}

inline std::vector<ParticipantRow> CollectHistogramChips(const std::vector<ScatterNotice>& notices, bool rank1_only) {
    std::vector<ParticipantRow> chips;
    size_t n = 0;
    for (const auto& notice : notices) {
        n += notice.chips.size();
    }
    chips.reserve(n);
    for (const auto& notice : notices) {
        for (const auto& chip : notice.chips) {
            if (rank1_only && !IsMappedRank1(chip, notice.chips)) {
                continue;
            }
            chips.push_back(chip);
        }
    }
    return chips;
}

inline HistScene LayoutHistScene(float plot_x, float plot_y, float plot_w, float plot_h, int bin_count,
                                 int cluster_count) {
    HistScene scene;
    scene.depth_x = chart_theme::kHistDepthX;
    scene.depth_y = chart_theme::kHistDepthY;
    scene.series_gap = chart_theme::kHistSeriesGap;
    scene.clusters = (std::max)(1, cluster_count);
    const int bins = (std::max)(1, bin_count);
    const float inner_w = (std::max)(40.0f, plot_w - static_cast<float>(chart_theme::kHistPadL + chart_theme::kHistPadR));
    const float inner_h = (std::max)(40.0f, plot_h - static_cast<float>(chart_theme::kHistPadT + chart_theme::kHistPadB));
    scene.bar_d = (std::min)(16.0f, inner_h * 0.11f);
    const float z_span = scene.bar_d;
    const float z_screen_x = z_span * scene.depth_x;
    const float z_screen_y = z_span * scene.depth_y;
    scene.gap = (std::max)(1.0f, inner_w * chart_theme::kHistBarGap / static_cast<float>(bins + 2));
    scene.bar_w =
        (std::max)(3.0f, (inner_w - z_screen_x - scene.gap * static_cast<float>(bins - 1)) / static_cast<float>(bins));
    scene.cluster_gap = (std::max)(1.0f, scene.bar_w * 0.08f);
    scene.max_h = (std::max)(20.0f, inner_h - z_screen_y - 6.0f);
    scene.origin_x = plot_x + static_cast<float>(chart_theme::kHistPadL);
    scene.origin_y = plot_y + plot_h - static_cast<float>(chart_theme::kHistPadB);
    return scene;
}

inline IsoPoint ProjectIso(float x, float y, float z, const HistScene& scene) {
    return {scene.origin_x + x + z * scene.depth_x, scene.origin_y - y - z * scene.depth_y};
}

inline HistBarGeom MakeHistBar(float x, float z, float w, float d, float h, const HistScene& scene) {
    HistBarGeom bar;
    const float hh = (std::max)(0.0f, h);
    bar.front[0] = ProjectIso(x, 0.0f, z, scene);
    bar.front[1] = ProjectIso(x + w, 0.0f, z, scene);
    bar.front[2] = ProjectIso(x + w, hh, z, scene);
    bar.front[3] = ProjectIso(x, hh, z, scene);
    bar.top[0] = ProjectIso(x, hh, z, scene);
    bar.top[1] = ProjectIso(x + w, hh, z, scene);
    bar.top[2] = ProjectIso(x + w, hh, z + d, scene);
    bar.top[3] = ProjectIso(x, hh, z + d, scene);
    bar.side[0] = ProjectIso(x + w, 0.0f, z, scene);
    bar.side[1] = ProjectIso(x + w, 0.0f, z + d, scene);
    bar.side[2] = ProjectIso(x + w, hh, z + d, scene);
    bar.side[3] = ProjectIso(x + w, hh, z, scene);
    return bar;
}

inline float HistBinX(int bin, const HistScene& scene) {
    return static_cast<float>(bin) * (scene.bar_w + scene.gap);
}

inline float HistSeriesZ(int series, const HistScene& scene) {
    return static_cast<float>(series) * (scene.bar_d + scene.series_gap);
}

inline float HistClusterBarW(const HistScene& scene) {
    const int n = (std::max)(1, scene.clusters);
    const float inner = (std::max)(2.0f, scene.bar_w - scene.cluster_gap * static_cast<float>(n - 1));
    return (std::max)(2.0f, inner / static_cast<float>(n));
}

inline float HistClusterX(int bin, int series, const HistScene& scene) {
    const float w = HistClusterBarW(scene);
    const int s = (std::max)(0, series);
    return HistBinX(bin, scene) + static_cast<float>(s) * (w + scene.cluster_gap);
}

inline bool HistClusterOverlaps(int bin, int a, int b, const HistScene& scene) {
    if (a == b) {
        return true;
    }
    const float w = HistClusterBarW(scene);
    const float xa = HistClusterX(bin, a, scene);
    const float xb = HistClusterX(bin, b, scene);
    return xa < xb + w && xb < xa + w;
}

inline int HistogramHitBin(float mouse_x, float mouse_y, const HistScene& scene, int bin_count) {
    if (bin_count <= 0) {
        return -1;
    }
    if (mouse_y > scene.origin_y + 10.0f) {
        return -1;
    }
    const float stride = scene.bar_w + scene.gap;
    if (!(stride > 0.0f)) {
        return -1;
    }
    const float x = mouse_x - scene.origin_x;
    if (x < 0.0f) {
        return -1;
    }
    const int bin = static_cast<int>(x / stride);
    if (bin < 0 || bin >= bin_count) {
        return -1;
    }
    if (x > HistBinX(bin, scene) + scene.bar_w + scene.bar_d * scene.depth_x) {
        return -1;
    }
    return bin;
}

}  // namespace nara
