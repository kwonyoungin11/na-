#include "nara/scatter_cache.h"

#include <cmath>
#include <iostream>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    using nara::scatter_cache::CanReuseWorldLayer;
    using nara::scatter_cache::ChromeHot;
    using nara::scatter_cache::IsOverlayOnlyFrame;
    using nara::scatter_cache::ScatterViewCacheKey;
    using nara::scatter_cache::ScatterWorldCacheKey;
    using nara::scatter_cache::ShouldRaiseHoveredRate;
    using nara::scatter_cache::ShouldReuseWorldLayerDuringViewChange;

    ScatterWorldCacheKey world{12, 201, 40, 20000, 1250};
    if (!CanReuseWorldLayer(world, world)) {
        return Fail("identical world keys reuse the chip layer");
    }
    ScatterWorldCacheKey world2 = world;
    world2.prepare = 13;
    if (CanReuseWorldLayer(world, world2)) {
        return Fail("prepare version must bust the world layer");
    }
    world2 = world;
    world2.rows = 41;
    if (CanReuseWorldLayer(world, world2)) {
        return Fail("row count must bust the world layer");
    }
    if (!ShouldReuseWorldLayerDuringViewChange(world, world)) {
        return Fail("same world key reuses during pan/zoom of the view layer");
    }

    if (!ShouldRaiseHoveredRate(false, 0, 99.500, 0.01)) {
        return Fail("first hover always raises");
    }
    if (ShouldRaiseHoveredRate(true, 99.500, 99.501, 0.01)) {
        return Fail("0.01 step must ignore 0.001 noise");
    }
    if (!ShouldRaiseHoveredRate(true, 99.500, 99.520, 0.01)) {
        return Fail("crossing a 0.01 cell must raise");
    }
    if (!ShouldRaiseHoveredRate(true, 99.500, 99.495, 0.001)) {
        return Fail("0.001 precision raises on 0.005 move");
    }

    ScatterViewCacheKey key{1, 1800, 900, 1000, 120, 120, 1600, 820, 201, 39, 0, 0};
    if (!IsOverlayOnlyFrame(true, key, key)) {
        return Fail("matching view layer is overlay-only even if the cursor moved");
    }
    if (IsOverlayOnlyFrame(false, key, key)) {
        return Fail("no cached bitmap cannot be overlay-only");
    }
    ScatterViewCacheKey panned = key;
    panned.scroll_x = 200;
    if (IsOverlayOnlyFrame(true, key, panned)) {
        return Fail("pan must rebuild the view layer");
    }
    ScatterViewCacheKey next = key;
    next.prepare = 2;
    if (IsOverlayOnlyFrame(true, key, next)) {
        return Fail("filter/data prepare must rebuild the view layer");
    }
    if (ChromeHot(-1, false, false) == ChromeHot(3, true, false)) {
        return Fail("dock hover is part of the chrome key");
    }
    if (!nara::scatter_cache::StaticLayerIncludesChipLabels()) {
        return Fail("hover overlay must blit chips that already have 업체명");
    }
    if (nara::scatter_cache::SkipChipLabelsDuringPan()) {
        return Fail("skipping labels while panning bakes empty chips into the overlay cache");
    }
    if (nara::scatter_cache::CanCacheStaticLayer(true)) {
        return Fail("do not cache a drag frame; overlay would keep chips without 업체명");
    }
    if (!nara::scatter_cache::CanCacheStaticLayer(false)) {
        return Fail("idle frames with labels may be cached");
    }
    if (!nara::scatter_cache::CanBlitPlotCacheDuringPan()) {
        return Fail("pan must blit a labeled overscan bitmap, not rebuild DirectWrite chips");
    }
    if (nara::scatter_cache::PlotCacheOverscanDip(700) < 700) {
        return Fail("overscan is at least one extra viewport so a drag does not rebuild");
    }
    if (!nara::scatter_cache::PlotCacheCoversView(100, 900, 250, 400)) {
        return Fail("view inside overscan reuses the plot bitmap");
    }
    if (nara::scatter_cache::PlotCacheCoversView(100, 900, 800, 400)) {
        return Fail("view past the cache must rebuild");
    }
    if (!nara::scatter_cache::CanStretchPlotCacheDuringZoom()) {
        return Fail("zoom must StretchBlt the last crisp plot, not rebuild chips per wheel tick");
    }
    if (nara::scatter_cache::ZoomSettleMs() < 50 || nara::scatter_cache::ZoomSettleMs() > 200) {
        return Fail("rebuild after the wheel stops, ~90ms");
    }
    if (nara::scatter_cache::ZoomSrcSpan(700, 20, 22) != 636) {
        return Fail("zoom-in source is dest * old_cell / new_cell");
    }
    if (nara::scatter_cache::ZoomSrcOrigin(0, 100, 250, 20, 22) != 127) {
        return Fail("zoom source origin keeps the cursor world point");
    }
    ScatterWorldCacheKey filtered = world;
    filtered.filter_hash = 9;
    if (CanReuseWorldLayer(world, filtered)) {
        return Fail("1위/업체 filter must bust the plot cache");
    }
    if (nara::scatter_cache::BounceOverlaySkipsLabels()) {
        return Fail("bounced 동국 overlay covers the static chip, so it must draw 업체명");
    }
    if (!nara::scatter_cache::PresentFromOffscreenFrame()) {
        return Fail("compose bounce offscreen then blit once, or the map blinks");
    }
    using nara::scatter_cache::DonggukBounceLiftPx;
    using nara::scatter_cache::DonggukBounceScale;
    using nara::scatter_cache::kDonggukBounceAmp;
    using nara::scatter_cache::kDonggukBouncePeriodMs;
    if (kDonggukBouncePeriodMs < 2000) {
        return Fail("동국 bounce should stay slow");
    }
    if (kDonggukBounceAmp < 0.10f || kDonggukBounceAmp > 0.15f) {
        return Fail("bounce must be visible without sparkling");
    }
    if (std::fabs(DonggukBounceScale(0) - 1.0f) > 0.001f) {
        return Fail("bounce starts at rest scale 1");
    }
    if (DonggukBounceScale(kDonggukBouncePeriodMs / 2) <= DonggukBounceScale(0)) {
        return Fail("mid-cycle scale pops above rest");
    }
    if (DonggukBounceScale(kDonggukBouncePeriodMs / 2) > 1.0f + kDonggukBounceAmp + 0.001f) {
        return Fail("bounce amplitude cap");
    }
    const float quarter = DonggukBounceScale(kDonggukBouncePeriodMs / 4);
    const float mid = DonggukBounceScale(kDonggukBouncePeriodMs / 2);
    if (quarter >= mid - 0.001f) {
        return Fail("cosine ease must peak once, not a sharp abs-sin flash");
    }
    const float lift = DonggukBounceLiftPx(kDonggukBouncePeriodMs / 2);
    if (lift > -2.5f || lift < -4.5f) {
        return Fail("lift stays a few pixels so the square does not glitter");
    }

    std::cerr << "ok scatter cache matches ScatterRenderCache\n";
    return 0;
}
