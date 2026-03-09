using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed record CompetitionRecommendationResult(
    bool HasRecommendation,
    string SourceCompany,
    string TargetCompany,
    string TrendDirection,
    double RecommendedRate,
    double BandMin,
    double BandMax,
    int CompetitorSampleCount,
    int BandCompetitorCount,
    int SupportingNoticeCount,
    int AwardNoticeCount,
    string DecisionCategory,
    string UncertaintyLevel,
    string AnchorCompany,
    double? AnchorGap,
    double? LeftSpaceWidth,
    double? LeftBoundaryDistance,
    string LeftSpaceSource,
    string ReasonSummary,
    IReadOnlyList<string> ReasonCodes,
    double FixedScore,
    double AnchorScore,
    double GapOpportunityScore,
    double CrowdAvoidanceScore,
    double DensityPenalty,
    double OverheatPenalty,
    double VolatilityPenalty,
    double EvidenceScore)
{
    public static CompetitionRecommendationResult Empty(double minRate, double maxRate, double zoneSize, int competitorSampleCount)
    {
        var center = Math.Clamp(100.0, minRate, maxRate);
        var spread = Math.Max(zoneSize * 2.0, 0.002);
        return new CompetitionRecommendationResult(
            HasRecommendation: false,
            SourceCompany: string.Empty,
            TargetCompany: string.Empty,
            TrendDirection: "알수없음",
            RecommendedRate: center,
            BandMin: Math.Clamp(center - spread, minRate, maxRate),
            BandMax: Math.Clamp(center + spread, minRate, maxRate),
            CompetitorSampleCount: competitorSampleCount,
            BandCompetitorCount: 0,
            SupportingNoticeCount: 0,
            AwardNoticeCount: 0,
            DecisionCategory: "boundary",
            UncertaintyLevel: "high",
            AnchorCompany: string.Empty,
            AnchorGap: null,
            LeftSpaceWidth: null,
            LeftBoundaryDistance: null,
            LeftSpaceSource: string.Empty,
            ReasonSummary: "조건부우위 후보를 만들 근거가 부족합니다.",
            ReasonCodes: ["UNCERTAINTY_HIGH"],
            FixedScore: 0,
            AnchorScore: 0,
            GapOpportunityScore: 0,
            CrowdAvoidanceScore: 0,
            DensityPenalty: 0,
            OverheatPenalty: 0,
            VolatilityPenalty: 0,
            EvidenceScore: 0);
    }
}

public sealed class CompetitionRecommendationEngine
{
    private readonly CompetitionAnalysisProfile _profile;

    public CompetitionRecommendationEngine(CompetitionAnalysisProfile? profile = null)
    {
        _profile = profile ?? CompetitionAnalysisProfileProvider.Current;
    }

    public CompetitionRecommendationResult Analyze(IEnumerable<ParticipantRecord> participants, double minRate, double maxRate)
    {
        var detail = AnalyzeDetailed(participants, minRate, maxRate);
        return detail.Recommendations.Count == 0
            ? CompetitionRecommendationResult.Empty(Math.Min(minRate, maxRate), Math.Max(minRate, maxRate), _profile.ZoneSize, detail.CompetitorSampleCount)
            : detail.Recommendations[0];
    }

    public CompetitionStrategyAnalysisResult AnalyzeDetailed(IEnumerable<ParticipantRecord> participants, double minRate, double maxRate)
    {
        var safeMinRate = Math.Min(minRate, maxRate);
        var safeMaxRate = Math.Max(minRate, maxRate);
        var competitors = BuildCompetitorPoints(participants, safeMinRate, safeMaxRate);
        if (competitors.Count == 0)
        {
            return new CompetitionStrategyAnalysisResult(0, 0, [], []);
        }

        var fixedBidders = DetectFixedBidders(competitors, safeMinRate, safeMaxRate);
        if (fixedBidders.Count == 0)
        {
            return new CompetitionStrategyAnalysisResult(competitors.Count, 0, [], []);
        }

        var trends = AnalyzeTrends(competitors, fixedBidders);
        var competitionProfiles = BuildCompetitionProfiles(competitors, fixedBidders, safeMinRate, safeMaxRate);
        var gaps = BuildGapProfiles(competitionProfiles, fixedBidders, safeMinRate);
        var recommendations = BuildRecommendations(competitors, fixedBidders, trends, gaps, safeMinRate, safeMaxRate);
        var zoneDecisions = BuildZoneDecisions(competitionProfiles, gaps);

        var normalizedRecommendations = recommendations
            .Select(x => x with { CompetitorSampleCount = competitors.Count })
            .ToList();
        return new CompetitionStrategyAnalysisResult(competitors.Count, fixedBidders.Count, normalizedRecommendations, zoneDecisions);
    }

    private List<RecommendationPoint> BuildCompetitorPoints(IEnumerable<ParticipantRecord> participants, double minRate, double maxRate)
    {
        return participants
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => new
            {
                Point = x,
                Rate = x.AdjustRate!.Value,
                Company = string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName : x.NormalizedCompanyName,
                Notice = NormalizeToken(string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber),
                Timestamp = x.BidDate ?? x.OpenDate,
            })
            .Where(x => x.Rate >= minRate && x.Rate <= maxRate)
            .Where(x => !string.IsNullOrWhiteSpace(x.Company) && !string.IsNullOrWhiteSpace(x.Notice))
            .Where(x => !ContainsConfiguredToken(x.Company, _profile.MyCompanyKeyword))
            .Where(x => !_profile.ExcludedCompanyKeywords.Any(token => ContainsConfiguredToken(x.Company, token)))
            .Select(x => new RecommendationPoint(
                Rate: x.Rate,
                Timestamp: x.Timestamp,
                Company: x.Company.Trim(),
                Notice: x.Notice,
                Rank: x.Point.Rank))
            .ToList();
    }

    private List<FixedBidderStat> DetectFixedBidders(IReadOnlyList<RecommendationPoint> points, double minRate, double maxRate)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var anchorTime = points.Max(x => x.Timestamp);
        var results = new List<FixedBidderStat>();

        foreach (var companyGroup in points.GroupBy(x => x.Company, StringComparer.OrdinalIgnoreCase))
        {
            var rows = companyGroup.ToList();
            if (rows.Count < _profile.FixedMinBids)
            {
                continue;
            }

            var zones = rows
                .Select(x => TryGetZoneIndex(x.Rate, minRate, maxRate))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();
            if (zones.Count == 0)
            {
                continue;
            }

            var zoneCounts = zones
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => x.Count());
            var dominant = zoneCounts
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .First();

            var concentration = dominant.Value / (double)rows.Count * 100.0;
            if (concentration < _profile.FixedConcentrationThreshold)
            {
                continue;
            }

            var alignedRates = rows
                .Where(x =>
                {
                    var idx = TryGetZoneIndex(x.Rate, minRate, maxRate);
                    return idx.HasValue && Math.Abs(idx.Value - dominant.Key) <= 1;
                })
                .Select(x => x.Rate)
                .ToList();
            var allRates = rows.Select(x => x.Rate).ToList();
            var mainZoneAvg = alignedRates.Count > 0 ? Median(alignedRates) : Median(allRates);
            var stdRate = PopulationStandardDeviation(allRates);

            var share30d = CollectWindowShare(rows, anchorTime, GetIntValue(_profile.FixedWindowDays, "recent", 30), dominant.Key, minRate, maxRate);
            var share90d = CollectWindowShare(rows, anchorTime, GetIntValue(_profile.FixedWindowDays, "short", 90), dominant.Key, minRate, maxRate);
            var share180d = CollectWindowShare(rows, anchorTime, GetIntValue(_profile.FixedWindowDays, "medium", 180), dominant.Key, minRate, maxRate);

            var weightedConsistency = WeightedAverage(
                [
                    concentration,
                    share180d.Share > 0 ? share180d.Share : concentration,
                    share90d.Share > 0 ? share90d.Share : concentration,
                    share30d.Share > 0 ? share30d.Share : concentration,
                ],
                [
                    GetDoubleValue(_profile.FixedConsistencyWeights, "concentration", 0.25),
                    GetDoubleValue(_profile.FixedConsistencyWeights, "medium", 0.25),
                    GetDoubleValue(_profile.FixedConsistencyWeights, "short", 0.25),
                    GetDoubleValue(_profile.FixedConsistencyWeights, "recent", 0.25),
                ]);

            var recencyPresence = Math.Min(100.0, ((share90d.Count / (double)Math.Max(rows.Count, 1)) * 100.0) * _profile.FixedRecencyMultiplier);
            var volatilityPenalty = Math.Min(100.0, (stdRate / Math.Max(_profile.FixedVolatilityReference, 0.00001)) * 100.0);
            var fixedScore = Math.Clamp(
                (concentration * GetDoubleValue(_profile.FixedScoreWeights, "concentration", 0.45))
                + (weightedConsistency * GetDoubleValue(_profile.FixedScoreWeights, "consistency", 0.35))
                + (recencyPresence * GetDoubleValue(_profile.FixedScoreWeights, "recency_presence", 0.20))
                - (volatilityPenalty * GetDoubleValue(_profile.FixedScoreWeights, "volatility_penalty", 0.15)),
                0.0,
                100.0);

            results.Add(new FixedBidderStat(
                Company: companyGroup.Key,
                MainZoneAverage: RoundRate(mainZoneAvg),
                Concentration: RoundScore(concentration),
                WindowConsistency: RoundScore(weightedConsistency),
                RecentShare30d: RoundScore(share30d.Share),
                RecentShare90d: RoundScore(share90d.Share),
                RecentShare180d: RoundScore(share180d.Share),
                RecentCount30d: share30d.Count,
                RecentCount90d: share90d.Count,
                RecentCount180d: share180d.Count,
                FixedScore: RoundScore(fixedScore),
                TotalBids: rows.Count,
                ZoneBids: dominant.Value,
                AverageRate: RoundRate(allRates.Average()),
                StandardDeviation: RoundRate(stdRate)));
        }

        return results
            .OrderByDescending(x => x.FixedScore)
            .ThenByDescending(x => x.Concentration)
            .ThenByDescending(x => x.TotalBids)
            .ToList();
    }

    private List<TrendStat> AnalyzeTrends(IReadOnlyList<RecommendationPoint> points, IReadOnlyList<FixedBidderStat> fixedBidders)
    {
        if (points.Count == 0 || fixedBidders.Count == 0)
        {
            return [];
        }

        var anchorTime = points.Max(x => x.Timestamp);
        var periods = _profile.PeriodDays
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var results = new List<TrendStat>();

        foreach (var bidder in fixedBidders)
        {
            var rows = points
                .Where(x => string.Equals(x.Company, bidder.Company, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Timestamp)
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var averages = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
            var medians = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, days) in periods)
            {
                var rates = rows
                    .Where(x => days <= 0 || x.Timestamp >= anchorTime.AddDays(-days))
                    .Select(x => x.Rate)
                    .ToList();
                averages[name] = rates.Count == 0 ? null : RoundRate(rates.Average());
                medians[name] = rates.Count == 0 ? null : RoundRate(Median(rates));
            }

            var longTerm = medians.TryGetValue("1y", out var median1y) && median1y.HasValue ? median1y
                : medians.TryGetValue("2y", out var median2y) && median2y.HasValue ? median2y
                : averages.TryGetValue("1y", out var avg1y) && avg1y.HasValue ? avg1y
                : averages.TryGetValue("2y", out var avg2y) && avg2y.HasValue ? avg2y
                : null;

            var shortTerm = medians.TryGetValue("1m", out var median1m) && median1m.HasValue ? median1m
                : medians.TryGetValue("3m", out var median3m) && median3m.HasValue ? median3m
                : averages.TryGetValue("1m", out var avg1m) && avg1m.HasValue ? avg1m
                : averages.TryGetValue("3m", out var avg3m) && avg3m.HasValue ? avg3m
                : null;

            var direction = "알수없음";
            double? velocity = null;
            if (longTerm.HasValue && shortTerm.HasValue)
            {
                var diff = shortTerm.Value - longTerm.Value;
                direction = diff > _profile.TrendDirectionThreshold
                    ? "상향조정"
                    : diff < -_profile.TrendDirectionThreshold
                        ? "하향조정"
                        : "유지";
                velocity = diff / 12.0;
            }

            var recentRates = rows
                .Take(Math.Max(1, _profile.TrendRecentSampleSize))
                .Select(x => x.Rate)
                .ToList();
            var recentAverage = recentRates.Count == 0 ? (double?)null : RoundRate(recentRates.Average());
            var recentMedian = recentRates.Count == 0 ? (double?)null : RoundRate(Median(recentRates));
            var recentStandardDeviation = recentRates.Count > 1 ? RoundRate(SampleStandardDeviation(recentRates)) : 0.0;

            results.Add(new TrendStat(
                Company: bidder.Company,
                Direction: direction,
                Velocity: velocity.HasValue ? RoundRate(velocity.Value) : null,
                RecentAverage: recentAverage,
                RecentMedian: recentMedian,
                RecentStandardDeviation: recentStandardDeviation));
        }

        return results;
    }

    private List<CompetitionZoneProfile> BuildCompetitionProfiles(
        IReadOnlyList<RecommendationPoint> points,
        IReadOnlyList<FixedBidderStat> fixedBidders,
        double minRate,
        double maxRate)
    {
        var numZones = GetZoneCount(minRate, maxRate);
        if (numZones <= 0)
        {
            return [];
        }

        var anchorTime = points.Max(x => x.Timestamp);
        var recent = Window(points, anchorTime, GetIntValue(_profile.CompetitionWindowDays, "recent", 30));
        var shortFrame = Window(points, anchorTime, GetIntValue(_profile.CompetitionWindowDays, "short", 90));
        var medium = Window(points, anchorTime, GetIntValue(_profile.CompetitionWindowDays, "medium", 180));
        var historical = HistoricalWindow(
            points,
            anchorTime,
            GetIntValue(_profile.CompetitionWindowDays, "historical_recent", 30),
            GetIntValue(_profile.CompetitionWindowDays, "historical_lookback", 180));

        var recentCounts = CollectZoneCounts(recent, minRate, maxRate, numZones);
        var shortCounts = CollectZoneCounts(shortFrame, minRate, maxRate, numZones);
        var mediumCounts = CollectZoneCounts(medium, minRate, maxRate, numZones);
        var historicalCounts = CollectZoneCounts(historical, minRate, maxRate, numZones);

        var recentUnique = recentCounts.CompanySets.Select(x => (double)x.Count).ToArray();
        var shortUnique = shortCounts.CompanySets.Select(x => (double)x.Count).ToArray();
        var mediumUnique = mediumCounts.CompanySets.Select(x => (double)x.Count).ToArray();

        var weightedCount = CombineCounts(
            recentCounts.Counts, shortCounts.Counts, mediumCounts.Counts,
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "recent", 0.50),
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "short", 0.30),
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "medium", 0.20));
        var weightedUnique = CombineCounts(
            recentUnique, shortUnique, mediumUnique,
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "recent", 0.50),
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "short", 0.30),
            GetDoubleValue(_profile.CompetitionWeightedCountWeights, "medium", 0.20));

        var normalizedRecent = recentCounts.Counts;
        var historicalScale = GetIntValue(_profile.CompetitionWindowDays, "historical_recent", 30)
                              / Math.Max(1.0, GetIntValue(_profile.CompetitionWindowDays, "historical_lookback", 180)
                                               - GetIntValue(_profile.CompetitionWindowDays, "historical_recent", 30));
        var normalizedHistorical = historicalCounts.Counts.Select(x => x * historicalScale).ToArray();
        var growthRaw = normalizedRecent.Zip(normalizedHistorical, (recentCount, historicalCount) => Math.Max(0.0, recentCount - historicalCount)).ToArray();
        var newcomerCount = Enumerable.Range(0, numZones)
            .Select(i => (double)recentCounts.CompanySets[i].Except(historicalCounts.CompanySets[i], StringComparer.OrdinalIgnoreCase).Count())
            .ToArray();
        var newcomerRatio = Enumerable.Range(0, numZones)
            .Select(i => newcomerCount[i] / Math.Max(recentUnique[i], 1.0))
            .ToArray();

        var countScore = Normalize(weightedCount);
        var uniqueScore = Normalize(weightedUnique);
        var growthScore = Normalize(growthRaw);
        var densityScore = Enumerable.Range(0, numZones)
            .Select(i =>
                ((countScore[i] * GetDoubleValue(_profile.CompetitionDensityWeights, "bid_count", 0.60))
                 + (uniqueScore[i] * GetDoubleValue(_profile.CompetitionDensityWeights, "company_count", 0.40))) * 100.0)
            .ToArray();
        var overheatScore = Enumerable.Range(0, numZones)
            .Select(i => Math.Clamp(
                ((growthScore[i] * GetDoubleValue(_profile.CompetitionOverheatWeights, "growth", 0.55))
                 + (newcomerRatio[i] * GetDoubleValue(_profile.CompetitionOverheatWeights, "newcomer_ratio", 0.45))) * 100.0,
                0.0,
                100.0))
            .ToArray();

        var anchorMap = BuildAnchorMap(fixedBidders, minRate, maxRate, numZones);
        var results = new List<CompetitionZoneProfile>(numZones);
        for (var zoneIndex = 0; zoneIndex < numZones; zoneIndex++)
        {
            var zoneStart = RoundRate(minRate + (zoneIndex * _profile.ZoneSize));
            var zoneEnd = RoundRate(Math.Min(maxRate, zoneStart + _profile.ZoneSize));
            var anchors = anchorMap[zoneIndex]
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.FixedScore)
                .ToList();
            var nearestAnchor = anchors.FirstOrDefault();

            results.Add(new CompetitionZoneProfile(
                ZoneStart: zoneStart,
                ZoneEnd: zoneEnd,
                ZoneCenter: RoundRate(zoneStart + (_profile.ZoneSize / 2.0)),
                DensityScore: RoundScore(densityScore[zoneIndex]),
                OverheatScore: RoundScore(overheatScore[zoneIndex]),
                NewcomerRatio: RoundRate(newcomerRatio[zoneIndex]),
                NearestFixedCompany: nearestAnchor?.Company ?? string.Empty,
                NearestFixedDistance: nearestAnchor?.Distance,
                NearestFixedRate: nearestAnchor?.Rate));
        }

        return results;
    }

    private List<GapZoneProfile> BuildGapProfiles(
        IReadOnlyList<CompetitionZoneProfile> competitionProfiles,
        IReadOnlyList<FixedBidderStat> fixedBidders,
        double minRate)
    {
        var results = new List<GapZoneProfile>(competitionProfiles.Count);
        foreach (var profile in competitionProfiles.OrderBy(x => x.ZoneStart))
        {
            var rightAnchor = FindNearestFixed(fixedBidders, profile.ZoneCenter, "right");
            var leftAnchor = FindNearestFixed(fixedBidders, profile.ZoneCenter, "left");

            var anchorScore = 0.0;
            if (rightAnchor is not null)
            {
                var proximity = Math.Max(0.0, 1.0 - (rightAnchor.Distance / Math.Max(_profile.GapMaxAnchorGap, 0.00001)));
                anchorScore = Math.Max(anchorScore, rightAnchor.FixedScore * proximity);
            }

            if (leftAnchor is not null)
            {
                var proximity = Math.Max(0.0, 1.0 - (leftAnchor.Distance / Math.Max(_profile.GapMaxAnchorGap, 0.00001)));
                anchorScore = Math.Max(anchorScore, leftAnchor.FixedScore * proximity * _profile.GapLeftAnchorDiscount);
            }

            var persistenceScore = Math.Clamp(
                100.0
                - (profile.DensityScore * GetDoubleValue(_profile.GapPersistenceWeights, "density", 0.65))
                - (profile.OverheatScore * GetDoubleValue(_profile.GapPersistenceWeights, "overheat", 0.35)),
                0.0,
                100.0);
            var calmScore = Math.Clamp(100.0 - (profile.NewcomerRatio * 100.0), 0.0, 100.0);
            var opportunityScore =
                (persistenceScore * GetDoubleValue(_profile.GapOpportunityWeights, "persistence", 0.55))
                + (anchorScore * GetDoubleValue(_profile.GapOpportunityWeights, "anchor", 0.35))
                + (calmScore * GetDoubleValue(_profile.GapOpportunityWeights, "calm", 0.10));
            if (rightAnchor is null && leftAnchor is null)
            {
                opportunityScore *= _profile.GapNoAnchorMultiplier;
            }

            results.Add(new GapZoneProfile(
                ZoneStart: profile.ZoneStart,
                ZoneEnd: profile.ZoneEnd,
                ZoneCenter: profile.ZoneCenter,
                DensityScore: profile.DensityScore,
                OverheatScore: profile.OverheatScore,
                AnchorScore: RoundScore(anchorScore),
                OpportunityScore: RoundScore(Math.Clamp(opportunityScore, 0.0, 100.0)),
                NearestRightCompany: rightAnchor?.Company ?? string.Empty,
                NearestRightDistance: rightAnchor?.Distance,
                NearestLeftCompany: leftAnchor?.Company ?? string.Empty,
                NearestLeftDistance: leftAnchor?.Distance,
                LeftBoundaryDistance: RoundRate(Math.Max(0.0, profile.ZoneCenter - minRate))));
        }

        return results;
    }

    private List<CompetitionRecommendationResult> BuildRecommendations(
        IReadOnlyList<RecommendationPoint> points,
        IReadOnlyList<FixedBidderStat> fixedBidders,
        IReadOnlyList<TrendStat> trends,
        IReadOnlyList<GapZoneProfile> gaps,
        double minRate,
        double maxRate)
    {
        var trendMap = trends.ToDictionary(x => x.Company, StringComparer.OrdinalIgnoreCase);
        var dedupe = new HashSet<(string Company, double RoundedRate)>();
        var results = new List<CompetitionRecommendationResult>();
        const int MinSupportingNoticeCount = 3;
        const int MinAwardNoticeCount = 1;

        foreach (var bidder in fixedBidders)
        {
            if (ContainsConfiguredToken(bidder.Company, _profile.MyCompanyKeyword))
            {
                continue;
            }

            trendMap.TryGetValue(bidder.Company, out var trend);
            var basePosition = trend?.RecentMedian ?? trend?.RecentAverage ?? bidder.MainZoneAverage;
            var direction = trend?.Direction ?? "알수없음";
            var velocity = Math.Abs(trend?.Velocity ?? 0.0);
            var recentStd = trend?.RecentStandardDeviation ?? bidder.StandardDeviation;

            var offset = _profile.ShadowBaseOffset
                         + Math.Min(_profile.ShadowOffsetStdCap, recentStd * _profile.ShadowOffsetStdMultiplier);
            if (string.Equals(direction, "하향조정", StringComparison.OrdinalIgnoreCase))
            {
                offset += Math.Min(_profile.ShadowDownwardVelocityCap, velocity * _profile.ShadowDownwardVelocityMultiplier);
            }
            else if (string.Equals(direction, "상향조정", StringComparison.OrdinalIgnoreCase))
            {
                offset = Math.Max(_profile.ShadowMinOffset, offset * _profile.ShadowUpwardOffsetMultiplier);
            }

            var candidateRate = ClampRate(basePosition - offset, minRate, maxRate);
            var bandHalf = Math.Max(
                _profile.ShadowBandMinHalf,
                Math.Min(
                    _profile.ShadowBandMaxHalf,
                    (recentStd * _profile.ShadowBandStdMultiplier) + _profile.ShadowBandBaseHalf));
            var bandMin = ClampRate(candidateRate - bandHalf, minRate, maxRate);
            var bandMax = ClampRate(candidateRate + bandHalf, minRate, maxRate);

            var gap = gaps.FirstOrDefault(x => RateFallsInZone(candidateRate, x.ZoneStart, x.ZoneEnd, maxRate));
            if (gap is null)
            {
                continue;
            }

            var bandRows = points
                .Where(x => x.Rate >= bandMin && x.Rate <= bandMax)
                .ToList();
            var supportingNoticeCount = bandRows
                .Select(x => x.Notice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var awardNoticeCount = bandRows
                .Where(x => x.Rank == 1)
                .Select(x => x.Notice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (supportingNoticeCount < MinSupportingNoticeCount || awardNoticeCount < MinAwardNoticeCount)
            {
                continue;
            }

            var movementScore = GetDoubleValue(_profile.ShadowMovementScores, direction, 50.0);
            var volatilityPenalty = Math.Min(100.0, (recentStd / Math.Max(_profile.ShadowVolatilityReference, 0.00001)) * 100.0);
            var positive =
                (bidder.FixedScore * GetDoubleValue(_profile.ShadowPositiveWeights, "fixed", 0.35))
                + (gap.OpportunityScore * GetDoubleValue(_profile.ShadowPositiveWeights, "gap", 0.35))
                + (gap.AnchorScore * GetDoubleValue(_profile.ShadowPositiveWeights, "anchor", 0.15))
                + (movementScore * GetDoubleValue(_profile.ShadowPositiveWeights, "movement", 0.15));
            var negative =
                (gap.DensityScore * GetDoubleValue(_profile.ShadowNegativeWeights, "density", 0.45))
                + (gap.OverheatScore * GetDoubleValue(_profile.ShadowNegativeWeights, "overheat", 0.35))
                + (volatilityPenalty * GetDoubleValue(_profile.ShadowNegativeWeights, "volatility", 0.20));
            var evidenceScore = Math.Clamp(positive - negative + _profile.ShadowEvidenceBonus, 0.0, 100.0);
            if (evidenceScore < _profile.ShadowMinimumPriorityScore)
            {
                continue;
            }

            var roundedRate = Math.Round(candidateRate, _profile.ShadowDedupePrecision);
            if (!dedupe.Add((bidder.Company, roundedRate)))
            {
                continue;
            }

            var crowdWeightsTotal = GetDoubleValue(_profile.ShadowNegativeWeights, "density", 0.45)
                                    + GetDoubleValue(_profile.ShadowNegativeWeights, "overheat", 0.35);
            var crowdPenalty = crowdWeightsTotal <= 0
                ? (gap.DensityScore + gap.OverheatScore) / 2.0
                : (
                    (gap.DensityScore * GetDoubleValue(_profile.ShadowNegativeWeights, "density", 0.45))
                    + (gap.OverheatScore * GetDoubleValue(_profile.ShadowNegativeWeights, "overheat", 0.35)))
                    / crowdWeightsTotal;
            var crowdAvoidanceScore = Math.Clamp(100.0 - crowdPenalty, 0.0, 100.0);
            var anchorCompany = gap.NearestRightCompany;
            var targetCompany = string.IsNullOrWhiteSpace(anchorCompany) ? bidder.Company : anchorCompany;
            var anchorGap = gap.NearestRightDistance ?? RoundRate(Math.Max(0.0, bidder.MainZoneAverage - candidateRate));
            var leftBoundaryDistance = ResolveRecommendationBoundaryDistance(points, candidateRate, gap);
            var leftSpaceSource = gap.NearestLeftDistance.HasValue ? "left_anchor" : "boundary";
            var reasonCodes = BuildReasonCodes(gap, volatilityPenalty, leftBoundaryDistance, anchorCompany);
            var decisionCategory = ClassifyDecisionCategory(gap, anchorCompany);
            if (!string.Equals(decisionCategory, "candidate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var uncertaintyLevel = ClassifyUncertainty(gap, volatilityPenalty, supportingNoticeCount: 0, anchorCompanyOverride: anchorCompany);
            uncertaintyLevel = ClassifyUncertainty(gap, volatilityPenalty, supportingNoticeCount, anchorCompanyOverride: anchorCompany);
            var reasonSummary = BuildReasonSummary(gap, uncertaintyLevel, leftBoundaryDistance);

            results.Add(new CompetitionRecommendationResult(
                HasRecommendation: true,
                SourceCompany: bidder.Company,
                TargetCompany: targetCompany,
                TrendDirection: direction,
                RecommendedRate: RoundRate(candidateRate),
                BandMin: RoundRate(bandMin),
                BandMax: RoundRate(bandMax),
                CompetitorSampleCount: points.Count,
                BandCompetitorCount: bandRows.Count,
                SupportingNoticeCount: supportingNoticeCount,
                AwardNoticeCount: awardNoticeCount,
                DecisionCategory: decisionCategory,
                UncertaintyLevel: uncertaintyLevel,
                AnchorCompany: anchorCompany,
                AnchorGap: anchorGap,
                LeftSpaceWidth: gap.NearestLeftDistance,
                LeftBoundaryDistance: leftBoundaryDistance,
                LeftSpaceSource: leftSpaceSource,
                ReasonSummary: reasonSummary,
                ReasonCodes: reasonCodes,
                FixedScore: bidder.FixedScore,
                AnchorScore: gap.AnchorScore,
                GapOpportunityScore: gap.OpportunityScore,
                CrowdAvoidanceScore: RoundScore(crowdAvoidanceScore),
                DensityPenalty: gap.DensityScore,
                OverheatPenalty: gap.OverheatScore,
                VolatilityPenalty: RoundScore(volatilityPenalty),
                EvidenceScore: RoundScore(evidenceScore)));
        }

        return results
            .OrderByDescending(x => x.EvidenceScore)
            .ThenByDescending(x => x.FixedScore)
            .ThenBy(x => x.DensityPenalty)
            .ToList();
    }

    private List<CompetitionZoneDecision> BuildZoneDecisions(
        IReadOnlyList<CompetitionZoneProfile> competitionProfiles,
        IReadOnlyList<GapZoneProfile> gaps)
    {
        return gaps
            .Select(gap =>
            {
                var anchorCompany = gap.NearestRightCompany;
                var reasonCodes = BuildReasonCodes(gap, volatilityPenalty: 0.0, leftBoundaryDistance: gap.LeftBoundaryDistance, anchorCompanyOverride: anchorCompany);
                var uncertainty = ClassifyUncertainty(gap, volatilityPenalty: 0.0, supportingNoticeCount: 0, anchorCompanyOverride: anchorCompany);
                var category = ClassifyDecisionCategory(gap, anchorCompany);

                return new CompetitionZoneDecision(
                    Category: category,
                    ZoneStart: gap.ZoneStart,
                    ZoneEnd: gap.ZoneEnd,
                    AnchorCompany: anchorCompany,
                    AnchorGap: gap.NearestRightDistance,
                    LeftSpaceWidth: gap.NearestLeftDistance,
                    LeftBoundaryDistance: gap.LeftBoundaryDistance,
                    LeftSpaceSource: gap.NearestLeftDistance.HasValue ? "left_anchor" : "boundary",
                    DensityScore: gap.DensityScore,
                    OverheatScore: gap.OverheatScore,
                    OpportunityScore: gap.OpportunityScore,
                    UncertaintyLevel: uncertainty,
                    ReasonCodes: reasonCodes);
            })
            .OrderBy(x => x.ZoneStart)
            .ToList();
    }

    private List<AnchorStat>[] BuildAnchorMap(IReadOnlyList<FixedBidderStat> fixedBidders, double minRate, double maxRate, int numZones)
    {
        var anchors = Enumerable.Range(0, numZones)
            .Select(_ => new List<AnchorStat>())
            .ToArray();
        foreach (var bidder in fixedBidders)
        {
            var fixedZone = TryGetZoneIndex(bidder.MainZoneAverage, minRate, maxRate);
            if (!fixedZone.HasValue)
            {
                continue;
            }

            for (var offset = -_profile.CompetitionFixedAnchorSearchZoneCount; offset <= _profile.CompetitionFixedAnchorSearchZoneCount; offset++)
            {
                var target = fixedZone.Value + offset;
                if (target < 0 || target >= numZones)
                {
                    continue;
                }

                anchors[target].Add(new AnchorStat(
                    Company: bidder.Company,
                    Rate: bidder.MainZoneAverage,
                    Distance: RoundRate(Math.Abs(offset) * _profile.ZoneSize),
                    FixedScore: bidder.FixedScore));
            }
        }

        return anchors;
    }

    private AnchorStat? FindNearestFixed(IReadOnlyList<FixedBidderStat> fixedBidders, double rate, string direction)
    {
        return fixedBidders
            .Select(x =>
            {
                var distance = x.MainZoneAverage - rate;
                return new { Bidder = x, Distance = distance };
            })
            .Where(x => direction switch
            {
                "right" => x.Distance > 0,
                "left" => x.Distance < 0,
                _ => false,
            })
            .Select(x => new AnchorStat(
                Company: x.Bidder.Company,
                Rate: x.Bidder.MainZoneAverage,
                Distance: RoundRate(Math.Abs(x.Distance)),
                FixedScore: x.Bidder.FixedScore))
            .Where(x => x.Distance <= _profile.GapMaxAnchorGap)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.FixedScore)
            .FirstOrDefault();
    }

    private ZoneCounts CollectZoneCounts(IReadOnlyList<RecommendationPoint> points, double minRate, double maxRate, int numZones)
    {
        var counts = new double[numZones];
        var companySets = Enumerable.Range(0, numZones)
            .Select(_ => new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .ToArray();

        foreach (var point in points)
        {
            var zone = TryGetZoneIndex(point.Rate, minRate, maxRate);
            if (!zone.HasValue)
            {
                continue;
            }

            counts[zone.Value] += 1.0;
            companySets[zone.Value].Add(point.Company);
        }

        return new ZoneCounts(counts, companySets);
    }

    private static IReadOnlyList<RecommendationPoint> Window(IReadOnlyList<RecommendationPoint> points, DateTime anchorTime, int days)
    {
        var cutoff = anchorTime.AddDays(-days);
        return points.Where(x => x.Timestamp >= cutoff).ToList();
    }

    private static IReadOnlyList<RecommendationPoint> HistoricalWindow(IReadOnlyList<RecommendationPoint> points, DateTime anchorTime, int recentDays, int lookbackDays)
    {
        var recentCutoff = anchorTime.AddDays(-recentDays);
        var lookbackCutoff = anchorTime.AddDays(-lookbackDays);
        return points.Where(x => x.Timestamp < recentCutoff && x.Timestamp >= lookbackCutoff).ToList();
    }

    private (double Share, int Count) CollectWindowShare(
        IReadOnlyList<RecommendationPoint> rows,
        DateTime anchorTime,
        int days,
        int mainIndex,
        double minRate,
        double maxRate)
    {
        var cutoff = anchorTime.AddDays(-days);
        var bins = rows
            .Where(x => x.Timestamp >= cutoff)
            .Select(x => (Rate: x.Rate, Index: TryGetZoneIndex(x.Rate, minRate, maxRate)))
            .Where(x => x.Index.HasValue)
            .ToList();
        if (bins.Count == 0)
        {
            return (0.0, 0);
        }

        var aligned = bins.Count(x => Math.Abs(x.Index!.Value - mainIndex) <= 1);
        return (aligned / (double)bins.Count * 100.0, bins.Count);
    }

    private static double[] CombineCounts(double[] recent, double[] shortFrame, double[] medium, double recentWeight, double shortWeight, double mediumWeight)
    {
        return Enumerable.Range(0, recent.Length)
            .Select(i => (recent[i] * recentWeight) + (shortFrame[i] * shortWeight) + (medium[i] * mediumWeight))
            .ToArray();
    }

    private static double[] Normalize(double[] values)
    {
        if (values.Length == 0)
        {
            return values;
        }

        var maxValue = values.Max();
        if (maxValue <= 0)
        {
            return values.Select(_ => 0.0).ToArray();
        }

        return values.Select(x => x / maxValue).ToArray();
    }

    private int GetZoneCount(double minRate, double maxRate)
    {
        return Math.Max(1, (int)Math.Round((maxRate - minRate) / Math.Max(_profile.ZoneSize, 0.00001), MidpointRounding.AwayFromZero));
    }

    private int? TryGetZoneIndex(double rate, double minRate, double maxRate)
    {
        var zoneSize = Math.Max(_profile.ZoneSize, 0.00001);
        var count = GetZoneCount(minRate, maxRate);
        if (count <= 0 || rate < minRate)
        {
            return null;
        }

        if (rate >= maxRate)
        {
            return count - 1;
        }

        var raw = (rate - minRate) / zoneSize;
        var index = (int)Math.Floor(raw + 1e-12);
        return index >= 0 && index < count ? index : null;
    }

    private static bool RateFallsInZone(double rate, double zoneStart, double zoneEnd, double maxRate)
    {
        if (rate < zoneStart)
        {
            return false;
        }

        if (rate < zoneEnd)
        {
            return true;
        }

        return Math.Abs(rate - maxRate) <= 1e-9 && Math.Abs(zoneEnd - maxRate) <= 1e-9;
    }

    private static double ClampRate(double value, double minRate, double maxRate)
        => RoundRate(Math.Clamp(value, minRate, maxRate));

    private static bool ContainsConfiguredToken(string? source, string? token)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var left = NormalizeToken(source);
        var right = NormalizeToken(token);
        return left.Length > 0 && right.Length > 0 && left.Contains(right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var ordered = values.OrderBy(x => x).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static double PopulationStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private static double SampleStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private static double WeightedAverage(IReadOnlyList<double> values, IReadOnlyList<double> weights)
    {
        var weightSum = weights.Sum();
        if (values.Count == 0 || weights.Count == 0 || weightSum <= 0)
        {
            return 0.0;
        }

        var length = Math.Min(values.Count, weights.Count);
        var weightedSum = 0.0;
        for (var i = 0; i < length; i++)
        {
            weightedSum += values[i] * weights[i];
        }

        return weightedSum / weightSum;
    }

    private static int GetIntValue(IReadOnlyDictionary<string, int> values, string key, int fallback)
        => values.TryGetValue(key, out var value) ? value : fallback;

    private static double GetDoubleValue(IReadOnlyDictionary<string, double> values, string key, double fallback)
        => values.TryGetValue(key, out var value) ? value : fallback;

    private static double RoundRate(double value) => Math.Round(value, 5);

    private static double RoundScore(double value) => Math.Round(value, 2);

    private static double? ResolveRecommendationBoundaryDistance(
        IReadOnlyList<RecommendationPoint> points,
        double candidateRate,
        GapZoneProfile gap)
    {
        if (gap.NearestLeftDistance.HasValue)
        {
            return RoundRate(gap.LeftBoundaryDistance);
        }

        var observedLeftDistance = points
            .Where(x => x.Rate < candidateRate)
            .Select(x => candidateRate - x.Rate)
            .OrderBy(x => x)
            .FirstOrDefault();
        if (observedLeftDistance > 0)
        {
            return RoundRate(observedLeftDistance);
        }

        return RoundRate(gap.LeftBoundaryDistance);
    }

    private string ClassifyDecisionCategory(GapZoneProfile gap, string anchorCompany)
    {
        if (gap.DensityScore >= _profile.DecisionForbiddenDensityScore || gap.OverheatScore >= _profile.DecisionForbiddenOverheatScore)
        {
            return "forbidden";
        }

        if (!string.IsNullOrWhiteSpace(anchorCompany)
            && IsLeftSpaceWide(gap.NearestLeftDistance, gap.NearestRightDistance)
            && gap.OpportunityScore >= _profile.DecisionCandidateMinOpportunityScore
            && gap.DensityScore <= _profile.DecisionCandidateMaxDensityScore
            && gap.OverheatScore <= _profile.DecisionCandidateMaxOverheatScore)
        {
            return "candidate";
        }

        return "boundary";
    }

    private IReadOnlyList<string> BuildReasonCodes(
        GapZoneProfile gap,
        double volatilityPenalty,
        double? leftBoundaryDistance,
        string? anchorCompanyOverride = null)
    {
        var codes = new List<string>();
        var anchorCompany = string.IsNullOrWhiteSpace(anchorCompanyOverride)
            ? gap.NearestRightCompany
            : anchorCompanyOverride;
        if (gap.DensityScore >= _profile.DecisionForbiddenDensityScore)
        {
            codes.Add("EXCESS_DENSITY");
        }
        if (gap.OverheatScore >= _profile.DecisionForbiddenOverheatScore)
        {
            codes.Add("OVERHEAT");
        }
        if (!string.IsNullOrWhiteSpace(anchorCompany))
        {
            codes.Add("LEFT_OF_COLLISION_ANCHOR");
        }
        if (IsLeftSpaceWide(gap.NearestLeftDistance, gap.NearestRightDistance))
        {
            codes.Add("LEFT_SPACE_WIDE");
        }
        else if (!gap.NearestLeftDistance.HasValue)
        {
            codes.Add("LEFT_SPACE_UNVERIFIED");
            if (leftBoundaryDistance.GetValueOrDefault() >= _profile.DecisionBoundaryClearMinDistance)
            {
                codes.Add("LEFT_BOUNDARY_CLEAR");
            }
        }
        else
        {
            codes.Add("LEFT_SPACE_TOO_NARROW");
        }
        if (gap.DensityScore <= _profile.DecisionCalmBandMaxDensityScore && gap.OverheatScore <= _profile.DecisionCalmBandMaxOverheatScore)
        {
            codes.Add("CALM_BAND");
        }
        if (gap.OverheatScore <= _profile.DecisionLowNewcomerOverheatScore)
        {
            codes.Add("LOW_NEWCOMER_RATIO");
        }
        if (gap.DensityScore <= _profile.DecisionLessObviousDensityScore)
        {
            codes.Add("LESS_OBVIOUS");
        }
        if (volatilityPenalty >= _profile.DecisionUncertaintyHighVolatilityPenalty)
        {
            codes.Add("UNCERTAINTY_HIGH");
        }

        return codes;
    }

    private static string BuildReasonSummary(GapZoneProfile gap, string uncertaintyLevel, double? leftBoundaryDistance)
    {
        var anchorName = string.IsNullOrWhiteSpace(gap.NearestRightCompany) ? "앵커미상" : gap.NearestRightCompany;
        var anchorGap = gap.NearestRightDistance.HasValue ? $"{gap.NearestRightDistance.Value:0.###}" : "-";
        if (!gap.NearestLeftDistance.HasValue)
        {
            var boundary = leftBoundaryDistance.HasValue ? $"{leftBoundaryDistance.Value:0.###}" : "-";
            return $"{anchorName} 왼쪽 · 앵커간격 {anchorGap} / 좌측앵커없음(경계 {boundary}) / 불확실성 {uncertaintyLevel}";
        }

        var leftSpace = $"{gap.NearestLeftDistance.Value:0.###}";
        return $"{anchorName} 왼쪽 · 앵커간격 {anchorGap} / 좌측여유 {leftSpace} / 불확실성 {uncertaintyLevel}";
    }

    private string ClassifyUncertainty(
        GapZoneProfile gap,
        double volatilityPenalty,
        int supportingNoticeCount,
        string? anchorCompanyOverride = null)
    {
        var anchorCompany = string.IsNullOrWhiteSpace(anchorCompanyOverride)
            ? gap.NearestRightCompany
            : anchorCompanyOverride;
        if (string.IsNullOrWhiteSpace(anchorCompany)
            || !gap.NearestLeftDistance.HasValue
            || gap.DensityScore >= _profile.DecisionUncertaintyHighDensityScore
            || gap.OverheatScore >= _profile.DecisionUncertaintyHighOverheatScore
            || volatilityPenalty >= _profile.DecisionUncertaintyHighVolatilityPenalty
            || supportingNoticeCount <= 1)
        {
            return "high";
        }

        if (!IsLeftSpaceWide(gap.NearestLeftDistance, gap.NearestRightDistance)
            || gap.DensityScore >= _profile.DecisionUncertaintyMediumDensityScore
            || gap.OverheatScore >= _profile.DecisionUncertaintyMediumOverheatScore
            || volatilityPenalty >= _profile.DecisionUncertaintyMediumVolatilityPenalty
            || supportingNoticeCount <= 3)
        {
            return "medium";
        }

        return "low";
    }

    private bool IsLeftSpaceWide(double? leftSpaceWidth, double? anchorGap)
    {
        if (!leftSpaceWidth.HasValue)
        {
            return false;
        }

        if (!anchorGap.HasValue)
        {
            return leftSpaceWidth.Value >= _profile.DecisionMinLeftSpaceWidth;
        }

        return leftSpaceWidth.Value >= Math.Max(anchorGap.Value * _profile.DecisionWideLeftSpaceAnchorMultiplier, _profile.DecisionMinLeftSpaceWidth);
    }

    private sealed record RecommendationPoint(
        double Rate,
        DateTime Timestamp,
        string Company,
        string Notice,
        int Rank);

    private sealed record FixedBidderStat(
        string Company,
        double MainZoneAverage,
        double Concentration,
        double WindowConsistency,
        double RecentShare30d,
        double RecentShare90d,
        double RecentShare180d,
        int RecentCount30d,
        int RecentCount90d,
        int RecentCount180d,
        double FixedScore,
        int TotalBids,
        int ZoneBids,
        double AverageRate,
        double StandardDeviation);

    private sealed record TrendStat(
        string Company,
        string Direction,
        double? Velocity,
        double? RecentAverage,
        double? RecentMedian,
        double RecentStandardDeviation);

    private sealed record AnchorStat(
        string Company,
        double Rate,
        double Distance,
        double FixedScore);

    private sealed record CompetitionZoneProfile(
        double ZoneStart,
        double ZoneEnd,
        double ZoneCenter,
        double DensityScore,
        double OverheatScore,
        double NewcomerRatio,
        string NearestFixedCompany,
        double? NearestFixedDistance,
        double? NearestFixedRate);

    private sealed record GapZoneProfile(
        double ZoneStart,
        double ZoneEnd,
        double ZoneCenter,
        double DensityScore,
        double OverheatScore,
        double AnchorScore,
        double OpportunityScore,
        string NearestRightCompany,
        double? NearestRightDistance,
        string NearestLeftCompany,
        double? NearestLeftDistance,
        double LeftBoundaryDistance);

    private sealed record ZoneCounts(double[] Counts, HashSet<string>[] CompanySets);
}

public sealed record CompetitionZoneDecision(
    string Category,
    double ZoneStart,
    double ZoneEnd,
    string AnchorCompany,
    double? AnchorGap,
    double? LeftSpaceWidth,
    double? LeftBoundaryDistance,
    string LeftSpaceSource,
    double DensityScore,
    double OverheatScore,
    double OpportunityScore,
    string UncertaintyLevel,
    IReadOnlyList<string> ReasonCodes);

public sealed record CompetitionStrategyAnalysisResult(
    int CompetitorSampleCount,
    int FixedBidderCount,
    IReadOnlyList<CompetitionRecommendationResult> Recommendations,
    IReadOnlyList<CompetitionZoneDecision> ZoneDecisions);
