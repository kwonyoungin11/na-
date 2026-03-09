using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.ViewModels;

internal sealed class MainWindowSnapshotCoordinator
{
    private readonly Dictionary<string, DashboardSnapshot> _snapshotCache = new(StringComparer.Ordinal);

    public bool TryGetSnapshot(DashboardFilter filter, out DashboardSnapshot? snapshot)
    {
        return _snapshotCache.TryGetValue(BuildCacheKey(filter), out snapshot);
    }

    public void StoreSnapshot(DashboardFilter filter, DashboardSnapshot snapshot)
    {
        _snapshotCache[BuildCacheKey(filter)] = snapshot;
    }

    private static string BuildCacheKey(DashboardFilter filter)
    {
        return string.Join(
            "|",
            filter.StartDate.ToString("yyyyMMdd"),
            filter.EndDate.ToString("yyyyMMdd"),
            NormalizeCacheToken(filter.Region),
            NormalizeCacheToken(filter.Keyword),
            NormalizeCacheToken(filter.OrderingAgency),
            NormalizeCacheToken(filter.DataType),
            NormalizeCacheToken(filter.Company),
            filter.MinAdjustRate?.ToString("F5") ?? string.Empty,
            filter.MaxAdjustRate?.ToString("F5") ?? string.Empty);
    }

    private static string NormalizeCacheToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
