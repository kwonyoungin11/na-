using System.Globalization;
using System.Text.Json;
using NaraCollector.Desktop.Models;
using System.IO;

namespace NaraCollector.Desktop.Services;

public sealed record CSharpPipelineOptions(
    string SourceFile,
    string OutputRoot,
    string Region,
    string Period,
    double MinRate,
    double MaxRate);

public sealed class CSharpAnalysisPipelineRunner
{
    public const string CurrentSnapshotSignature = "console-analysis-2026-03-09.1";

    private readonly IAppPathResolver _pathResolver;

    public CSharpAnalysisPipelineRunner(IAppPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new DefaultAppPathResolver();
    }

    public string Execute(CSharpPipelineOptions options)
    {
        var sourceFile = _pathResolver.ResolveMasterPath(options.SourceFile);
        var analysisProfilePath = _pathResolver.ResolveAnalysisProfilePath();
        var outputRoot = string.IsNullOrWhiteSpace(options.OutputRoot)
            ? _pathResolver.ResolveConsoleAnalysisRootPath()
            : options.OutputRoot;
        Directory.CreateDirectory(outputRoot);
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var sourceLastWriteTimeUtc = ReadLastWriteTimeUtc(sourceFile);
        var profileLastWriteTimeUtc = ReadLastWriteTimeUtc(analysisProfilePath);

        var parser = new NaraApiParticipantParser();
        var allRows = parser.LoadParticipantsFromMasterExcel(
            sourceFile,
            new DashboardFilter(
                StartDate: new DateTime(2000, 1, 1),
                EndDate: new DateTime(2100, 1, 1),
                Region: "전체",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: string.Empty,
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null),
            CancellationToken.None);

        var filteredRows = FilterParticipants(allRows, options.Region, options.Period);
        var analysisRows = PrepareAnalysisRows(filteredRows, options.MinRate, options.MaxRate);
        var normalizedRegion = NormalizeRegion(options.Region);
        var crowdingDir = Path.Combine(outputRoot, $"crowding_{normalizedRegion}");
        var collisionDir = Path.Combine(outputRoot, $"collision_{normalizedRegion}");
        var shadowDir = Path.Combine(outputRoot, $"shadow_{normalizedRegion}");
        Directory.CreateDirectory(crowdingDir);
        Directory.CreateDirectory(collisionDir);
        Directory.CreateDirectory(shadowDir);

        if (filteredRows.Count == 0)
        {
            WriteStatus(shadowDir, new
            {
                snapshot_signature = CurrentSnapshotSignature,
                generated_at_utc = generatedAtUtc,
                source_file = sourceFile,
                source_file_last_write_time_utc = sourceLastWriteTimeUtc,
                analysis_profile_path = analysisProfilePath,
                analysis_profile_last_write_time_utc = profileLastWriteTimeUtc,
                status = "skipped_empty_input",
                region = normalizedRegion,
                rows = 0,
                notices = 0,
            });
            return Path.Combine(shadowDir, "pipeline_status.json");
        }

        var rateWindowRows = analysisRows.Count;
        if (rateWindowRows == 0)
        {
            WriteStatus(shadowDir, new
            {
                snapshot_signature = CurrentSnapshotSignature,
                generated_at_utc = generatedAtUtc,
                source_file = sourceFile,
                source_file_last_write_time_utc = sourceLastWriteTimeUtc,
                analysis_profile_path = analysisProfilePath,
                analysis_profile_last_write_time_utc = profileLastWriteTimeUtc,
                status = "skipped_empty_rate_window",
                region = normalizedRegion,
                rows = filteredRows.Count,
                notices = filteredRows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                rate_window = new { rows = 0 },
            });
            return Path.Combine(shadowDir, "pipeline_status.json");
        }

        var crowding = new CrowdingAnalysisService().Analyze(analysisRows, new CrowdingAnalysisOptions(0.001, options.MinRate, options.MaxRate));
        new CrowdingAnalysisService().SaveSummary(crowding, crowdingDir);

        var collision = new CollisionAnalysisService().Analyze(analysisRows, new CollisionAnalysisOptions(20, options.MinRate, options.MaxRate));
        new CollisionAnalysisService().SaveSummary(collision, collisionDir);

        var recommendationEngine = new CompetitionRecommendationEngine();
        var strategy = recommendationEngine.AnalyzeDetailed(analysisRows, options.MinRate, options.MaxRate);
        var (conditionalAdvantage, conditionalSummary) = BuildConditionalAdvantagePayload(crowding, collision, strategy);
        WriteShadowStrategy(
            shadowDir,
            strategy,
            options.Period,
            conditionalAdvantage,
            conditionalSummary,
            generatedAtUtc,
            sourceFile,
            sourceLastWriteTimeUtc,
            analysisProfilePath,
            profileLastWriteTimeUtc);
        File.WriteAllText(
            Path.Combine(shadowDir, "conditional_advantage.json"),
            JsonSerializer.Serialize(conditionalAdvantage, new JsonSerializerOptions { WriteIndented = true }));

        WriteStatus(shadowDir, new
        {
            snapshot_signature = CurrentSnapshotSignature,
            generated_at_utc = generatedAtUtc,
            source_file = sourceFile,
            source_file_last_write_time_utc = sourceLastWriteTimeUtc,
            analysis_profile_path = analysisProfilePath,
            analysis_profile_last_write_time_utc = profileLastWriteTimeUtc,
            status = "completed",
            region = normalizedRegion,
            rows = filteredRows.Count,
            notices = filteredRows.Select(x => x.NoticeNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            rate_window = new { rows = rateWindowRows, min = options.MinRate, max = options.MaxRate },
            crowding_key_findings = new
            {
                crowded_band_lift = crowding.KeyFindings.CrowdedBandLift,
                sparse_band_lift = crowding.KeyFindings.SparseBandLift,
                crowded_notice_expected_win_rate = crowding.KeyFindings.CrowdedNoticeExpectedWinRate,
                sparse_notice_expected_win_rate = crowding.KeyFindings.SparseNoticeExpectedWinRate,
                crowded_notice_adjust_rate_mean = crowding.KeyFindings.CrowdedNoticeAdjustRateMean,
                sparse_notice_adjust_rate_mean = crowding.KeyFindings.SparseNoticeAdjustRateMean,
            },
            collision_key_findings = new
            {
                wide_left_space_lift = collision.KeyFindings.WideLeftSpaceLift,
                narrow_left_space_lift = collision.KeyFindings.NarrowLeftSpaceLift,
                high_left_advantage_lift = collision.KeyFindings.HighLeftAdvantageLift,
                low_left_advantage_lift = collision.KeyFindings.LowLeftAdvantageLift,
                best_matrix_cell = collision.KeyFindings.BestMatrixCell is null ? null : new
                {
                    우측앵커거리구간 = collision.KeyFindings.BestMatrixCell.RightAnchorTier,
                    좌측여유구간 = collision.KeyFindings.BestMatrixCell.LeftSpaceTier,
                    낙찰리프트 = collision.KeyFindings.BestMatrixCell.WinLift,
                    표본수 = collision.KeyFindings.BestMatrixCell.SampleCount,
                },
            },
            shadow_summary = new
            {
                period = options.Period,
                fixed_bidders = strategy.FixedBidderCount,
                recommendations = strategy.Recommendations.Count,
                self_company_policy = "excluded_from_market_structure",
            },
            conditional_advantage = conditionalAdvantage,
            conditional_advantage_summary = conditionalSummary,
            outputs = new
            {
                crowding = crowdingDir,
                collision = collisionDir,
                shadow = shadowDir,
                conditional_advantage = Path.Combine(shadowDir, "conditional_advantage.json"),
            },
        });

        return Path.Combine(shadowDir, "pipeline_status.json");
    }

    internal static List<ParticipantRecord> PrepareAnalysisRows(
        IReadOnlyList<ParticipantRecord> participants,
        double minRate,
        double maxRate,
        CompetitionAnalysisProfile? profile = null)
    {
        var safeMinRate = Math.Min(minRate, maxRate);
        var safeMaxRate = Math.Max(minRate, maxRate);
        var rowsInWindow = participants
            .Where(x => x.AdjustRate.HasValue)
            .Where(x => x.AdjustRate!.Value >= safeMinRate && x.AdjustRate.Value <= safeMaxRate)
            .ToList();
        if (rowsInWindow.Count == 0)
        {
            return rowsInWindow;
        }

        profile ??= CompetitionAnalysisProfileProvider.Current;
        return ParticipantSameDayClusterReducer
            .Collapse(
                rowsInWindow,
                profile.SameDayClusterRateTolerance,
                profile.SameDayClusterMinClusterSize)
            .ToList();
    }

    internal static List<ParticipantRecord> FilterParticipants(IEnumerable<ParticipantRecord> participants, string region, string period)
    {
        var normalizedRegion = NormalizeRegion(region);
        var rows = participants
            .Where(x => RegionMatches(x.Region, normalizedRegion))
            .ToList();
        if (rows.Count == 0)
        {
            return rows;
        }

        var anchor = rows.Max(x => x.OpenDate);
        if (string.Equals(period, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(period, "전체기간", StringComparison.OrdinalIgnoreCase))
        {
            return rows
                .OrderBy(x => x.OpenDate)
                .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                .ToList();
        }

        var cutoff = period switch
        {
            "1m" => anchor.AddMonths(-1),
            "3m" => anchor.AddMonths(-3),
            "6m" => anchor.AddMonths(-6),
            "1y" => anchor.AddYears(-1),
            "2y" => anchor.AddYears(-2),
            _ => anchor.AddMonths(-3),
        };

        return rows
            .Where(x => x.OpenDate >= cutoff)
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ToList();
    }

    internal static bool RegionMatches(string? rowRegion, string normalizedRegion)
    {
        if (string.Equals(normalizedRegion, "전체", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var token = (rowRegion ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return normalizedRegion switch
        {
            "대구" => token.Contains("대구", StringComparison.OrdinalIgnoreCase),
            "경북" => token.Contains("경북", StringComparison.OrdinalIgnoreCase) || token.Contains("경상북", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(token, normalizedRegion, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string NormalizeRegion(string? region)
    {
        var token = (region ?? string.Empty).Trim();
        return token switch
        {
            "전체" => "전체",
            _ when token.Contains("전국", StringComparison.Ordinal) => "전국",
            _ when token.Contains("대구", StringComparison.Ordinal) => "대구",
            _ when token.Contains("경북", StringComparison.Ordinal) || token.Contains("경상북", StringComparison.Ordinal) => "경북",
            _ => string.IsNullOrWhiteSpace(token) ? "전국" : token,
        };
    }

    private static DateTimeOffset? ReadLastWriteTimeUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return File.GetLastWriteTimeUtc(path);
    }

    private static void WriteShadowStrategy(
        string shadowDir,
        CompetitionStrategyAnalysisResult strategy,
        string period,
        object conditionalAdvantage,
        object conditionalSummary,
        DateTimeOffset generatedAtUtc,
        string sourceFile,
        DateTimeOffset? sourceLastWriteTimeUtc,
        string analysisProfilePath,
        DateTimeOffset? analysisProfileLastWriteTimeUtc)
    {
        Directory.CreateDirectory(shadowDir);
        var payload = new
        {
            snapshot_signature = CurrentSnapshotSignature,
            generated_at_utc = generatedAtUtc,
            source_file = sourceFile,
            source_file_last_write_time_utc = sourceLastWriteTimeUtc,
            analysis_profile_path = analysisProfilePath,
            analysis_profile_last_write_time_utc = analysisProfileLastWriteTimeUtc,
            summary = new
            {
                period,
                competitor_samples = strategy.CompetitorSampleCount,
                fixed_bidders = strategy.FixedBidderCount,
                recommendations = strategy.Recommendations.Count,
            },
            conditional_advantage = conditionalAdvantage,
            conditional_advantage_summary = conditionalSummary,
            recommendations = strategy.Recommendations.Select(x => new
            {
                source_company = x.SourceCompany,
                target_company = x.TargetCompany,
                recommended_rate = x.RecommendedRate,
                band_min = x.BandMin,
                band_max = x.BandMax,
                decision_category = x.DecisionCategory,
                uncertainty = x.UncertaintyLevel,
                anchor_company = x.AnchorCompany,
                anchor_gap = x.AnchorGap,
                anchor_gap_left = x.AnchorGap,
                left_space_width = x.LeftSpaceWidth,
                left_boundary_distance = x.LeftBoundaryDistance,
                left_space_source = x.LeftSpaceSource,
                fixed_score = x.FixedScore,
                gap_score = x.GapOpportunityScore,
                crowd_avoidance_score = x.CrowdAvoidanceScore,
                evidence_score = x.EvidenceScore,
                reason_codes = x.ReasonCodes,
                reason = x.ReasonSummary,
            }),
        };
        File.WriteAllText(
            Path.Combine(shadowDir, "shadow_strategy.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static (object Payload, object Summary) BuildConditionalAdvantagePayload(
        CrowdingAnalysisResult crowding,
        CollisionAnalysisResult collision,
        CompetitionStrategyAnalysisResult strategy)
    {
        var forbiddenZones = strategy.ZoneDecisions
            .Where(x => string.Equals(x.Category, "forbidden", StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .Select(MapZone)
            .ToList();
        var boundaryZones = strategy.ZoneDecisions
            .Where(x => string.Equals(x.Category, "boundary", StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .Select(MapZone)
            .ToList();
        var candidateBands = strategy.Recommendations
            .Take(10)
            .Select((x, index) => new
            {
                band = new[] { x.BandMin, x.BandMax },
                anchor = x.AnchorCompany,
                anchor_gap_left = x.AnchorGap,
                left_space_width = x.LeftSpaceWidth,
                left_boundary_distance = x.LeftBoundaryDistance,
                left_space_source = x.LeftSpaceSource,
                priority = index + 1,
                uncertainty = x.UncertaintyLevel,
                recommended_rate = x.RecommendedRate,
                priority_score = x.EvidenceScore,
                source_company = x.SourceCompany,
                target_company = x.TargetCompany,
                reason_codes = x.ReasonCodes,
                why = x.ReasonCodes,
            })
            .ToList();

        var crowdedLift = crowding.KeyFindings.CrowdedBandLift ?? 0.0;
        var compressionLevel = crowdedLift >= 1.35 || strategy.FixedBidderCount >= 5
            ? "high"
            : crowdedLift >= 1.10 || strategy.FixedBidderCount >= 3
                ? "medium"
                : "low";
        var topCandidate = candidateBands.FirstOrDefault();
        var primaryAnchor = topCandidate?.anchor
                            ?? collision.AnchorSummary.FirstOrDefault()?.CompanyName
                            ?? string.Empty;

        var summary = new
        {
            forbidden_zone_count = strategy.ZoneDecisions.Count(x => string.Equals(x.Category, "forbidden", StringComparison.OrdinalIgnoreCase)),
            boundary_zone_count = strategy.ZoneDecisions.Count(x => string.Equals(x.Category, "boundary", StringComparison.OrdinalIgnoreCase)),
            candidate_band_count = strategy.Recommendations.Count,
            primary_anchor = primaryAnchor,
            top_candidate = topCandidate,
        };

        var payload = new
        {
            system_view = new
            {
                compression_level = compressionLevel,
                predictability = compressionLevel == "high" ? "low" : compressionLevel == "medium" ? "medium" : "guarded",
                crowded_band_lift = crowding.KeyFindings.CrowdedBandLift,
                wide_left_space_lift = collision.KeyFindings.WideLeftSpaceLift,
                left_space_requirement = topCandidate?.left_space_width,
            },
            market_view = new
            {
                forbidden_zones = forbiddenZones,
                boundary_zones = boundaryZones,
            },
            relation_view = new
            {
                primary_anchor = new
                {
                    company = primaryAnchor,
                    anchor_gap_left = topCandidate?.anchor_gap_left,
                    left_space_width = topCandidate?.left_space_width,
                    left_boundary_distance = topCandidate?.left_boundary_distance,
                },
                collision_anchors = collision.AnchorSummary
                    .Take(5)
                    .Select(x => new
                    {
                        company = x.CompanyName,
                        sample_count = x.SampleCount,
                        average_left_space_gap = x.AverageLeftSpaceGap,
                        average_right_anchor_gap = x.AverageRightAnchorGap,
                        win_lift = x.WinLift,
                    })
                    .ToList(),
            },
            decision_view = new
            {
                candidate_bands = candidateBands,
            },
            summary,
        };
        return (payload, summary);
    }

    private static object MapZone(CompetitionZoneDecision zone)
    {
        return new
        {
            decision = zone.Category,
            band = new[] { zone.ZoneStart, zone.ZoneEnd },
            anchor = zone.AnchorCompany,
            anchor_gap_left = zone.AnchorGap,
            left_space_width = zone.LeftSpaceWidth,
            left_boundary_distance = zone.LeftBoundaryDistance,
            left_space_source = zone.LeftSpaceSource,
            density_score = zone.DensityScore,
            overheat_score = zone.OverheatScore,
            opportunity_score = zone.OpportunityScore,
            reason_codes = zone.ReasonCodes,
        };
    }

    private static void WriteStatus(string shadowDir, object payload)
    {
        Directory.CreateDirectory(shadowDir);
        File.WriteAllText(
            Path.Combine(shadowDir, "pipeline_status.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}
