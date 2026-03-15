using System.IO;
using System.Text.Json;

namespace NaraCollector.Desktop.Services;

public sealed record DesktopFixedWindowRule(
    int MinBids,
    double ConcentrationThreshold);

public sealed record DesktopCompetitionStrategyProfile(
    string MyCompanyKeyword,
    IReadOnlyList<string> ExcludedCompanyKeywords,
    int Period1YDays,
    int Period6MDays,
    int Period3MDays,
    DesktopFixedWindowRule FixedAll,
    DesktopFixedWindowRule Fixed1Y,
    DesktopFixedWindowRule Fixed6M,
    DesktopFixedWindowRule Fixed3M,
    int MinimumSupportNotices,
    int MinimumAwardNotices,
    double NearbyRadiusRate,
    double BandThresholdMin,
    double BandThresholdRatio,
    int MaxBandExpandSteps,
    double EvidenceShadowWeight,
    double EvidenceGapWeight,
    double EvidenceCompetitionWeight,
    double FallbackGapWeight,
    double FallbackCompetitionWeight,
    double FallbackSupportWeight)
{
    public static DesktopCompetitionStrategyProfile Default { get; } = new(
        MyCompanyKeyword: "동국문화재연구원",
        ExcludedCompanyKeywords: ["효림"],
        Period1YDays: 365,
        Period6MDays: 180,
        Period3MDays: 90,
        FixedAll: new DesktopFixedWindowRule(6, 0.52),
        Fixed1Y: new DesktopFixedWindowRule(5, 0.53),
        Fixed6M: new DesktopFixedWindowRule(4, 0.54),
        Fixed3M: new DesktopFixedWindowRule(3, 0.55),
        MinimumSupportNotices: 3,
        MinimumAwardNotices: 1,
        NearbyRadiusRate: 0.01,
        BandThresholdMin: 18.0,
        BandThresholdRatio: 0.82,
        MaxBandExpandSteps: 4,
        EvidenceShadowWeight: 0.45,
        EvidenceGapWeight: 0.35,
        EvidenceCompetitionWeight: 0.20,
        FallbackGapWeight: 0.55,
        FallbackCompetitionWeight: 0.30,
        FallbackSupportWeight: 0.15);
}

public static class CompetitionStrategyProfileProvider
{
    private static readonly Lazy<DesktopCompetitionStrategyProfile> CurrentProfile = new(() => Load());

    public static DesktopCompetitionStrategyProfile Current => CurrentProfile.Value;

    public static DesktopCompetitionStrategyProfile Load(IAppPathResolver? pathResolver = null)
    {
        pathResolver ??= new DefaultAppPathResolver();
        return LoadFromPaths(pathResolver.ResolveAnalysisProfilePath(), pathResolver.ResolveApiRuleProfilePath());
    }

    public static DesktopCompetitionStrategyProfile LoadFromPaths(string analysisProfilePath, string apiRuleProfilePath)
    {
        var defaults = DesktopCompetitionStrategyProfile.Default;
        using var analysis = LoadDocument(analysisProfilePath);
        using var apiRule = LoadDocument(apiRuleProfilePath);

        var myCompany = ReadString(analysis, "cli_defaults", "my_company") ?? defaults.MyCompanyKeyword;
        var excluded = ReadStringArray(apiRule, "retired_company_tokens");
        if (excluded.Count == 0)
        {
            excluded = defaults.ExcludedCompanyKeywords.ToList();
        }

        var fixedWindows = GetObject(analysis, "desktop", "fixed_windows");
        var evidenceWeights = GetObject(analysis, "desktop", "evidence_weights");
        var fallbackWeights = GetObject(analysis, "desktop", "fallback_weights");

        return new DesktopCompetitionStrategyProfile(
            MyCompanyKeyword: myCompany,
            ExcludedCompanyKeywords: excluded,
            Period1YDays: ReadInt(analysis, defaults.Period1YDays, "period_days", "1y"),
            Period6MDays: ReadInt(analysis, defaults.Period6MDays, "period_days", "6m"),
            Period3MDays: ReadInt(analysis, defaults.Period3MDays, "period_days", "3m"),
            FixedAll: ReadFixedWindowRule(fixedWindows, "all", defaults.FixedAll),
            Fixed1Y: ReadFixedWindowRule(fixedWindows, "1y", defaults.Fixed1Y),
            Fixed6M: ReadFixedWindowRule(fixedWindows, "6m", defaults.Fixed6M),
            Fixed3M: ReadFixedWindowRule(fixedWindows, "3m", defaults.Fixed3M),
            MinimumSupportNotices: ReadInt(analysis, defaults.MinimumSupportNotices, "desktop", "minimum_support_notices"),
            MinimumAwardNotices: ReadInt(analysis, defaults.MinimumAwardNotices, "desktop", "minimum_award_notices"),
            NearbyRadiusRate: ReadDouble(analysis, defaults.NearbyRadiusRate, "desktop", "nearby_radius_rate"),
            BandThresholdMin: ReadDouble(analysis, defaults.BandThresholdMin, "desktop", "band_threshold_min"),
            BandThresholdRatio: ReadDouble(analysis, defaults.BandThresholdRatio, "desktop", "band_threshold_ratio"),
            MaxBandExpandSteps: ReadInt(analysis, defaults.MaxBandExpandSteps, "desktop", "max_band_expand_steps"),
            EvidenceShadowWeight: ReadDouble(evidenceWeights, defaults.EvidenceShadowWeight, "shadow"),
            EvidenceGapWeight: ReadDouble(evidenceWeights, defaults.EvidenceGapWeight, "gap"),
            EvidenceCompetitionWeight: ReadDouble(evidenceWeights, defaults.EvidenceCompetitionWeight, "competition"),
            FallbackGapWeight: ReadDouble(fallbackWeights, defaults.FallbackGapWeight, "gap"),
            FallbackCompetitionWeight: ReadDouble(fallbackWeights, defaults.FallbackCompetitionWeight, "competition"),
            FallbackSupportWeight: ReadDouble(fallbackWeights, defaults.FallbackSupportWeight, "support"));
    }

    private static JsonDocument? LoadDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static DesktopFixedWindowRule ReadFixedWindowRule(JsonElement? parent, string key, DesktopFixedWindowRule fallback)
    {
        if (parent is null || !parent.Value.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        var minBids = TryGetInt(element, "min_bids") ?? fallback.MinBids;
        var concentration = TryGetDouble(element, "concentration_threshold") ?? fallback.ConcentrationThreshold;
        return new DesktopFixedWindowRule(minBids, concentration);
    }

    private static JsonElement? GetObject(JsonDocument? document, params string[] path)
    {
        if (document is null)
        {
            return null;
        }

        return GetObject(document.RootElement, path);
    }

    private static JsonElement? GetObject(JsonElement? element, params string[] path)
    {
        if (element is null)
        {
            return null;
        }

        var current = element.Value;
        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static string? ReadString(JsonDocument? document, params string[] path)
    {
        var element = GetObject(document, path);
        return element is not null && element.Value.ValueKind == JsonValueKind.String
            ? element.Value.GetString()
            : null;
    }

    private static List<string> ReadStringArray(JsonDocument? document, params string[] path)
    {
        var element = GetObject(document, path);
        if (element is null || element.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.Value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ReadInt(JsonDocument? document, int fallback, params string[] path)
    {
        var element = GetObject(document, path);
        return element is null ? fallback : ReadInt(element, fallback);
    }

    private static int ReadInt(JsonElement? element, int fallback, params string[] path)
    {
        var target = GetObject(element, path);
        return target is null ? fallback : (TryGetInt(target.Value) ?? fallback);
    }

    private static double ReadDouble(JsonDocument? document, double fallback, params string[] path)
    {
        var element = GetObject(document, path);
        return element is null ? fallback : ReadDouble(element, fallback);
    }

    private static double ReadDouble(JsonElement? element, double fallback, params string[] path)
    {
        var target = GetObject(element, path);
        return target is null ? fallback : (TryGetDouble(target.Value) ?? fallback);
    }

    private static int ReadInt(JsonElement? element, int fallback)
        => element is null ? fallback : (TryGetInt(element.Value) ?? fallback);

    private static double ReadDouble(JsonElement? element, double fallback)
        => element is null ? fallback : (TryGetDouble(element.Value) ?? fallback);

    private static int? TryGetInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? TryGetInt(value) : null;

    private static int? TryGetInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? TryGetDouble(value) : null;

    private static double? TryGetDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
