using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class CollectorNoticeRules
{
    public const int CollectLookbackMonths = 12;
    public const int CollectLookbackMonthsMax = 12;

    public static int CollectLookbackMonthsFor(DateTime start, DateTime end)
    {
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Day < start.Day)
        {
            months -= 1;
        }

        if (months < 1)
        {
            months = 1;
        }

        return months > CollectLookbackMonthsMax ? CollectLookbackMonthsMax : months;
    }

    public static DateTime CollectWindowStart(DateTime now, int? months = null)
    {
        return now.AddMonths(-(months ?? CollectLookbackMonths));
    }

    public static DateTime AwardRetentionStart(DateTime now, int? months = null)
    {
        return now.Date.AddMonths(-(months ?? CollectLookbackMonths));
    }

    public const int MinParticipatingCompanies = 6;

    public static IReadOnlyList<ParticipantRecord> DropNoticesWithTooFewCompanies(
        IReadOnlyList<ParticipantRecord> rows)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var allowed = rows
            .GroupBy(NoticeKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Where(g => g
                .Select(CompanyKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() >= MinParticipatingCompanies)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(x => allowed.Contains(NoticeKey(x)))
            .ToList();
    }

    public static IReadOnlyList<ParticipantRecord> DropExcludedNoticeNames(
        IReadOnlyList<ParticipantRecord> rows)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        return rows.Where(x => !IsExcludedNoticeName(x.NoticeName)).ToList();
    }

    public static IReadOnlyList<ParticipantRecord> KeepDonggukParticipatingNotices(
        IReadOnlyList<ParticipantRecord> rows)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var donggukNotices = rows
            .Where(IsDonggukCompany)
            .Select(NoticeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows.Where(x => donggukNotices.Contains(NoticeKey(x))).ToList();
    }

    public static IReadOnlyList<ParticipantRecord> ApplyPersistRules(IReadOnlyList<ParticipantRecord> rows)
    {
        return DropNoticesWithTooFewCompanies(DropExcludedNoticeNames(rows));
    }

    public static IReadOnlyList<ParticipantRecord> ApplyChartRules(IReadOnlyList<ParticipantRecord> rows)
    {
        return KeepDonggukParticipatingNotices(DropExcludedNoticeNames(rows));
    }

    public static bool IsExcludedNoticeName(string? noticeName)
    {
        return !string.IsNullOrWhiteSpace(noticeName)
            && noticeName.Contains("영향진단", StringComparison.Ordinal);
    }

    public static bool IsDonggukCompany(ParticipantRecord row)
    {
        var name = string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName;
        var canonical = CompanyNamePresentation.Canonicalize(name);
        return canonical.Contains("동국문화재연구원", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> FindMissingAwardNoticeNumbers(
        IEnumerable<ParticipantRecord> naraRows,
        IEnumerable<string> awardNoticeNumbers)
    {
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in naraRows)
        {
            var key = NoticeKey(row);
            if (!string.IsNullOrWhiteSpace(key))
            {
                have.Add(key);
                have.Add(StripNoticeSuffix(key));
            }
        }

        return awardNoticeNumbers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => !have.Contains(x) && !have.Contains(StripNoticeSuffix(x)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string StripNoticeSuffix(string noticeNumber)
    {
        var text = noticeNumber.Trim();
        var dash = text.LastIndexOf('-');
        if (dash <= 0)
        {
            return text;
        }

        var suffix = text[(dash + 1)..];
        return suffix.All(char.IsDigit) && suffix.Length <= 3 ? text[..dash] : text;
    }

    private static string NoticeKey(ParticipantRecord row)
    {
        var value = string.IsNullOrWhiteSpace(row.NoticeNumber) ? row.NoticeNo : row.NoticeNumber;
        return value.Trim();
    }

    private static string CompanyKey(ParticipantRecord row)
    {
        var value = string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName;
        return value.Trim();
    }

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

    public static DashboardFilter ToCollectorViewFilter(DashboardFilter filter)
    {
        var region = (filter.Region ?? string.Empty).Trim();
        if (string.Equals(region, "경북", StringComparison.OrdinalIgnoreCase)
            || string.Equals(region, "대구", StringComparison.OrdinalIgnoreCase))
        {
            return filter;
        }

        return filter with { Region = "전체" };
    }

    public static IReadOnlyList<CollectorNoticeRowModel> ResolveDonggukAwardNoticeRows(
        IReadOnlyList<CollectorNoticeRowModel> donggukRows,
        IReadOnlyList<CollectorNoticeRowModel>? collectorAwardRows)
    {
        return donggukRows;
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
        var start = AwardRetentionStart(now);

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
