namespace NaraCollector.Desktop.Services;

public readonly record struct ScatterWorldCacheKey(
    int PrepareVersion,
    int ColumnCount,
    int RowCount,
    int CellSizeMilli,
    int DpiMilli);

public readonly record struct ScatterViewCacheKey(
    int PrepareVersion,
    int PixelWidth,
    int PixelHeight,
    int ScaleMilli,
    int OffsetXMilli,
    int OffsetYMilli,
    int PlotWidth,
    int PlotHeight,
    int ColumnCount,
    int RowCount);

public static class ScatterRenderCache
{
    public static bool CanReuseWorldLayer(ScatterWorldCacheKey cached, ScatterWorldCacheKey current)
        => cached == current;

    public static bool ShouldReuseWorldLayerDuringViewChange(ScatterWorldCacheKey cached, ScatterWorldCacheKey current)
        => CanReuseWorldLayer(cached, current);

    public static bool IsOverlayOnlyFrame(bool hasViewLayer, ScatterViewCacheKey cached, ScatterViewCacheKey current)
        => hasViewLayer && cached == current;

    public static bool ShouldRaiseHoveredRate(double? previous, double next, double precision)
    {
        if (!previous.HasValue)
        {
            return true;
        }

        var step = precision > 0 ? precision : 0.01;
        return Math.Abs(previous.Value - next) >= (step / 2.0);
    }
}
