using System.Text.Json;
using NaraCollector.Desktop.Models;
using System.IO;

namespace NaraCollector.Desktop.Services;

public sealed record CrowdingAnalysisOptions(
    double BinWidth = 0.001,
    double? MinRate = null,
    double? MaxRate = null);

public sealed record CrowdingAnalysisMetadata(
    int AllNoticeCount,
    int CompleteNoticeCount,
    int ExcludedNoticeCount,
    int AllRowCount,
    int CompleteRowCount,
    int NegativeRankExcludedRowCount);

public sealed record CrowdingBandRow(
    int BandIndex,
    double BandStart,
    double BandEnd,
    string BandLabel,
    int BidCount,
    int NoticeCount,
    int WinnerCount,
    double ExpectedWins,
    double AvgNoticeAdjustRate,
    double MedianNoticeAdjustRate,
    double AvgBidAdjustRate,
    double MedianBidAdjustRate,
    double ActualWinRate,
    double ExpectedWinRate,
    double WinLift,
    string CrowdingTier);

public sealed record CrowdingTierRow(
    string CrowdingTier,
    int ZoneCount,
    int TotalBidCount,
    int TotalWinnerCount,
    double TotalExpectedWins,
    double AverageBandBidCount,
    double AverageAdjustRate,
    double ActualWinRate,
    double ExpectedWinRate,
    double WinLift);

public sealed record NoticeSizeRow(
    string ParticipantTier,
    int NoticeCount,
    double AverageParticipantCount,
    double MedianParticipantCount,
    double AverageNoticeAdjustRate,
    double MedianNoticeAdjustRate,
    double NoticeAdjustRateStdDev,
    double AverageAwardAdjustRate,
    double MedianAwardAdjustRate,
    double ExpectedWinProbability);

public sealed record CrowdingKeyFindings(
    int CompleteNoticeCount,
    int ExcludedNoticeCount,
    double? CrowdedBandLift,
    double? SparseBandLift,
    double? CrowdedNoticeExpectedWinRate,
    double? SparseNoticeExpectedWinRate,
    double? CrowdedNoticeAdjustRateMean,
    double? SparseNoticeAdjustRateMean);

public sealed record CrowdingAnalysisResult(
    CrowdingAnalysisMetadata Metadata,
    double ResolvedMinRate,
    double ResolvedMaxRate,
    IReadOnlyList<CrowdingBandRow> BandTable,
    IReadOnlyList<CrowdingTierRow> CrowdingSummary,
    IReadOnlyList<NoticeSizeRow> NoticeSizeSummary,
    CrowdingKeyFindings KeyFindings);

public sealed class CrowdingAnalysisService
{
    public CrowdingAnalysisResult Analyze(IEnumerable<ParticipantRecord> participants, CrowdingAnalysisOptions? options = null)
    {
        var config = options ?? new CrowdingAnalysisOptions();
        var rows = participants
            .Where(x => !string.IsNullOrWhiteSpace(x.NoticeNumber))
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => new Row(
                NoticeNumber: x.NoticeNumber.Trim(),
                Rank: x.Rank,
                CompanyName: x.NormalizedCompanyName,
                BidAdjustRate: x.AdjustRate!.Value,
                NoticeAdjustRate: x.PriceAdjustmentRate ?? 0.0))
            .ToList();

        var winnerCounts = rows
            .GroupBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Count(x => x.Rank == 1),
                StringComparer.OrdinalIgnoreCase);

        var completeNoticeIds = winnerCounts
            .Where(x => x.Value == 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var completeRows = rows
            .Where(x => completeNoticeIds.Contains(x.NoticeNumber))
            .ToList();
        var validRows = completeRows
            .Where(x => x.Rank >= 1)
            .ToList();

        var participantsPerNotice = validRows
            .GroupBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var preparedRows = validRows
            .Select(x => new PreparedRow(
                x.NoticeNumber,
                x.Rank,
                x.CompanyName,
                x.BidAdjustRate,
                x.NoticeAdjustRate,
                participantsPerNotice[x.NoticeNumber],
                1.0 / participantsPerNotice[x.NoticeNumber],
                x.Rank == 1))
            .ToList();

        var metadata = new CrowdingAnalysisMetadata(
            AllNoticeCount: rows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            CompleteNoticeCount: completeNoticeIds.Count,
            ExcludedNoticeCount: rows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() - completeNoticeIds.Count,
            AllRowCount: rows.Count,
            CompleteRowCount: preparedRows.Count,
            NegativeRankExcludedRowCount: completeRows.Count - preparedRows.Count);

        if (preparedRows.Count == 0)
        {
            return new CrowdingAnalysisResult(metadata, config.MinRate ?? 99.5, config.MaxRate ?? 100.0, [], [], [], new CrowdingKeyFindings(0, metadata.ExcludedNoticeCount, null, null, null, null, null, null));
        }

        var resolvedMinRate = config.MinRate ?? Math.Floor(preparedRows.Min(x => x.BidAdjustRate) * 1000) / 1000.0;
        var resolvedMaxRate = config.MaxRate ?? Math.Ceiling(preparedRows.Max(x => x.BidAdjustRate) * 1000) / 1000.0;

        var binnedRows = preparedRows
            .Where(x => x.BidAdjustRate >= resolvedMinRate && x.BidAdjustRate <= resolvedMaxRate)
            .Select(x =>
            {
                var bandIndex = (int)Math.Floor(((x.BidAdjustRate - resolvedMinRate) / config.BinWidth) + 1e-12);
                var bandStart = Math.Round(resolvedMinRate + (bandIndex * config.BinWidth), 6);
                var bandEnd = Math.Round(bandStart + config.BinWidth, 6);
                return new BinnedRow(x, bandIndex, bandStart, bandEnd, $"{bandStart:F3}~{bandEnd:F3}");
            })
            .ToList();

        var bandCounts = binnedRows.Select(x => (double)x.BandIndex).ToList();
        var bandTable = binnedRows
            .GroupBy(x => new { x.BandIndex, x.BandStart, x.BandEnd, x.BandLabel })
            .Select(g =>
            {
                var bidCount = g.Count();
                var winnerCount = g.Count(x => x.Row.IsWinner);
                var expectedWins = g.Sum(x => x.Row.ExpectedWinProbability);
                var actualWinRate = bidCount == 0 ? 0.0 : winnerCount / (double)bidCount;
                var expectedWinRate = bidCount == 0 ? 0.0 : expectedWins / bidCount;
                return new CrowdingBandRow(
                    BandIndex: g.Key.BandIndex,
                    BandStart: g.Key.BandStart,
                    BandEnd: g.Key.BandEnd,
                    BandLabel: g.Key.BandLabel,
                    BidCount: bidCount,
                    NoticeCount: g.Select(x => x.Row.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    WinnerCount: winnerCount,
                    ExpectedWins: expectedWins,
                    AvgNoticeAdjustRate: AnalysisMath.SafeMean(g.Select(x => x.Row.NoticeAdjustRate)),
                    MedianNoticeAdjustRate: AnalysisMath.Median(g.Select(x => x.Row.NoticeAdjustRate).ToList()),
                    AvgBidAdjustRate: AnalysisMath.SafeMean(g.Select(x => x.Row.BidAdjustRate)),
                    MedianBidAdjustRate: AnalysisMath.Median(g.Select(x => x.Row.BidAdjustRate).ToList()),
                    ActualWinRate: actualWinRate,
                    ExpectedWinRate: expectedWinRate,
                    WinLift: expectedWinRate <= 0 ? 0.0 : actualWinRate / expectedWinRate,
                    CrowdingTier: string.Empty);
            })
            .OrderBy(x => x.BandStart)
            .ToList();

        var bidCountValues = bandTable.Select(x => (double)x.BidCount).ToList();
        var classifiedBandTable = bandTable
            .Select(x => x with
            {
                CrowdingTier = AnalysisMath.ClassifyTertile(x.BidCount, bidCountValues, "적은구간", "중간구간", "많은구간"),
            })
            .ToList();

        var tierOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["적은구간"] = 0,
            ["중간구간"] = 1,
            ["많은구간"] = 2,
        };

        var crowdingSummary = classifiedBandTable
            .GroupBy(x => x.CrowdingTier, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var totalBids = g.Sum(x => x.BidCount);
                var totalWinners = g.Sum(x => x.WinnerCount);
                var totalExpectedWins = g.Sum(x => x.ExpectedWins);
                var actualWinRate = totalBids == 0 ? 0.0 : totalWinners / (double)totalBids;
                var expectedWinRate = totalBids == 0 ? 0.0 : totalExpectedWins / totalBids;
                return new CrowdingTierRow(
                    CrowdingTier: g.Key,
                    ZoneCount: g.Count(),
                    TotalBidCount: totalBids,
                    TotalWinnerCount: totalWinners,
                    TotalExpectedWins: totalExpectedWins,
                    AverageBandBidCount: g.Average(x => x.BidCount),
                    AverageAdjustRate: g.Average(x => x.AvgNoticeAdjustRate),
                    ActualWinRate: actualWinRate,
                    ExpectedWinRate: expectedWinRate,
                    WinLift: expectedWinRate <= 0 ? 0.0 : actualWinRate / expectedWinRate);
            })
            .OrderBy(x => tierOrder.TryGetValue(x.CrowdingTier, out var order) ? order : 99)
            .ToList();

        var winnerRows = preparedRows.Where(x => x.IsWinner).ToList();
        var participantCounts = winnerRows.Select(x => (double)x.ParticipantCount).ToList();
        var participantTierRows = winnerRows
            .Select(x => new
            {
                Row = x,
                Tier = AnalysisMath.ClassifyTertile(x.ParticipantCount, participantCounts, "적음", "중간", "많음"),
            })
            .ToList();
        var noticeTierOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["적음"] = 0,
            ["중간"] = 1,
            ["많음"] = 2,
        };
        var noticeSizeSummary = participantTierRows
            .GroupBy(x => x.Tier, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var participantValues = g.Select(x => (double)x.Row.ParticipantCount).ToList();
                var noticeAdjustValues = g.Select(x => x.Row.NoticeAdjustRate).ToList();
                var awardAdjustValues = g.Select(x => x.Row.BidAdjustRate).ToList();
                var avgParticipants = participantValues.Average();
                return new NoticeSizeRow(
                    ParticipantTier: g.Key,
                    NoticeCount: g.Select(x => x.Row.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    AverageParticipantCount: avgParticipants,
                    MedianParticipantCount: AnalysisMath.Median(participantValues),
                    AverageNoticeAdjustRate: AnalysisMath.SafeMean(noticeAdjustValues),
                    MedianNoticeAdjustRate: AnalysisMath.Median(noticeAdjustValues),
                    NoticeAdjustRateStdDev: AnalysisMath.PopulationStandardDeviation(noticeAdjustValues),
                    AverageAwardAdjustRate: AnalysisMath.SafeMean(awardAdjustValues),
                    MedianAwardAdjustRate: AnalysisMath.Median(awardAdjustValues),
                    ExpectedWinProbability: avgParticipants <= 0 ? 0.0 : 1.0 / avgParticipants);
            })
            .OrderBy(x => noticeTierOrder.TryGetValue(x.ParticipantTier, out var order) ? order : 99)
            .ToList();

        CrowdingTierRow? crowded = crowdingSummary.FirstOrDefault(x => x.CrowdingTier == "많은구간");
        CrowdingTierRow? sparse = crowdingSummary.FirstOrDefault(x => x.CrowdingTier == "적은구간");
        NoticeSizeRow? crowdedNotice = noticeSizeSummary.FirstOrDefault(x => x.ParticipantTier == "많음");
        NoticeSizeRow? sparseNotice = noticeSizeSummary.FirstOrDefault(x => x.ParticipantTier == "적음");

        var findings = new CrowdingKeyFindings(
            metadata.CompleteNoticeCount,
            metadata.ExcludedNoticeCount,
            crowded?.WinLift,
            sparse?.WinLift,
            crowdedNotice?.ExpectedWinProbability,
            sparseNotice?.ExpectedWinProbability,
            crowdedNotice?.AverageNoticeAdjustRate,
            sparseNotice?.AverageNoticeAdjustRate);

        return new CrowdingAnalysisResult(
            metadata,
            resolvedMinRate,
            resolvedMaxRate,
            classifiedBandTable,
            crowdingSummary,
            noticeSizeSummary,
            findings);
    }

    public void SaveSummary(CrowdingAnalysisResult result, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var payload = new
        {
            metadata = result.Metadata,
            resolved_range = new { min = result.ResolvedMinRate, max = result.ResolvedMaxRate },
            key_findings = new
            {
                complete_notice_count = result.KeyFindings.CompleteNoticeCount,
                excluded_notice_count = result.KeyFindings.ExcludedNoticeCount,
                crowded_band_lift = result.KeyFindings.CrowdedBandLift,
                sparse_band_lift = result.KeyFindings.SparseBandLift,
                crowded_notice_expected_win_rate = result.KeyFindings.CrowdedNoticeExpectedWinRate,
                sparse_notice_expected_win_rate = result.KeyFindings.SparseNoticeExpectedWinRate,
                crowded_notice_adjust_rate_mean = result.KeyFindings.CrowdedNoticeAdjustRateMean,
                sparse_notice_adjust_rate_mean = result.KeyFindings.SparseNoticeAdjustRateMean,
            },
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputDirectory, "summary.json"), json);
    }

    private sealed record Row(string NoticeNumber, int Rank, string CompanyName, double BidAdjustRate, double NoticeAdjustRate);

    private sealed record PreparedRow(
        string NoticeNumber,
        int Rank,
        string CompanyName,
        double BidAdjustRate,
        double NoticeAdjustRate,
        int ParticipantCount,
        double ExpectedWinProbability,
        bool IsWinner);

    private sealed record BinnedRow(PreparedRow Row, int BandIndex, double BandStart, double BandEnd, string BandLabel);
}
