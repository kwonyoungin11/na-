#pragma once

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

    bool operator==(const ScatterWorldCacheKey& o) const {
        return prepare == o.prepare && cols == o.cols && rows == o.rows && cell_milli == o.cell_milli
               && dpi_milli == o.dpi_milli;
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
inline bool BounceOverlaySkipsLabels() { return false; }
inline bool PresentFromOffscreenFrame() { return true; }

// 동국 칩만 천천히 통통. Cosine ease so edges do not sparkle.
constexpr float kDonggukBounceAmp = 0.12f;
constexpr int kDonggukBouncePeriodMs = 2200;
constexpr float kDonggukBounceLift = 3.5f;

inline float DonggukBounceAmount(unsigned elapsed_ms) {
    const unsigned period = static_cast<unsigned>(kDonggukBouncePeriodMs);
    const float t = static_cast<float>(elapsed_ms % period) / static_cast<float>(period);
    return 0.5f * (1.0f - std::cos(6.2831853f * t));
}

inline float DonggukBounceScale(unsigned elapsed_ms) {
    return 1.0f + kDonggukBounceAmp * DonggukBounceAmount(elapsed_ms);
}

inline float DonggukBounceLiftPx(unsigned elapsed_ms) {
    return -kDonggukBounceLift * DonggukBounceAmount(elapsed_ms);
}

inline bool ShouldRaiseHoveredRate(bool has_previous, double previous, double next, double precision) {
    if (!has_previous) {
        return true;
    }
    const double step = precision > 0.0 ? precision : 0.01;
    return std::fabs(previous - next) >= (step / 2.0);
}

inline int ChromeHot(int hot_company, bool hot_reset, bool hot_all) {
    return (hot_company & 0xFFFF) | (hot_reset ? 0x10000 : 0) | (hot_all ? 0x20000 : 0);
}

}  // namespace scatter_cache
}  // namespace nara
