using System.Globalization;
using System.IO;
using System.Text.Json;

namespace NaraCollector.Desktop.Services;

public sealed record CompetitionAnalysisProfile(
    string MyCompanyKeyword,
    IReadOnlyList<string> ExcludedCompanyKeywords,
    double RateMin,
    double RateMax,
    double ZoneSize,
    IReadOnlyDictionary<string, int> PeriodDays,
    int FixedMinBids,
    double FixedConcentrationThreshold,
    IReadOnlyDictionary<string, int> FixedWindowDays,
    IReadOnlyDictionary<string, double> FixedConsistencyWeights,
    IReadOnlyDictionary<string, double> FixedScoreWeights,
    double FixedRecencyMultiplier,
    double FixedVolatilityReference,
    double TrendDirectionThreshold,
    int TrendRecentSampleSize,
    IReadOnlyDictionary<string, int> CompetitionWindowDays,
    IReadOnlyDictionary<string, double> CompetitionWeightedCountWeights,
    IReadOnlyDictionary<string, double> CompetitionDensityWeights,
    IReadOnlyDictionary<string, double> CompetitionOverheatWeights,
    int CompetitionFixedAnchorSearchZoneCount,
    double CompetitionFixedAnchorMaxGap,
    double GapMaxAnchorGap,
    double GapLeftAnchorDiscount,
    IReadOnlyDictionary<string, double> GapPersistenceWeights,
    IReadOnlyDictionary<string, double> GapOpportunityWeights,
    double GapNoAnchorMultiplier,
    double ShadowBaseOffset,
    double ShadowOffsetStdMultiplier,
    double ShadowOffsetStdCap,
    double ShadowDownwardVelocityMultiplier,
    double ShadowDownwardVelocityCap,
    double ShadowUpwardOffsetMultiplier,
    double ShadowMinOffset,
    double ShadowBandStdMultiplier,
    double ShadowBandBaseHalf,
    double ShadowBandMinHalf,
    double ShadowBandMaxHalf,
    double ShadowVolatilityReference,
    IReadOnlyDictionary<string, double> ShadowMovementScores,
    IReadOnlyDictionary<string, double> ShadowPositiveWeights,
    IReadOnlyDictionary<string, double> ShadowNegativeWeights,
    double ShadowEvidenceBonus,
    double ShadowMinimumPriorityScore,
    int ShadowDedupePrecision,
    double SameDayClusterRateTolerance,
    int SameDayClusterMinClusterSize,
    double DecisionForbiddenDensityScore,
    double DecisionForbiddenOverheatScore,
    double DecisionCandidateMaxDensityScore,
    double DecisionCandidateMaxOverheatScore,
    double DecisionCandidateMinOpportunityScore,
    double DecisionMinLeftSpaceWidth,
    double DecisionBoundaryClearMinDistance,
    double DecisionWideLeftSpaceAnchorMultiplier,
    double DecisionNarrowLeftSpaceAnchorMultiplier,
    double DecisionNarrowLeftSpaceMinWidth,
    double DecisionCalmBandMaxDensityScore,
    double DecisionCalmBandMaxOverheatScore,
    double DecisionLowNewcomerOverheatScore,
    double DecisionLessObviousDensityScore,
    double DecisionUncertaintyHighDensityScore,
    double DecisionUncertaintyHighOverheatScore,
    double DecisionUncertaintyMediumDensityScore,
    double DecisionUncertaintyMediumOverheatScore,
    double DecisionUncertaintyMinOpportunityScore,
    double DecisionUncertaintyHighVolatilityPenalty,
    double DecisionUncertaintyMediumVolatilityPenalty)
{
    public static CompetitionAnalysisProfile Default { get; } = new(
        MyCompanyKeyword: "동국문화재연구원",
        ExcludedCompanyKeywords: ["한국문화재연구원", "효림"],
        RateMin: 99.5,
        RateMax: 100.0,
        ZoneSize: 0.001,
        PeriodDays: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["all"] = 0,
            ["1m"] = 30,
            ["3m"] = 90,
            ["6m"] = 180,
            ["1y"] = 365,
            ["2y"] = 730,
        },
        FixedMinBids: 5,
        FixedConcentrationThreshold: 40.0,
        FixedWindowDays: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["recent"] = 30,
            ["short"] = 90,
            ["medium"] = 180,
        },
        FixedConsistencyWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["concentration"] = 0.25,
            ["medium"] = 0.25,
            ["short"] = 0.25,
            ["recent"] = 0.25,
        },
        FixedScoreWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["concentration"] = 0.45,
            ["consistency"] = 0.35,
            ["recency_presence"] = 0.20,
            ["volatility_penalty"] = 0.15,
        },
        FixedRecencyMultiplier: 2.0,
        FixedVolatilityReference: 0.02,
        TrendDirectionThreshold: 0.006,
        TrendRecentSampleSize: 8,
        CompetitionWindowDays: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["recent"] = 30,
            ["short"] = 90,
            ["medium"] = 180,
            ["historical_recent"] = 30,
            ["historical_lookback"] = 180,
        },
        CompetitionWeightedCountWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["recent"] = 0.50,
            ["short"] = 0.30,
            ["medium"] = 0.20,
        },
        CompetitionDensityWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["bid_count"] = 0.60,
            ["company_count"] = 0.40,
        },
        CompetitionOverheatWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["growth"] = 0.55,
            ["newcomer_ratio"] = 0.45,
        },
        CompetitionFixedAnchorSearchZoneCount: 20,
        CompetitionFixedAnchorMaxGap: 0.02,
        GapMaxAnchorGap: 0.02,
        GapLeftAnchorDiscount: 0.70,
        GapPersistenceWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["density"] = 0.65,
            ["overheat"] = 0.35,
        },
        GapOpportunityWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["persistence"] = 0.55,
            ["anchor"] = 0.35,
            ["calm"] = 0.10,
        },
        GapNoAnchorMultiplier: 0.40,
        ShadowBaseOffset: 0.001,
        ShadowOffsetStdMultiplier: 0.60,
        ShadowOffsetStdCap: 0.002,
        ShadowDownwardVelocityMultiplier: 12.0,
        ShadowDownwardVelocityCap: 0.002,
        ShadowUpwardOffsetMultiplier: 0.80,
        ShadowMinOffset: 0.001,
        ShadowBandStdMultiplier: 1.50,
        ShadowBandBaseHalf: 0.001,
        ShadowBandMinHalf: 0.001,
        ShadowBandMaxHalf: 0.004,
        ShadowVolatilityReference: 0.015,
        ShadowMovementScores: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["하향조정"] = 85.0,
            ["유지"] = 70.0,
            ["상향조정"] = 55.0,
            ["알수없음"] = 50.0,
        },
        ShadowPositiveWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["fixed"] = 0.35,
            ["gap"] = 0.35,
            ["anchor"] = 0.15,
            ["movement"] = 0.15,
        },
        ShadowNegativeWeights: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["density"] = 0.45,
            ["overheat"] = 0.35,
            ["volatility"] = 0.20,
        },
        ShadowEvidenceBonus: 30.0,
        ShadowMinimumPriorityScore: 35.0,
        ShadowDedupePrecision: 3,
        SameDayClusterRateTolerance: 0.003,
        SameDayClusterMinClusterSize: 2,
        DecisionForbiddenDensityScore: 65.0,
        DecisionForbiddenOverheatScore: 60.0,
        DecisionCandidateMaxDensityScore: 45.0,
        DecisionCandidateMaxOverheatScore: 45.0,
        DecisionCandidateMinOpportunityScore: 55.0,
        DecisionMinLeftSpaceWidth: 0.002,
        DecisionBoundaryClearMinDistance: 0.002,
        DecisionWideLeftSpaceAnchorMultiplier: 1.05,
        DecisionNarrowLeftSpaceAnchorMultiplier: 0.75,
        DecisionNarrowLeftSpaceMinWidth: 0.001,
        DecisionCalmBandMaxDensityScore: 45.0,
        DecisionCalmBandMaxOverheatScore: 45.0,
        DecisionLowNewcomerOverheatScore: 35.0,
        DecisionLessObviousDensityScore: 35.0,
        DecisionUncertaintyHighDensityScore: 60.0,
        DecisionUncertaintyHighOverheatScore: 60.0,
        DecisionUncertaintyMediumDensityScore: 45.0,
        DecisionUncertaintyMediumOverheatScore: 45.0,
        DecisionUncertaintyMinOpportunityScore: 45.0,
        DecisionUncertaintyHighVolatilityPenalty: 45.0,
        DecisionUncertaintyMediumVolatilityPenalty: 25.0);
}

public static class CompetitionAnalysisProfileProvider
{
    private static readonly Lazy<CompetitionAnalysisProfile> CurrentValue = new(() => Load());

    public static CompetitionAnalysisProfile Current => CurrentValue.Value;

    public static CompetitionAnalysisProfile Load(IAppPathResolver? pathResolver = null)
    {
        pathResolver ??= new DefaultAppPathResolver();
        return LoadFromPaths(pathResolver.ResolveAnalysisProfilePath(), pathResolver.ResolveApiRuleProfilePath());
    }

    public static CompetitionAnalysisProfile LoadFromPaths(string analysisProfilePath, string apiRuleProfilePath)
    {
        var defaults = CompetitionAnalysisProfile.Default;
        using var analysis = LoadDocument(analysisProfilePath);
        using var apiRule = LoadDocument(apiRuleProfilePath);

        return new CompetitionAnalysisProfile(
            MyCompanyKeyword: ReadString(analysis, defaults.MyCompanyKeyword, "cli_defaults", "my_company"),
            ExcludedCompanyKeywords: ReadStringArray(apiRule, defaults.ExcludedCompanyKeywords, "retired_company_tokens"),
            RateMin: ReadDouble(analysis, defaults.RateMin, "rate_range", "min"),
            RateMax: ReadDouble(analysis, defaults.RateMax, "rate_range", "max"),
            ZoneSize: ReadDouble(analysis, defaults.ZoneSize, "rate_range", "zone_size"),
            PeriodDays: ReadIntDictionary(analysis, defaults.PeriodDays, "period_days"),
            FixedMinBids: ReadInt(analysis, defaults.FixedMinBids, "fixed_bidder", "min_bids"),
            FixedConcentrationThreshold: ReadDouble(analysis, defaults.FixedConcentrationThreshold, "fixed_bidder", "concentration_threshold"),
            FixedWindowDays: ReadIntDictionary(analysis, defaults.FixedWindowDays, "fixed_bidder", "window_days"),
            FixedConsistencyWeights: ReadDoubleDictionary(analysis, defaults.FixedConsistencyWeights, "fixed_bidder", "consistency_weights"),
            FixedScoreWeights: ReadDoubleDictionary(analysis, defaults.FixedScoreWeights, "fixed_bidder", "score_weights"),
            FixedRecencyMultiplier: ReadDouble(analysis, defaults.FixedRecencyMultiplier, "fixed_bidder", "recency_multiplier"),
            FixedVolatilityReference: ReadDouble(analysis, defaults.FixedVolatilityReference, "fixed_bidder", "volatility_reference"),
            TrendDirectionThreshold: ReadDouble(analysis, defaults.TrendDirectionThreshold, "trend", "direction_threshold"),
            TrendRecentSampleSize: ReadInt(analysis, defaults.TrendRecentSampleSize, "trend", "recent_sample_size"),
            CompetitionWindowDays: ReadIntDictionary(analysis, defaults.CompetitionWindowDays, "competition", "window_days"),
            CompetitionWeightedCountWeights: ReadDoubleDictionary(analysis, defaults.CompetitionWeightedCountWeights, "competition", "weighted_count_weights"),
            CompetitionDensityWeights: ReadDoubleDictionary(analysis, defaults.CompetitionDensityWeights, "competition", "density_weights"),
            CompetitionOverheatWeights: ReadDoubleDictionary(analysis, defaults.CompetitionOverheatWeights, "competition", "overheat_weights"),
            CompetitionFixedAnchorSearchZoneCount: ReadInt(analysis, defaults.CompetitionFixedAnchorSearchZoneCount, "competition", "fixed_anchor_search_zone_count"),
            CompetitionFixedAnchorMaxGap: ReadDouble(analysis, defaults.CompetitionFixedAnchorMaxGap, "competition", "fixed_anchor_max_gap"),
            GapMaxAnchorGap: ReadDouble(analysis, defaults.GapMaxAnchorGap, "gap", "max_anchor_gap"),
            GapLeftAnchorDiscount: ReadDouble(analysis, defaults.GapLeftAnchorDiscount, "gap", "left_anchor_discount"),
            GapPersistenceWeights: ReadDoubleDictionary(analysis, defaults.GapPersistenceWeights, "gap", "persistence_weights"),
            GapOpportunityWeights: ReadDoubleDictionary(analysis, defaults.GapOpportunityWeights, "gap", "opportunity_weights"),
            GapNoAnchorMultiplier: ReadDouble(analysis, defaults.GapNoAnchorMultiplier, "gap", "no_anchor_multiplier"),
            ShadowBaseOffset: ReadDouble(analysis, defaults.ShadowBaseOffset, "shadow", "base_offset"),
            ShadowOffsetStdMultiplier: ReadDouble(analysis, defaults.ShadowOffsetStdMultiplier, "shadow", "offset_std_multiplier"),
            ShadowOffsetStdCap: ReadDouble(analysis, defaults.ShadowOffsetStdCap, "shadow", "offset_std_cap"),
            ShadowDownwardVelocityMultiplier: ReadDouble(analysis, defaults.ShadowDownwardVelocityMultiplier, "shadow", "downward_velocity_multiplier"),
            ShadowDownwardVelocityCap: ReadDouble(analysis, defaults.ShadowDownwardVelocityCap, "shadow", "downward_velocity_cap"),
            ShadowUpwardOffsetMultiplier: ReadDouble(analysis, defaults.ShadowUpwardOffsetMultiplier, "shadow", "upward_offset_multiplier"),
            ShadowMinOffset: ReadDouble(analysis, defaults.ShadowMinOffset, "shadow", "min_offset"),
            ShadowBandStdMultiplier: ReadDouble(analysis, defaults.ShadowBandStdMultiplier, "shadow", "band_std_multiplier"),
            ShadowBandBaseHalf: ReadDouble(analysis, defaults.ShadowBandBaseHalf, "shadow", "band_base_half"),
            ShadowBandMinHalf: ReadDouble(analysis, defaults.ShadowBandMinHalf, "shadow", "band_min_half"),
            ShadowBandMaxHalf: ReadDouble(analysis, defaults.ShadowBandMaxHalf, "shadow", "band_max_half"),
            ShadowVolatilityReference: ReadDouble(analysis, defaults.ShadowVolatilityReference, "shadow", "volatility_reference"),
            ShadowMovementScores: ReadDoubleDictionary(analysis, defaults.ShadowMovementScores, "shadow", "movement_scores"),
            ShadowPositiveWeights: ReadDoubleDictionary(analysis, defaults.ShadowPositiveWeights, "shadow", "positive_weights"),
            ShadowNegativeWeights: ReadDoubleDictionary(analysis, defaults.ShadowNegativeWeights, "shadow", "negative_weights"),
            ShadowEvidenceBonus: ReadDouble(analysis, defaults.ShadowEvidenceBonus, "shadow", "evidence_bonus"),
            ShadowMinimumPriorityScore: ReadDouble(analysis, defaults.ShadowMinimumPriorityScore, "shadow", "minimum_priority_score"),
            ShadowDedupePrecision: ReadInt(analysis, defaults.ShadowDedupePrecision, "shadow", "dedupe_precision"),
            SameDayClusterRateTolerance: ReadDouble(analysis, defaults.SameDayClusterRateTolerance, "same_day_collapse", "rate_tolerance"),
            SameDayClusterMinClusterSize: ReadInt(analysis, defaults.SameDayClusterMinClusterSize, "same_day_collapse", "min_cluster_size"),
            DecisionForbiddenDensityScore: ReadDouble(analysis, defaults.DecisionForbiddenDensityScore, "decision_rules", "forbidden_density_score"),
            DecisionForbiddenOverheatScore: ReadDouble(analysis, defaults.DecisionForbiddenOverheatScore, "decision_rules", "forbidden_overheat_score"),
            DecisionCandidateMaxDensityScore: ReadDouble(analysis, defaults.DecisionCandidateMaxDensityScore, "decision_rules", "candidate_max_density_score"),
            DecisionCandidateMaxOverheatScore: ReadDouble(analysis, defaults.DecisionCandidateMaxOverheatScore, "decision_rules", "candidate_max_overheat_score"),
            DecisionCandidateMinOpportunityScore: ReadDouble(analysis, defaults.DecisionCandidateMinOpportunityScore, "decision_rules", "candidate_min_opportunity_score"),
            DecisionMinLeftSpaceWidth: ReadDouble(analysis, defaults.DecisionMinLeftSpaceWidth, "decision_rules", "min_left_space_width"),
            DecisionBoundaryClearMinDistance: ReadDouble(analysis, defaults.DecisionBoundaryClearMinDistance, "decision_rules", "boundary_clear_min_distance"),
            DecisionWideLeftSpaceAnchorMultiplier: ReadDouble(analysis, defaults.DecisionWideLeftSpaceAnchorMultiplier, "decision_rules", "wide_left_space_anchor_multiplier"),
            DecisionNarrowLeftSpaceAnchorMultiplier: ReadDouble(analysis, defaults.DecisionNarrowLeftSpaceAnchorMultiplier, "decision_rules", "narrow_left_space_anchor_multiplier"),
            DecisionNarrowLeftSpaceMinWidth: ReadDouble(analysis, defaults.DecisionNarrowLeftSpaceMinWidth, "decision_rules", "narrow_left_space_min_width"),
            DecisionCalmBandMaxDensityScore: ReadDouble(analysis, defaults.DecisionCalmBandMaxDensityScore, "decision_rules", "calm_band_max_density_score"),
            DecisionCalmBandMaxOverheatScore: ReadDouble(analysis, defaults.DecisionCalmBandMaxOverheatScore, "decision_rules", "calm_band_max_overheat_score"),
            DecisionLowNewcomerOverheatScore: ReadDouble(analysis, defaults.DecisionLowNewcomerOverheatScore, "decision_rules", "low_newcomer_overheat_score"),
            DecisionLessObviousDensityScore: ReadDouble(analysis, defaults.DecisionLessObviousDensityScore, "decision_rules", "less_obvious_density_score"),
            DecisionUncertaintyHighDensityScore: ReadDouble(analysis, defaults.DecisionUncertaintyHighDensityScore, "decision_rules", "uncertainty_high_density_score"),
            DecisionUncertaintyHighOverheatScore: ReadDouble(analysis, defaults.DecisionUncertaintyHighOverheatScore, "decision_rules", "uncertainty_high_overheat_score"),
            DecisionUncertaintyMediumDensityScore: ReadDouble(analysis, defaults.DecisionUncertaintyMediumDensityScore, "decision_rules", "uncertainty_medium_density_score"),
            DecisionUncertaintyMediumOverheatScore: ReadDouble(analysis, defaults.DecisionUncertaintyMediumOverheatScore, "decision_rules", "uncertainty_medium_overheat_score"),
            DecisionUncertaintyMinOpportunityScore: ReadDouble(analysis, defaults.DecisionUncertaintyMinOpportunityScore, "decision_rules", "uncertainty_min_opportunity_score"),
            DecisionUncertaintyHighVolatilityPenalty: ReadDouble(analysis, defaults.DecisionUncertaintyHighVolatilityPenalty, "decision_rules", "uncertainty_high_volatility_penalty"),
            DecisionUncertaintyMediumVolatilityPenalty: ReadDouble(analysis, defaults.DecisionUncertaintyMediumVolatilityPenalty, "decision_rules", "uncertainty_medium_volatility_penalty"));
    }

    private static JsonDocument? LoadDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonElement? GetElement(JsonDocument? document, params string[] path)
    {
        if (document is null)
        {
            return null;
        }

        JsonElement current = document.RootElement;
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

    private static string ReadString(JsonDocument? document, string fallback, params string[] path)
    {
        var element = GetElement(document, path);
        return element is not null && element.Value.ValueKind == JsonValueKind.String
            ? element.Value.GetString() ?? fallback
            : fallback;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonDocument? document, IReadOnlyList<string> fallback, params string[] path)
    {
        var element = GetElement(document, path);
        if (element is null || element.Value.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        var values = element.Value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? fallback : values;
    }

    private static int ReadInt(JsonDocument? document, int fallback, params string[] path)
    {
        var element = GetElement(document, path);
        return element is null ? fallback : TryParseInt(element.Value, fallback);
    }

    private static double ReadDouble(JsonDocument? document, double fallback, params string[] path)
    {
        var element = GetElement(document, path);
        return element is null ? fallback : TryParseDouble(element.Value, fallback);
    }

    private static IReadOnlyDictionary<string, int> ReadIntDictionary(JsonDocument? document, IReadOnlyDictionary<string, int> fallback, params string[] path)
    {
        var element = GetElement(document, path);
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.Value.EnumerateObject())
        {
            dict[prop.Name] = TryParseInt(prop.Value, 0);
        }

        return dict.Count == 0 ? fallback : dict;
    }

    private static IReadOnlyDictionary<string, double> ReadDoubleDictionary(JsonDocument? document, IReadOnlyDictionary<string, double> fallback, params string[] path)
    {
        var element = GetElement(document, path);
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.Value.EnumerateObject())
        {
            dict[prop.Name] = TryParseDouble(prop.Value, 0.0);
        }

        return dict.Count == 0 ? fallback : dict;
    }

    private static int TryParseInt(JsonElement element, int fallback)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static double TryParseDouble(JsonElement element, double fallback)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }
}
