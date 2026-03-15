using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

internal static class DonggukAdjustRateTrendBuilder
{
    private const string DonggukCompanyName = "동국문화재연구원";

    public static IReadOnlyList<TrendPointModel> BuildPoints(IReadOnlyList<ParticipantRecord> participants, DateTime startDate, DateTime endDate)
    {
        return BuildRowsInWindow(participants, startDate, endDate)
            .Select(x => new TrendPointModel(
                x.OpenDate.ToString("MM-dd", CultureInfo.InvariantCulture),
                x.DonggukAdjustRate,
                x.NoticeNumber))
            .ToList();
    }

    public static IReadOnlyList<TrendPointModel> BuildPoints(IReadOnlyList<ParticipantRecord> participants, DateTime? endDateOverride = null)
    {
        var endDate = (endDateOverride ?? DateTime.Today).Date;
        return BuildPoints(participants, endDate.AddMonths(-3), endDate);
    }

    public static IReadOnlyList<TrendPointModel> BuildAwardReferencePoints(IReadOnlyList<ParticipantRecord> participants, DateTime startDate, DateTime endDate)
    {
        return BuildRowsInWindow(participants, startDate, endDate)
            .Select(x => new TrendPointModel(
                x.OpenDate.ToString("MM-dd", CultureInfo.InvariantCulture),
                x.AwardAdjustRate ?? double.NaN,
                x.NoticeNumber))
            .ToList();
    }

    public static IReadOnlyList<TrendPointModel> BuildAwardReferencePoints(IReadOnlyList<ParticipantRecord> participants, DateTime? endDateOverride = null)
    {
        var endDate = (endDateOverride ?? DateTime.Today).Date;
        return BuildAwardReferencePoints(participants, endDate.AddMonths(-3), endDate);
    }

    public static IReadOnlyList<CollectorNoticeRowModel> BuildAwardNoticeRows(IReadOnlyList<ParticipantRecord> participants, DateTime startDate, DateTime endDate)
    {
        return BuildRowsInWindow(participants, startDate, endDate)
            .Where(x => x.AwardAdjustRate.HasValue)
            .OrderByDescending(x => x.OpenDate)
            .Select(x => new CollectorNoticeRowModel(
                NoticeDate: x.OpenDate,
                NoticeNumber: x.NoticeNumber,
                NoticeName: x.NoticeName,
                OrderingAgency: x.OrderingAgency,
                RegionText: x.Region,
                ParticipantCountText: $"동국 {x.DonggukAdjustRate:F5} / {x.DonggukRank}위 · 낙찰 {x.AwardAdjustRate!.Value:F5}"))
            .ToList();
    }

    public static IReadOnlyList<CollectorNoticeRowModel> BuildAwardNoticeRows(IReadOnlyList<ParticipantRecord> participants, DateTime? endDateOverride = null)
    {
        var endDate = (endDateOverride ?? DateTime.Today).Date;
        return BuildAwardNoticeRows(participants, endDate.AddMonths(-3), endDate);
    }

    public static string BuildSummary(IReadOnlyList<ParticipantRecord> participants, DateTime startDate, DateTime endDate)
    {
        var rows = BuildRowsInWindow(participants, startDate, endDate);
        if (rows.Count == 0)
        {
            return $"동국 참여 공고 · {startDate:yyyy-MM-dd}~{endDate:yyyy-MM-dd} · 데이터 대기";
        }

        return $"동국 참여 공고 · {rows[0].OpenDate:yyyy-MM-dd}~{rows[^1].OpenDate:yyyy-MM-dd} · 공고 {rows.Count:N0}건";
    }

    public static string BuildSummary(IReadOnlyList<ParticipantRecord> participants, DateTime? endDateOverride = null)
    {
        var endDate = (endDateOverride ?? DateTime.Today).Date;
        return BuildSummary(participants, endDate.AddMonths(-3), endDate);
    }

    private static List<DonggukTrendRow> BuildRowsInWindow(IReadOnlyList<ParticipantRecord> participants, DateTime startDate, DateTime endDate)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var startBoundary = startDate.Date;
        var endBoundary = endDate.Date;

        return participants
            .Where(IsDonggukParticipant)
            .Where(x => x.AdjustRate.HasValue)
            .Where(x => x.OpenDate.Date >= startBoundary && x.OpenDate.Date <= endBoundary)
            .GroupBy(
                x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var noticeRows = g.ToList();
                var donggukRow = noticeRows
                    .OrderByDescending(x => x.OpenDate)
                    .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                    .First();
                var awardRow = participants
                    .Where(x => string.Equals(
                        string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                        g.Key,
                        StringComparison.OrdinalIgnoreCase))
                    .Where(x => x.AdjustRate.HasValue)
                    .Where(x => x.IsAwarded || x.Rank == 1)
                    .OrderByDescending(x => x.OpenDate)
                    .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                    .FirstOrDefault();
                return new DonggukTrendRow(
                    string.IsNullOrWhiteSpace(donggukRow.NoticeNumber) ? donggukRow.NoticeNo : donggukRow.NoticeNumber,
                    donggukRow.NoticeName,
                    donggukRow.OrderingAgency,
                    string.IsNullOrWhiteSpace(donggukRow.Region) ? (awardRow?.Region ?? string.Empty) : donggukRow.Region,
                    donggukRow.OpenDate,
                    donggukRow.Rank,
                    donggukRow.AdjustRate!.Value,
                    awardRow?.AdjustRate);
            })
            .OrderBy(x => x.OpenDate)
            .ToList();
    }

    private static bool IsDonggukParticipant(ParticipantRecord row)
    {
        var normalized = CompanyNamePresentation.Canonicalize(
            string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName);
        return normalized.Contains(DonggukCompanyName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DonggukTrendRow(
        string NoticeNumber,
        string NoticeName,
        string OrderingAgency,
        string Region,
        DateTime OpenDate,
        int DonggukRank,
        double DonggukAdjustRate,
        double? AwardAdjustRate);
}
