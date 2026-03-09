using System.Text.Json;
using NaraCollector.Desktop.Models;
using System.IO;

namespace NaraCollector.Desktop.Services;

public sealed record CollisionAnalysisOptions(
    int MinAnchorSamples = 20,
    double? MinRate = null,
    double? MaxRate = null);

public sealed record CollisionAnalysisMetadata(
    int AllNoticeCount,
    int CompleteNoticeCount,
    int ExcludedNoticeCount,
    int AllCompleteRowCount,
    int ValidRankRowCount,
    int NegativeRankExcludedRowCount);

public sealed record CollisionFrameRow(
    string NoticeNumber,
    string CompanyName,
    int Rank,
    bool IsWinner,
    int ParticipantCount,
    double BidAdjustRate,
    double NoticeAdjustRate,
    string? RightAnchorCompany,
    double? RightAnchorGap,
    double? LeftGapValid,
    double? LeftSpaceGapAll,
    int? LeftPrevRank,
    string? LeftPrevCompany,
    double? LeftSpaceAdvantage,
    double? LeftSpaceRatio,
    double ExpectedWinProbability);

public sealed record CollisionLiftRow(
    string Tier,
    int SampleCount,
    double ActualWinRate,
    double ExpectedWinRate,
    double WinLift,
    double AverageRank,
    double AverageLeftSpaceGap,
    double AverageRightAnchorGap,
    double AverageLeftSpaceAdvantage);

public sealed record CollisionMatrixRow(
    string RightAnchorTier,
    string LeftSpaceTier,
    int SampleCount,
    double ActualWinRate,
    double ExpectedWinRate,
    double WinLift,
    double AverageRank,
    double AverageLeftSpaceGap,
    double AverageRightAnchorGap,
    double AverageLeftSpaceAdvantage);

public sealed record AnchorCompanySummaryRow(
    string CompanyName,
    int SampleCount,
    double ActualWinRate,
    double ExpectedWinRate,
    double AverageLeftSpaceGap,
    double AverageRightAnchorGap,
    double AverageLeftSpaceAdvantage,
    double WinLift);

public sealed record CollisionKeyFindings(
    int CompleteNoticeCount,
    int ValidRankRowCount,
    double? WideLeftSpaceLift,
    double? NarrowLeftSpaceLift,
    double? HighLeftAdvantageLift,
    double? LowLeftAdvantageLift,
    CollisionBestCell? BestMatrixCell);

public sealed record CollisionBestCell(
    string RightAnchorTier,
    string LeftSpaceTier,
    double WinLift,
    int SampleCount);

public sealed record CollisionAnalysisResult(
    CollisionAnalysisMetadata Metadata,
    double ResolvedMinRate,
    double ResolvedMaxRate,
    IReadOnlyList<CollisionFrameRow> CollisionFrame,
    IReadOnlyList<CollisionLiftRow> LeftSpaceSummary,
    IReadOnlyList<CollisionLiftRow> AdvantageSummary,
    IReadOnlyList<CollisionMatrixRow> MatrixSummary,
    IReadOnlyList<AnchorCompanySummaryRow> AnchorSummary,
    CollisionKeyFindings KeyFindings);

public sealed class CollisionAnalysisService
{
    public CollisionAnalysisResult Analyze(IEnumerable<ParticipantRecord> participants, CollisionAnalysisOptions? options = null)
    {
        var config = options ?? new CollisionAnalysisOptions();
        var rows = participants
            .Where(x => !string.IsNullOrWhiteSpace(x.NoticeNumber))
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => new SourceRow(
                NoticeNumber: x.NoticeNumber.Trim(),
                CompanyName: string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName : x.NormalizedCompanyName,
                Rank: x.Rank,
                BidAdjustRate: x.AdjustRate!.Value,
                NoticeAdjustRate: x.PriceAdjustmentRate ?? 0.0))
            .ToList();

        var winnerCounts = rows
            .GroupBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(x => x.Rank == 1), StringComparer.OrdinalIgnoreCase);
        var completeNoticeIds = winnerCounts
            .Where(x => x.Value == 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allCompleteRows = rows.Where(x => completeNoticeIds.Contains(x.NoticeNumber)).ToList();
        var validRows = allCompleteRows.Where(x => x.Rank >= 1).ToList();
        var metadata = new CollisionAnalysisMetadata(
            AllNoticeCount: rows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            CompleteNoticeCount: completeNoticeIds.Count,
            ExcludedNoticeCount: rows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() - completeNoticeIds.Count,
            AllCompleteRowCount: allCompleteRows.Count,
            ValidRankRowCount: validRows.Count,
            NegativeRankExcludedRowCount: allCompleteRows.Count - validRows.Count);

        var collisionRows = BuildCollisionFrame(allCompleteRows);
        if (collisionRows.Count == 0)
        {
            return new CollisionAnalysisResult(metadata, config.MinRate ?? 99.5, config.MaxRate ?? 100.0, [], [], [], [], [], new CollisionKeyFindings(metadata.CompleteNoticeCount, metadata.ValidRankRowCount, null, null, null, null, null));
        }

        var resolvedMinRate = config.MinRate ?? collisionRows.Min(x => x.BidAdjustRate);
        var resolvedMaxRate = config.MaxRate ?? collisionRows.Max(x => x.BidAdjustRate);
        collisionRows = collisionRows
            .Where(x => x.BidAdjustRate >= resolvedMinRate && x.BidAdjustRate <= resolvedMaxRate)
            .ToList();

        var withBothGaps = collisionRows
            .Where(x => x.LeftSpaceGapAll.HasValue && x.RightAnchorGap.HasValue)
            .ToList();
        var leftGapValues = withBothGaps.Select(x => x.LeftSpaceGapAll!.Value).ToList();
        var advantageValues = collisionRows.Where(x => x.LeftSpaceAdvantage.HasValue).Select(x => x.LeftSpaceAdvantage!.Value).ToList();
        var rightGapValues = withBothGaps.Select(x => x.RightAnchorGap!.Value).ToList();

        var leftSpaceSummary = SummarizeLiftByTier(
            withBothGaps,
            x => AnalysisMath.ClassifyTertile(x.LeftSpaceGapAll!.Value, leftGapValues, "좁음", "중간", "넓음"));
        var advantageSummary = SummarizeLiftByTier(
            collisionRows.Where(x => x.LeftSpaceAdvantage.HasValue).ToList(),
            x => AnalysisMath.ClassifyTertile(x.LeftSpaceAdvantage!.Value, advantageValues, "낮음", "중간", "높음"));

        var matrixRows = withBothGaps
            .Select(x => new
            {
                Row = x,
                LeftTier = AnalysisMath.ClassifyTertile(x.LeftSpaceGapAll!.Value, leftGapValues, "좁음", "중간", "넓음"),
                RightTier = AnalysisMath.ClassifyTertile(x.RightAnchorGap!.Value, rightGapValues, "가까움", "중간", "멀음"),
            })
            .GroupBy(x => new { x.RightTier, x.LeftTier })
            .Select(g =>
            {
                var aggregate = Aggregate(g.Select(x => x.Row).ToList());
                return new CollisionMatrixRow(
                    g.Key.RightTier,
                    g.Key.LeftTier,
                    aggregate.SampleCount,
                    aggregate.ActualWinRate,
                    aggregate.ExpectedWinRate,
                    aggregate.WinLift,
                    aggregate.AverageRank,
                    aggregate.AverageLeftSpaceGap,
                    aggregate.AverageRightAnchorGap,
                    aggregate.AverageLeftSpaceAdvantage);
            })
            .OrderBy(x => TierOrder(x.RightAnchorTier, "가까움", "중간", "멀음"))
            .ThenBy(x => TierOrder(x.LeftSpaceTier, "좁음", "중간", "넓음"))
            .ToList();

        var anchorSummary = collisionRows
            .Where(x => !string.IsNullOrWhiteSpace(x.RightAnchorCompany) && x.RightAnchorGap.HasValue)
            .GroupBy(x => x.RightAnchorCompany!, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var rowsForAnchor = g.ToList();
                var aggregate = Aggregate(rowsForAnchor);
                return new AnchorCompanySummaryRow(
                    CompanyName: g.Key,
                    SampleCount: rowsForAnchor.Count,
                    ActualWinRate: aggregate.ActualWinRate,
                    ExpectedWinRate: aggregate.ExpectedWinRate,
                    AverageLeftSpaceGap: aggregate.AverageLeftSpaceGap,
                    AverageRightAnchorGap: aggregate.AverageRightAnchorGap,
                    AverageLeftSpaceAdvantage: aggregate.AverageLeftSpaceAdvantage,
                    WinLift: aggregate.WinLift);
            })
            .Where(x => x.SampleCount >= config.MinAnchorSamples)
            .OrderByDescending(x => x.SampleCount)
            .ThenByDescending(x => x.WinLift)
            .ToList();

        var wide = leftSpaceSummary.FirstOrDefault(x => x.Tier == "넓음");
        var narrow = leftSpaceSummary.FirstOrDefault(x => x.Tier == "좁음");
        var highAdv = advantageSummary.FirstOrDefault(x => x.Tier == "높음");
        var lowAdv = advantageSummary.FirstOrDefault(x => x.Tier == "낮음");
        var bestCell = matrixRows
            .OrderByDescending(x => x.WinLift)
            .ThenByDescending(x => x.SampleCount)
            .FirstOrDefault();

        var findings = new CollisionKeyFindings(
            metadata.CompleteNoticeCount,
            metadata.ValidRankRowCount,
            wide?.WinLift,
            narrow?.WinLift,
            highAdv?.WinLift,
            lowAdv?.WinLift,
            bestCell is null ? null : new CollisionBestCell(bestCell.RightAnchorTier, bestCell.LeftSpaceTier, bestCell.WinLift, bestCell.SampleCount));

        return new CollisionAnalysisResult(
            metadata,
            resolvedMinRate,
            resolvedMaxRate,
            collisionRows,
            leftSpaceSummary,
            advantageSummary,
            matrixRows,
            anchorSummary,
            findings);
    }

    public void SaveSummary(CollisionAnalysisResult result, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var payload = new
        {
            metadata = result.Metadata,
            resolved_range = new { min = result.ResolvedMinRate, max = result.ResolvedMaxRate },
            key_findings = new
            {
                complete_notice_count = result.KeyFindings.CompleteNoticeCount,
                valid_rank_row_count = result.KeyFindings.ValidRankRowCount,
                wide_left_space_lift = result.KeyFindings.WideLeftSpaceLift,
                narrow_left_space_lift = result.KeyFindings.NarrowLeftSpaceLift,
                high_left_advantage_lift = result.KeyFindings.HighLeftAdvantageLift,
                low_left_advantage_lift = result.KeyFindings.LowLeftAdvantageLift,
                best_matrix_cell = result.KeyFindings.BestMatrixCell is null ? null : new
                {
                    우측앵커거리구간 = result.KeyFindings.BestMatrixCell.RightAnchorTier,
                    좌측여유구간 = result.KeyFindings.BestMatrixCell.LeftSpaceTier,
                    낙찰리프트 = result.KeyFindings.BestMatrixCell.WinLift,
                    표본수 = result.KeyFindings.BestMatrixCell.SampleCount,
                },
            },
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputDirectory, "summary.json"), json);
    }

    private static List<CollisionFrameRow> BuildCollisionFrame(IReadOnlyList<SourceRow> allCompleteRows)
    {
        var rows = new List<CollisionFrameRow>();
        foreach (var noticeGroup in allCompleteRows.GroupBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase))
        {
            var sortedAll = noticeGroup
                .OrderBy(x => x.BidAdjustRate)
                .ToList();
            var valid = sortedAll.Where(x => x.Rank >= 1).ToList();
            if (valid.Count == 0)
            {
                continue;
            }

            for (var validIndex = 0; validIndex < valid.Count; validIndex++)
            {
                var row = valid[validIndex];
                var allIndex = sortedAll.FindIndex(x => ReferenceEquals(x, row) || (x.CompanyName == row.CompanyName && x.Rank == row.Rank && x.BidAdjustRate == row.BidAdjustRate));
                if (allIndex < 0)
                {
                    continue;
                }

                var previousAll = allIndex > 0 ? sortedAll[allIndex - 1] : null;
                var previousValid = validIndex > 0 ? valid[validIndex - 1] : null;
                var nextValid = validIndex + 1 < valid.Count ? valid[validIndex + 1] : null;
                var leftSpaceGapAll = previousAll is null ? (double?)null : row.BidAdjustRate - previousAll.BidAdjustRate;
                var leftGapValid = previousValid is null ? (double?)null : row.BidAdjustRate - previousValid.BidAdjustRate;
                var rightAnchorGap = nextValid is null ? (double?)null : nextValid.BidAdjustRate - row.BidAdjustRate;
                double? advantage = leftSpaceGapAll.HasValue && rightAnchorGap.HasValue ? leftSpaceGapAll.Value - rightAnchorGap.Value : null;
                double? ratio = leftSpaceGapAll.HasValue && rightAnchorGap.HasValue ? leftSpaceGapAll.Value / (rightAnchorGap.Value + 1e-9) : null;

                rows.Add(new CollisionFrameRow(
                    NoticeNumber: row.NoticeNumber,
                    CompanyName: row.CompanyName,
                    Rank: row.Rank,
                    IsWinner: row.Rank == 1,
                    ParticipantCount: valid.Count,
                    BidAdjustRate: row.BidAdjustRate,
                    NoticeAdjustRate: row.NoticeAdjustRate,
                    RightAnchorCompany: nextValid?.CompanyName,
                    RightAnchorGap: rightAnchorGap,
                    LeftGapValid: leftGapValid,
                    LeftSpaceGapAll: leftSpaceGapAll,
                    LeftPrevRank: previousAll?.Rank,
                    LeftPrevCompany: previousAll?.CompanyName,
                    LeftSpaceAdvantage: advantage,
                    LeftSpaceRatio: ratio,
                    ExpectedWinProbability: 1.0 / valid.Count));
            }
        }

        return rows;
    }

    private static List<CollisionLiftRow> SummarizeLiftByTier(IReadOnlyList<CollisionFrameRow> rows, Func<CollisionFrameRow, string> tierSelector)
    {
        return rows
            .Select(x => new { Row = x, Tier = tierSelector(x) })
            .GroupBy(x => x.Tier, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var aggregate = Aggregate(g.Select(x => x.Row).ToList());
                return new CollisionLiftRow(
                    Tier: g.Key,
                    SampleCount: aggregate.SampleCount,
                    ActualWinRate: aggregate.ActualWinRate,
                    ExpectedWinRate: aggregate.ExpectedWinRate,
                    WinLift: aggregate.WinLift,
                    AverageRank: aggregate.AverageRank,
                    AverageLeftSpaceGap: aggregate.AverageLeftSpaceGap,
                    AverageRightAnchorGap: aggregate.AverageRightAnchorGap,
                    AverageLeftSpaceAdvantage: aggregate.AverageLeftSpaceAdvantage);
            })
            .OrderBy(x => TierOrder(x.Tier, "좁음", "낮음", "중간", "넓음", "높음"))
            .ToList();
    }

    private static AggregateResult Aggregate(IReadOnlyList<CollisionFrameRow> rows)
    {
        var actual = rows.Count == 0 ? 0.0 : rows.Count(x => x.IsWinner) / (double)rows.Count;
        var expected = rows.Count == 0 ? 0.0 : rows.Average(x => x.ExpectedWinProbability);
        return new AggregateResult(
            SampleCount: rows.Count,
            ActualWinRate: actual,
            ExpectedWinRate: expected,
            WinLift: expected <= 0 ? 0.0 : actual / expected,
            AverageRank: rows.Count == 0 ? 0.0 : rows.Average(x => x.Rank),
            AverageLeftSpaceGap: AnalysisMath.SafeMean(rows.Where(x => x.LeftSpaceGapAll.HasValue).Select(x => x.LeftSpaceGapAll!.Value)),
            AverageRightAnchorGap: AnalysisMath.SafeMean(rows.Where(x => x.RightAnchorGap.HasValue).Select(x => x.RightAnchorGap!.Value)),
            AverageLeftSpaceAdvantage: AnalysisMath.SafeMean(rows.Where(x => x.LeftSpaceAdvantage.HasValue).Select(x => x.LeftSpaceAdvantage!.Value)));
    }

    private static int TierOrder(string tier, params string[] orderedTiers)
    {
        for (var index = 0; index < orderedTiers.Length; index++)
        {
            if (string.Equals(tier, orderedTiers[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 99;
    }

    private sealed record SourceRow(string NoticeNumber, string CompanyName, int Rank, double BidAdjustRate, double NoticeAdjustRate);

    private sealed record AggregateResult(
        int SampleCount,
        double ActualWinRate,
        double ExpectedWinRate,
        double WinLift,
        double AverageRank,
        double AverageLeftSpaceGap,
        double AverageRightAnchorGap,
        double AverageLeftSpaceAdvantage);
}
