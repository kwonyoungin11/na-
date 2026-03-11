using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

internal static class NoticeAdjustRateTrendBuilder
{
    public static IReadOnlyList<TrendPointModel> BuildPoints(IReadOnlyList<NoticeRowModel> notices, DateTime? nowOverride = null)
    {
        return BuildRecentRows(notices, nowOverride)
            .Select(x => new TrendPointModel(
                x.OpenDate.ToString("MM-dd", CultureInfo.InvariantCulture),
                x.AdjustRate))
            .ToList();
    }

    public static string BuildSummary(IReadOnlyList<NoticeRowModel> notices, DateTime? nowOverride = null)
    {
        var recentRows = BuildRecentRows(notices, nowOverride);
        if (recentRows.Count == 0)
        {
            return "날짜순 최근 1개월 낙찰공고 · 데이터 대기";
        }

        return $"날짜순 최근 1개월 낙찰공고 · {recentRows[0].OpenDate:yyyy-MM-dd}~{recentRows[^1].OpenDate:yyyy-MM-dd} · 공고 {recentRows.Count:N0}건";
    }

    private static List<NoticeRowModel> BuildRecentRows(IReadOnlyList<NoticeRowModel> notices, DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.Now;
        var cutoff = now.Date.AddMonths(-1);
        var orderedRows = notices
            .Where(x => x.IsAwarded)
            .Where(x => x.OpenDate > DateTime.MinValue)
            .Where(x => x.OpenDate >= cutoff && x.OpenDate < now.Date.AddDays(1))
            .OrderBy(x => x.OpenDate)
            .ToList();
        return orderedRows;
    }
}
