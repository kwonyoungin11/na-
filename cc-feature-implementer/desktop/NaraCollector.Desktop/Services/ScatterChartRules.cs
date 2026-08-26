using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public readonly record struct ScatterNotice(
    string NoticeNumber,
    int ChipCount,
    int DonggukRank,
    int TotalCompanies,
    IReadOnlyList<ParticipantRecord> Chips);

public static class ScatterChartRules
{
    public static ScatterNotice BuildNotice(
        IReadOnlyList<ParticipantRecord> rows,
        string noticeNumber,
        double minRate,
        double maxRate)
    {
        var noticeRows = rows
            .Where(x => NoticeKey(x).Equals(noticeNumber, StringComparison.OrdinalIgnoreCase)
                || CollectorNoticeRules.StripNoticeSuffix(NoticeKey(x))
                    .Equals(CollectorNoticeRules.StripNoticeSuffix(noticeNumber), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var unique = UniqueCompanies(noticeRows);
        var chips = unique
            .Where(x => x.AdjustRate.HasValue && x.AdjustRate.Value >= minRate && x.AdjustRate.Value <= maxRate)
            .ToList();
        var donggukRank = 0;
        for (var i = 0; i < chips.Count; i++)
        {
            if (CollectorNoticeRules.IsDonggukCompany(chips[i]))
            {
                donggukRank = i + 1;
                break;
            }
        }

        return new ScatterNotice(noticeNumber, chips.Count, donggukRank, unique.Count, chips);
    }

    public static IReadOnlyList<ParticipantRecord> InPeriod(
        IReadOnlyList<ParticipantRecord> rows,
        DateTime startDate,
        DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return rows
            .Where(x => x.OpenDate.Date >= start && x.OpenDate.Date <= end)
            .ToList();
    }

    public static double? FromBidRateVsLowerLimit(double bidRatePercent, double lowerRatePercent)
    {
        if (lowerRatePercent == 0)
        {
            return null;
        }

        return Math.Round(bidRatePercent / lowerRatePercent * 100.0, 5, MidpointRounding.AwayFromZero);
    }

    public static double ToChartAdjustRate(double raw, double rank1Raw, double rank1Chart)
    {
        if (raw >= 99.0 && raw <= 101.0)
        {
            return raw;
        }

        if (Math.Abs(raw) <= 10.0)
        {
            return Math.Round(100.0 + raw, 5, MidpointRounding.AwayFromZero);
        }

        return Math.Round(raw + (rank1Chart - rank1Raw), 5, MidpointRounding.AwayFromZero);
    }

    public static List<ParticipantRecord> SortOverlappingChips(IEnumerable<ParticipantRecord> bucket)
    {
        return bucket
            .OrderBy(x => CollectorNoticeRules.IsDonggukCompany(x) ? 1 : 0)
            .ThenBy(x => x.IsAwarded || x.Rank == 1 ? 1 : 0)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatDonggukRankLabel(ScatterNotice scatter, double donggukRate, double awardRate)
    {
        return $"동국 {donggukRate:F5} / {scatter.DonggukRank}위 · {scatter.ChipCount}개사 · 낙찰 {awardRate:F5}";
    }

    private static List<ParticipantRecord> UniqueCompanies(IReadOnlyList<ParticipantRecord> rows)
    {
        var unique = new Dictionary<string, ParticipantRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = CompanyKey(row);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!unique.TryGetValue(key, out var existing)
                || (row.Rank > 0 && (existing.Rank <= 0 || row.Rank < existing.Rank)))
            {
                unique[key] = row;
            }
        }

        return unique.Values
            .OrderBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(CompanyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NoticeKey(ParticipantRecord row)
    {
        return string.IsNullOrWhiteSpace(row.NoticeNumber) ? row.NoticeNo : row.NoticeNumber;
    }

    private static string CompanyKey(ParticipantRecord row)
    {
        var name = string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName;
        return CompanyNamePresentation.Canonicalize(name);
    }
}
