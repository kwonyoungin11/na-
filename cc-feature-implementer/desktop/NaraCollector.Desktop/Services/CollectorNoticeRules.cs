using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class CollectorNoticeRules
{
    public static IReadOnlyList<CollectorNoticeRowModel> ExcludeAwardedBidRows(
        IReadOnlyList<CollectorNoticeRowModel> bidRows,
        IReadOnlyList<CollectorNoticeRowModel> awardRows)
    {
        if (bidRows.Count == 0)
        {
            return bidRows;
        }

        var awardedNoticeNumbers = new HashSet<string>(
            awardRows
                .Where(x => !string.IsNullOrWhiteSpace(x.NoticeNumber))
                .Select(x => x.NoticeNumber),
            StringComparer.OrdinalIgnoreCase);

        return bidRows
            .Where(x => !awardedNoticeNumbers.Contains(x.NoticeNumber))
            .ToList();
    }

    public static List<CollectorNoticeRowModel> CreateVisibleBidRows(
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalRows,
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalAwardRows,
        IReadOnlyList<NoticeRowModel> snapshotRows,
        DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.Now;
        var awardedNoticeNumbers = BuildAwardedNoticeNumberSet(incrementalAwardRows, snapshotRows);

        if (incrementalRows.Count > 0)
        {
            return CollapseDisplayDuplicates(incrementalRows
                .Where(x => x.NoticeDate > now)
                .Where(x => !awardedNoticeNumbers.Contains(x.NoticeNumber))
                .OrderBy(x => x.NoticeDate)
                .ToList());
        }

        return CollapseDisplayDuplicates(snapshotRows
            .Where(x => !x.IsAwarded)
            .Where(x => x.OpenDate > now)
            .Where(x => !awardedNoticeNumbers.Contains(x.NoticeNumber))
            .OrderByDescending(x => x.OpenDate)
            .Select(ToCollectorRow)
            .OrderBy(x => x.NoticeDate)
            .ToList());
    }

    public static List<CollectorNoticeRowModel> CreateVisibleAwardRows(
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalRows,
        IReadOnlyList<NoticeRowModel> snapshotRows,
        DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.Now;
        var start = now.Date.AddMonths(-1);

        if (incrementalRows.Count > 0)
        {
            return incrementalRows
                .Where(x => x.NoticeDate >= start && x.NoticeDate < now.Date.AddDays(1))
                .OrderByDescending(x => x.NoticeDate)
                .ToList();
        }

        return snapshotRows
            .Where(x => x.IsAwarded)
            .Where(x => x.OpenDate >= start && x.OpenDate < now.Date.AddDays(1))
            .OrderByDescending(x => x.OpenDate)
            .Select(ToCollectorRow)
            .ToList();
    }

    private static CollectorNoticeRowModel ToCollectorRow(NoticeRowModel row)
    {
        return new CollectorNoticeRowModel(
            NoticeDate: row.OpenDate,
            NoticeNumber: row.NoticeNumber,
            NoticeName: row.NoticeName,
            OrderingAgency: row.OrderingAgency,
            RegionText: "팝업공고",
            ParticipantCountText: row.AdjustRate.ToString("F5", CultureInfo.InvariantCulture));
    }

    private static HashSet<string> BuildAwardedNoticeNumberSet(
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalAwardRows,
        IReadOnlyList<NoticeRowModel> snapshotRows)
    {
        return new HashSet<string>(
            incrementalAwardRows
                .Where(x => !string.IsNullOrWhiteSpace(x.NoticeNumber))
                .Select(x => x.NoticeNumber)
                .Concat(
                    snapshotRows
                        .Where(x => x.IsAwarded && !string.IsNullOrWhiteSpace(x.NoticeNumber))
                        .Select(x => x.NoticeNumber)),
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<CollectorNoticeRowModel> CollapseDisplayDuplicates(IReadOnlyList<CollectorNoticeRowModel> rows)
    {
        if (rows.Count <= 1)
        {
            return rows.ToList();
        }

        return rows
            .GroupBy(
                x => (
                    x.NoticeDate,
                    NoticeName: NormalizeDisplayKey(x.NoticeName),
                    OrderingAgency: NormalizeDisplayKey(x.OrderingAgency),
                    RegionText: NormalizeDisplayKey(x.RegionText)))
            .Select(group => group
                .OrderByDescending(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(x => x.NoticeDate)
            .ThenBy(x => x.NoticeName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeDisplayKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(c => !char.IsWhiteSpace(c))).Trim();
    }
}
