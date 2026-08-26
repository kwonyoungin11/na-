using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed record LeftOccupyZoneItem(
    string Category,
    string CategoryLabel,
    double ZoneStart,
    double ZoneEnd,
    string AnchorCompany,
    double? LeftSpaceWidth,
    string UncertaintyLabel,
    string Summary);

public static class LeftOccupyBoardBuilder
{
    public static IReadOnlyList<LeftOccupyZoneItem> Build(
        IEnumerable<ParticipantRecord> participants,
        DateTime startDate,
        DateTime endDate,
        double minRate,
        double maxRate,
        CompetitionAnalysisProfile? profile = null)
    {
        var inPeriod = ScatterChartRules.InPeriod(
            (participants ?? Array.Empty<ParticipantRecord>()).ToList(),
            startDate,
            endDate);
        var engine = new CompetitionRecommendationEngine(profile);
        var detail = engine.AnalyzeDetailed(inPeriod, Math.Min(minRate, maxRate), Math.Max(minRate, maxRate));
        return detail.ZoneDecisions
            .Where(KeepZone)
            .Select(zone => new LeftOccupyZoneItem(
                Category: zone.Category,
                CategoryLabel: FormatCategory(zone.Category),
                ZoneStart: zone.ZoneStart,
                ZoneEnd: zone.ZoneEnd,
                AnchorCompany: zone.AnchorCompany ?? string.Empty,
                LeftSpaceWidth: zone.LeftSpaceWidth,
                UncertaintyLabel: FormatUncertainty(zone.UncertaintyLevel),
                Summary: BuildSummary(zone)))
            .OrderBy(x => CategoryOrder(x.Category))
            .ThenBy(x => x.ZoneStart)
            .ToList();
    }

    private static bool KeepZone(CompetitionZoneDecision zone)
    {
        if (string.Equals(zone.Category, "candidate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(zone.Category, "forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(zone.AnchorCompany);
    }

    private static string BuildSummary(CompetitionZoneDecision zone)
    {
        var band = $"{zone.ZoneStart:0.000}%~{zone.ZoneEnd:0.000}%";
        var anchor = string.IsNullOrWhiteSpace(zone.AnchorCompany)
            ? "앵커 없음"
            : $"앵커 {zone.AnchorCompany}";
        var space = zone.LeftSpaceWidth.HasValue
            ? $"좌측여유 {zone.LeftSpaceWidth.Value:0.###}"
            : "좌측여유 없음";
        return $"{band} · {anchor} · {space}";
    }

    private static string FormatCategory(string category)
        => category switch
        {
            "candidate" => "좌측선점 후보",
            "forbidden" => "금지구간",
            "boundary" => "경계구간",
            _ => "기타",
        };

    private static string FormatUncertainty(string? level)
        => level switch
        {
            "low" => "낮음",
            "medium" => "중간",
            "high" => "높음",
            _ => "중간",
        };

    private static int CategoryOrder(string category)
        => category switch
        {
            "candidate" => 0,
            "forbidden" => 1,
            "boundary" => 2,
            _ => 9,
        };
}
