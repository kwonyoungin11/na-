#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace nara {
namespace scatter_cache {

// Mirrors NaraCollector.Desktop.Services.ScatterRenderCache.

struct ScatterWorldCacheKey {
    int prepare = 0;
    int cols = 0;
    int rows = 0;
    int cell_milli = 0;
    int dpi_milli = 0;
    int filter_hash = 0;

    bool operator==(const ScatterWorldCacheKey& o) const {
        return prepare == o.prepare && cols == o.cols && rows == o.rows && cell_milli == o.cell_milli
               && dpi_milli == o.dpi_milli && filter_hash == o.filter_hash;
    }
    bool operator!=(const ScatterWorldCacheKey& o) const { return !(*this == o); }
};

struct ScatterViewCacheKey {
    int prepare = 0;
    int pixel_w = 0;
    int pixel_h = 0;
    int scale_milli = 0;
    int scroll_x = 0;
    int scroll_y = 0;
    int plot_w = 0;
    int plot_h = 0;
    int cols = 0;
    int rows = 0;
    int filter_hash = 0;
    int chrome_hot = 0;

    bool operator==(const ScatterViewCacheKey& o) const {
        return prepare == o.prepare && pixel_w == o.pixel_w && pixel_h == o.pixel_h && scale_milli == o.scale_milli
               && scroll_x == o.scroll_x && scroll_y == o.scroll_y && plot_w == o.plot_w && plot_h == o.plot_h
               && cols == o.cols && rows == o.rows && filter_hash == o.filter_hash && chrome_hot == o.chrome_hot;
    }
    bool operator!=(const ScatterViewCacheKey& o) const { return !(*this == o); }
};

inline bool CanReuseWorldLayer(const ScatterWorldCacheKey& cached, const ScatterWorldCacheKey& current) {
    return cached == current;
}

inline bool ShouldReuseWorldLayerDuringViewChange(const ScatterWorldCacheKey& cached,
                                                 const ScatterWorldCacheKey& current) {
    return CanReuseWorldLayer(cached, current);
}

inline bool IsOverlayOnlyFrame(bool has_view_layer, const ScatterViewCacheKey& cached,
                               const ScatterViewCacheKey& current) {
    return has_view_layer && cached == current;
}

// Overlay blit copies the static layer as-is. Chip labels must already be in that layer.
inline bool StaticLayerIncludesChipLabels() { return true; }
inline bool SkipChipLabelsDuringPan() { return false; }
inline bool CanCacheStaticLayer(bool panning) { return !panning; }

// Pan is a GPU/CPU blit of a labeled overscan bitmap, not a full DirectWrite rebuild.
inline bool CanBlitPlotCacheDuringPan() { return true; }

inline int PlotCacheOverscanDip(int plot_span) { return (std::max)(120, plot_span); }

inline bool PlotCacheCoversView(int origin, int cache_span, int view_origin, int view_span) {
    return view_origin >= origin && view_origin + view_span <= origin + cache_span;
}

inline bool CanStretchPlotCacheDuringZoom() { return true; }
inline int ZoomSettleMs() { return 90; }

inline int ZoomSrcSpan(int dest_span, int old_cell, int new_cell) {
    if (new_cell <= 0) {
        return dest_span;
    }
    return static_cast<int>(std::llround(static_cast<double>(dest_span) * static_cast<double>(old_cell)
                                         / static_cast<double>(new_cell)));
}

inline int ZoomSrcOrigin(int axis, int origin, int new_scroll, int old_cell, int new_cell) {
    if (new_cell <= 0) {
        return axis - origin;
    }
    return axis + static_cast<int>(std::llround(static_cast<double>(new_scroll) * static_cast<double>(old_cell)
                                                / static_cast<double>(new_cell)))
           - origin;
}
inline bool BounceOverlaySkipsLabels() { return false; }
inline bool PresentFromOffscreenFrame() { return true; }

// Bounce off: 50ms overlay rebuild lags the map and scaled 동국 covers neighbor labels.
inline bool DonggukBounceEnabled() { return false; }
constexpr float kDonggukBounceAmp = 0.0f;
constexpr int kDonggukBouncePeriodMs = 2200;
constexpr float kDonggukBounceLift = 0.0f;

inline float DonggukBounceAmount(unsigned) { return 0.0f; }

inline float DonggukBounceScale(unsigned) { return 1.0f; }

inline float DonggukBounceLiftPx(unsigned) { return 0.0f; }

inline bool ShouldRaiseHoveredRate(bool has_previous, double previous, double next, double precision) {
    if (!has_previous) {
        return true;
    }
    const double step = precision > 0.0 ? precision : 0.01;
    return std::fabs(previous - next) >= (step / 2.0);
}

inline int ChromeHot(int hot_company, bool hot_reset, bool hot_all, bool hot_rank1 = false, int hot_tab = -1) {
    return (hot_company & 0xFFFF) | (hot_reset ? 0x10000 : 0) | (hot_all ? 0x20000 : 0) | (hot_rank1 ? 0x40000 : 0)
           | (((hot_tab + 1) & 0x7) << 19);
}

}  // namespace scatter_cache
}  // namespace nara
