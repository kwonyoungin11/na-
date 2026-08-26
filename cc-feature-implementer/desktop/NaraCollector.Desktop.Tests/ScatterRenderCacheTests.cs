using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class ScatterRenderCacheTests
{
    [Fact]
    public void CanReuseWorldLayer_IgnoresPanAndZoom()
    {
        var key = new ScatterWorldCacheKey(12, 201, 40, 20000, 1250);

        Assert.True(ScatterRenderCache.CanReuseWorldLayer(key, key));
        Assert.False(ScatterRenderCache.CanReuseWorldLayer(key, key with { PrepareVersion = 13 }));
        Assert.False(ScatterRenderCache.CanReuseWorldLayer(key, key with { RowCount = 41 }));
        Assert.True(ScatterRenderCache.ShouldReuseWorldLayerDuringViewChange(key, key));
    }

    [Fact]
    public void ShouldRaiseHoveredRate_OnlyWhenRateBucketMoves()
    {
        Assert.True(ScatterRenderCache.ShouldRaiseHoveredRate(null, 99.500, 0.01));
        Assert.False(ScatterRenderCache.ShouldRaiseHoveredRate(99.500, 99.501, 0.01));
        Assert.True(ScatterRenderCache.ShouldRaiseHoveredRate(99.500, 99.520, 0.01));
        Assert.True(ScatterRenderCache.ShouldRaiseHoveredRate(99.500, 99.495, 0.001));
    }

    [Fact]
    public void OverlayOnlyFrame_WhenViewLayerMatches_EvenIfCursorMoved()
    {
        var key = new ScatterViewCacheKey(1, 1800, 900, 1000, 120, 120, 1600, 820, 201, 39);

        Assert.True(ScatterRenderCache.IsOverlayOnlyFrame(hasViewLayer: true, cached: key, current: key));
        Assert.False(ScatterRenderCache.IsOverlayOnlyFrame(hasViewLayer: false, cached: key, current: key));
        Assert.False(ScatterRenderCache.IsOverlayOnlyFrame(hasViewLayer: true, cached: key, current: key with { OffsetXMilli = 200 }));
        Assert.False(ScatterRenderCache.IsOverlayOnlyFrame(hasViewLayer: true, cached: key, current: key with { PrepareVersion = 2 }));
    }
}
