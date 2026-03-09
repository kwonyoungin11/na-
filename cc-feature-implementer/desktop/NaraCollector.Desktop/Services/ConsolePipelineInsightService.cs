using System.Globalization;
using System.IO;
using System.Text.Json;

namespace NaraCollector.Desktop.Services;

public sealed record ConsolePipelineInsight(
    bool IsAvailable,
    string Region,
    string Status,
    string StatusText,
    string SummaryText,
    string CrowdingText,
    string CollisionText,
    string ShadowText,
    string RecommendationText,
    string RecommendationReasonText,
    double? RecommendationBandMin,
    double? RecommendationBandMax,
    string AnalysisPeriod,
    double? AnalysisMinRate,
    double? AnalysisMaxRate,
    int? ForbiddenZoneCount,
    int? BoundaryZoneCount,
    int? CandidateBandCount,
    string PrimaryAnchorCompany,
    string RecommendationBandText,
    string RecommendationLeftSpaceText,
    string RecommendationUncertainty)
{
    public static ConsolePipelineInsight Empty(string region, string status, string message) => new(
        IsAvailable: false,
        Region: region,
        Status: status,
        StatusText: message,
        SummaryText: message,
        CrowdingText: "군집 분석 결과 없음",
        CollisionText: "충돌 분석 결과 없음",
        ShadowText: "전략 분석 결과 없음",
        RecommendationText: "추천 밴드 없음",
        RecommendationReasonText: "콘솔 파이프라인을 먼저 실행해야 합니다.",
        RecommendationBandMin: null,
        RecommendationBandMax: null,
        AnalysisPeriod: string.Empty,
        AnalysisMinRate: null,
        AnalysisMaxRate: null,
        ForbiddenZoneCount: null,
        BoundaryZoneCount: null,
        CandidateBandCount: null,
        PrimaryAnchorCompany: string.Empty,
        RecommendationBandText: string.Empty,
        RecommendationLeftSpaceText: string.Empty,
        RecommendationUncertainty: string.Empty);
}

public sealed class ConsolePipelineInsightService
{
    private readonly IAppPathResolver _pathResolver;

    public ConsolePipelineInsightService(IAppPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new DefaultAppPathResolver();
    }

    public ConsolePipelineInsight Load(string? region)
    {
        var normalizedRegion = NormalizeRegion(region);
        var analysisRoot = _pathResolver.ResolveConsoleAnalysisRootPath();
        var shadowDirectory = Path.Combine(analysisRoot, $"shadow_{normalizedRegion}");
        var statusPath = Path.Combine(shadowDirectory, "pipeline_status.json");
        if (!File.Exists(statusPath))
        {
            return ConsolePipelineInsight.Empty(normalizedRegion, "missing", $"{normalizedRegion} 콘솔 분석 결과가 아직 없습니다.");
        }

        using var statusDocument = JsonDocument.Parse(File.ReadAllText(statusPath));
        var root = statusDocument.RootElement;
        var status = ReadString(root, "status") ?? "unknown";
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            var rows = ReadInt(root, "rows") ?? 0;
            var notices = ReadInt(root, "notices") ?? 0;
            return ConsolePipelineInsight.Empty(
                normalizedRegion,
                status,
                $"{normalizedRegion} 콘솔 분석 상태: {status} (입력 {rows:N0}행 / 공고 {notices:N0}건)");
        }

        var stalenessReason = DetectStaleness(root, statusPath);
        if (!string.IsNullOrWhiteSpace(stalenessReason))
        {
            return CreateStaleInsight(normalizedRegion, stalenessReason);
        }

        var rowsCompleted = ReadInt(root, "rows") ?? 0;
        var noticesCompleted = ReadInt(root, "notices") ?? 0;
        var rateWindowRows = ReadNestedInt(root, "rate_window", "rows") ?? 0;
        var rateWindowMin = ReadNestedDouble(root, "rate_window", "min");
        var rateWindowMax = ReadNestedDouble(root, "rate_window", "max");
        var crowding = ReadNestedElement(root, "crowding_key_findings");
        var collision = ReadNestedElement(root, "collision_key_findings");
        var shadowSummary = ReadNestedElement(root, "shadow_summary");
        var conditionalAdvantage = ReadNestedElement(root, "conditional_advantage");
        var conditionalSummary = ReadNestedElement(root, "conditional_advantage_summary");

        var shadowStrategyPath = ResolveShadowStrategyPath(root, shadowDirectory);
        ShadowRecommendation shadowRecommendation = ShadowRecommendation.Empty;
        JsonElement? shadowConditionalAdvantage = null;
        JsonElement? shadowConditionalSummary = null;
        if (!string.IsNullOrWhiteSpace(shadowStrategyPath) && File.Exists(shadowStrategyPath))
        {
            using var shadowDocument = JsonDocument.Parse(File.ReadAllText(shadowStrategyPath));
            shadowRecommendation = ReadTopRecommendation(shadowDocument.RootElement);
            shadowConditionalAdvantage = ReadNestedElement(shadowDocument.RootElement, "conditional_advantage");
            shadowConditionalSummary = ReadNestedElement(shadowDocument.RootElement, "conditional_advantage_summary");
        }
        conditionalAdvantage ??= shadowConditionalAdvantage;
        conditionalSummary ??= ReadNestedElement(conditionalAdvantage, "summary") ?? shadowConditionalSummary ?? conditionalAdvantage;
        var conditionalDecision = ReadNestedElement(conditionalAdvantage, "decision_view");

        var crowdedLift = ReadDouble(crowding, "crowded_band_lift");
        var sparseLift = ReadDouble(crowding, "sparse_band_lift");
        var wideLift = ReadDouble(collision, "wide_left_space_lift");
        var advantageLift = ReadDouble(collision, "high_left_advantage_lift");
        var bestMatrix = ReadNestedElement(collision, "best_matrix_cell");
        var bestMatrixLabel = bestMatrix is { ValueKind: JsonValueKind.Object }
            ? $"{ReadString(bestMatrix.Value, "우측앵커거리구간") ?? "-"} / {ReadString(bestMatrix.Value, "좌측여유구간") ?? "-"}"
            : "-";

        var fixedBidders = ReadInt(shadowSummary, "fixed_bidders") ?? 0;
        var recommendations = ReadInt(shadowSummary, "recommendations") ?? 0;
        var period = ReadString(shadowSummary, "period") ?? "-";
        var forbiddenCount = ReadInt(conditionalSummary, "forbidden_zone_count") ?? ReadInt(conditionalDecision, "forbidden_count");
        var boundaryCount = ReadInt(conditionalSummary, "boundary_zone_count") ?? ReadInt(conditionalDecision, "boundary_count");
        var candidateCount = ReadInt(conditionalSummary, "candidate_band_count") ?? ReadInt(conditionalDecision, "candidate_count");
        var topCandidate = ReadNestedElement(conditionalSummary, "top_candidate") ?? ReadFirstArrayElement(conditionalDecision, "candidate_bands");

        var summaryText =
            $"{normalizedRegion} · 입력 {rowsCompleted:N0}행 / 공고 {noticesCompleted:N0}건 · 타겟존 {rateWindowRows:N0}행";
        var crowdingText =
            $"붐빔 리프트 {FormatNullable(crowdedLift)}배 · 한산 리프트 {FormatNullable(sparseLift)}배";
        var collisionText =
            $"좌측여유 넓음 {FormatNullable(wideLift)}배 · 좌측우위 높음 {FormatNullable(advantageLift)}배 · 최적셀 {bestMatrixLabel}";
        var shadowText =
            forbiddenCount.HasValue && boundaryCount.HasValue && candidateCount.HasValue
                ? $"{period} 기준 고정업체 {fixedBidders:N0}개 · 금지 {forbiddenCount.Value:N0} / 경계 {boundaryCount.Value:N0} / 후보 {candidateCount.Value:N0}"
                : $"{period} 기준 고정업체 {fixedBidders:N0}개 · 추천 {recommendations:N0}개";
        var candidateSummary = ReadCandidateSummary(topCandidate);
        var recommendationText = candidateSummary.HasValue
            ? $"{candidateSummary.AnchorCompany} 왼쪽 {candidateSummary.BandText} · 좌측여유 {candidateSummary.LeftSpaceText}"
            : shadowRecommendation.HasValue
                ? $"{shadowRecommendation.TargetCompany} 기준 {shadowRecommendation.RecommendedRateText} · 밴드 {shadowRecommendation.BandText}"
                : "추천 밴드가 아직 생성되지 않았습니다.";
        var recommendationReason = candidateSummary.HasValue
            ? $"좌측선점 후보 · 불확실성 {FormatUncertainty(candidateSummary.Uncertainty)}"
            : shadowRecommendation.HasValue
                ? shadowRecommendation.Reason
            : "shadow_strategy.json 상단 추천이 없습니다.";

        return new ConsolePipelineInsight(
            IsAvailable: true,
            Region: normalizedRegion,
            Status: status,
            StatusText: $"{normalizedRegion} 콘솔 분석 완료",
            SummaryText: summaryText,
            CrowdingText: crowdingText,
            CollisionText: collisionText,
            ShadowText: shadowText,
            RecommendationText: recommendationText,
            RecommendationReasonText: recommendationReason,
            RecommendationBandMin: candidateSummary.HasValue ? candidateSummary.BandMin : shadowRecommendation.BandMin,
            RecommendationBandMax: candidateSummary.HasValue ? candidateSummary.BandMax : shadowRecommendation.BandMax,
            AnalysisPeriod: period,
            AnalysisMinRate: rateWindowMin,
            AnalysisMaxRate: rateWindowMax,
            ForbiddenZoneCount: forbiddenCount,
            BoundaryZoneCount: boundaryCount,
            CandidateBandCount: candidateCount,
            PrimaryAnchorCompany: candidateSummary.HasValue ? candidateSummary.AnchorCompany : shadowRecommendation.TargetCompany,
            RecommendationBandText: candidateSummary.HasValue ? candidateSummary.BandText : shadowRecommendation.BandText,
            RecommendationLeftSpaceText: candidateSummary.HasValue ? candidateSummary.LeftSpaceText : string.Empty,
            RecommendationUncertainty: candidateSummary.HasValue ? FormatUncertainty(candidateSummary.Uncertainty) : string.Empty);
    }

    private ConsolePipelineInsight CreateStaleInsight(string region, string reason)
    {
        var message = $"{region} 콘솔 분석 결과가 오래되었습니다. 다시 분석하세요.";
        return new ConsolePipelineInsight(
            IsAvailable: false,
            Region: region,
            Status: "stale",
            StatusText: message,
            SummaryText: reason,
            CrowdingText: "군집 분석 결과가 오래되어 숨김 처리되었습니다.",
            CollisionText: "충돌 분석 결과가 오래되어 숨김 처리되었습니다.",
            ShadowText: "전략 분석 결과가 오래되어 숨김 처리되었습니다.",
            RecommendationText: "추천 밴드 없음",
            RecommendationReasonText: reason,
            RecommendationBandMin: null,
            RecommendationBandMax: null,
            AnalysisPeriod: string.Empty,
            AnalysisMinRate: null,
            AnalysisMaxRate: null,
            ForbiddenZoneCount: null,
            BoundaryZoneCount: null,
            CandidateBandCount: null,
            PrimaryAnchorCompany: string.Empty,
            RecommendationBandText: string.Empty,
            RecommendationLeftSpaceText: string.Empty,
            RecommendationUncertainty: string.Empty);
    }

    private static string NormalizeRegion(string? region)
    {
        var token = (region ?? string.Empty).Trim();
        return token switch
        {
            "전체" => "전국",
            _ when token.Contains("전국", StringComparison.Ordinal) => "전국",
            _ when token.Contains("대구", StringComparison.Ordinal) => "대구",
            _ when token.Contains("경북", StringComparison.Ordinal) || token.Contains("경상북", StringComparison.Ordinal) => "경북",
            _ => string.IsNullOrWhiteSpace(token) ? "전국" : token,
        };
    }

    private static string? ResolveShadowStrategyPath(JsonElement root, string shadowDirectory)
    {
        var configured = ReadNestedString(root, "outputs", "shadow");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(configured, "shadow_strategy.json");
        }

        return Path.Combine(shadowDirectory, "shadow_strategy.json");
    }

    private string? DetectStaleness(JsonElement root, string statusPath)
    {
        var signature = ReadString(root, "snapshot_signature");
        if (!string.Equals(signature, CSharpAnalysisPipelineRunner.CurrentSnapshotSignature, StringComparison.Ordinal))
        {
            return $"저장된 분석 버전이 현재 추천 로직과 다릅니다. 다시 분석이 필요합니다. (저장본: {signature ?? "없음"} / 현재: {CSharpAnalysisPipelineRunner.CurrentSnapshotSignature})";
        }

        var snapshotInstant = ReadDateTimeOffset(root, "generated_at_utc")
                              ?? ReadFileLastWriteTimeUtc(statusPath);
        if (!snapshotInstant.HasValue)
        {
            return "저장된 분석 시각을 확인할 수 없어 다시 분석이 필요합니다.";
        }

        var masterPath = _pathResolver.ResolveMasterPath(null);
        var masterStaleness = DetectDependencyStaleness(
            "원본 엑셀",
            masterPath,
            snapshotInstant.Value,
            ReadDateTimeOffset(root, "source_file_last_write_time_utc"));
        if (!string.IsNullOrWhiteSpace(masterStaleness))
        {
            return masterStaleness;
        }

        var analysisProfilePath = _pathResolver.ResolveAnalysisProfilePath();
        var profileStaleness = DetectDependencyStaleness(
            "분석 프로필",
            analysisProfilePath,
            snapshotInstant.Value,
            ReadDateTimeOffset(root, "analysis_profile_last_write_time_utc"));
        if (!string.IsNullOrWhiteSpace(profileStaleness))
        {
            return profileStaleness;
        }

        return null;
    }

    private static string? DetectDependencyStaleness(
        string label,
        string? currentPath,
        DateTimeOffset snapshotInstant,
        DateTimeOffset? recordedDependencyInstant)
    {
        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
        {
            return null;
        }

        var currentInstant = File.GetLastWriteTimeUtc(currentPath);
        if (currentInstant <= snapshotInstant)
        {
            return null;
        }

        if (recordedDependencyInstant.HasValue && currentInstant <= recordedDependencyInstant.Value)
        {
            return null;
        }

        return $"{label}이 저장된 분석 결과보다 더 최신입니다. 다시 분석이 필요합니다. ({currentInstant:yyyy-MM-dd HH:mm:ss} UTC)";
    }

    private static ShadowRecommendation ReadTopRecommendation(JsonElement root)
    {
        if (!root.TryGetProperty("recommendations", out var recommendations)
            || recommendations.ValueKind != JsonValueKind.Array
            || recommendations.GetArrayLength() == 0)
        {
            return ShadowRecommendation.Empty;
        }

        var first = recommendations[0];
        var company = ReadString(first, "target_company") ?? "-";
        var recommendedRate = ReadDouble(first, "recommended_rate");
        var bandMin = ReadDouble(first, "band_min");
        var bandMax = ReadDouble(first, "band_max");
        var reason = ReadString(first, "reason") ?? "사유 없음";

        return new ShadowRecommendation(
            HasValue: true,
            TargetCompany: company,
            RecommendedRateText: recommendedRate.HasValue ? recommendedRate.Value.ToString("F3", CultureInfo.InvariantCulture) + "%" : "-",
            BandText: bandMin.HasValue && bandMax.HasValue
                ? $"{bandMin.Value.ToString("F3", CultureInfo.InvariantCulture)}%~{bandMax.Value.ToString("F3", CultureInfo.InvariantCulture)}%"
                : "-",
            Reason: reason,
            BandMin: bandMin,
            BandMax: bandMax);
    }

    private static CandidateSummary ReadCandidateSummary(JsonElement? root)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object)
        {
            return CandidateSummary.Empty;
        }

        var band = ReadBand(root.Value, "band");
        var anchor = ReadString(root, "anchor") ?? string.Empty;
        var leftSpace = ReadDouble(root, "left_space_width");
        var uncertainty = ReadString(root, "uncertainty") ?? string.Empty;
        if (band is null || string.IsNullOrWhiteSpace(anchor))
        {
            return CandidateSummary.Empty;
        }

        return new CandidateSummary(
            HasValue: true,
            AnchorCompany: anchor,
            BandText: $"{band.Value.Min.ToString("F3", CultureInfo.InvariantCulture)}%~{band.Value.Max.ToString("F3", CultureInfo.InvariantCulture)}%",
            LeftSpaceText: leftSpace.HasValue ? leftSpace.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-",
            Uncertainty: uncertainty,
            BandMin: band.Value.Min,
            BandMax: band.Value.Max);
    }

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement? element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ReadFileLastWriteTimeUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return File.GetLastWriteTimeUtc(path);
    }

    private static string FormatUncertainty(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "low" => "낮음",
            "medium" => "중간",
            "high" => "높음",
            _ => "미상",
        };
    }

    private static (double Min, double Max)? ReadBand(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var band) || band.ValueKind != JsonValueKind.Array || band.GetArrayLength() < 2)
        {
            return null;
        }

        var values = band.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var number) ? (double?)number : null)
            .ToList();
        return values.Count >= 2 && values[0].HasValue && values[1].HasValue
            ? (values[0]!.Value, values[1]!.Value)
            : null;
    }

    private static JsonElement? ReadNestedElement(JsonElement? root, string propertyName)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.Value.TryGetProperty(propertyName, out var value) ? value : null;
    }

    private static JsonElement? ReadFirstArrayElement(JsonElement? root, string propertyName)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object || !root.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 ? value[0] : null;
    }

    private static string? ReadString(JsonElement? root, string propertyName)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object || !root.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement? root, string propertyName)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object || !root.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => (int)Math.Round(doubleValue),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static double? ReadDouble(JsonElement? root, string propertyName)
    {
        if (!root.HasValue || root.Value.ValueKind != JsonValueKind.Object || !root.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? ReadNestedString(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out var parentValue) || parentValue.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(parentValue, child);
    }

    private static int? ReadNestedInt(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out var parentValue) || parentValue.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadInt(parentValue, child);
    }

    private static double? ReadNestedDouble(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out var parentValue) || parentValue.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadDouble(parentValue, child);
    }

    private readonly record struct ShadowRecommendation(
        bool HasValue,
        string TargetCompany,
        string RecommendedRateText,
        string BandText,
        string Reason,
        double? BandMin,
        double? BandMax)
    {
        public static ShadowRecommendation Empty => new(false, string.Empty, string.Empty, string.Empty, string.Empty, null, null);
    }

    private readonly record struct CandidateSummary(
        bool HasValue,
        string AnchorCompany,
        string BandText,
        string LeftSpaceText,
        string Uncertainty,
        double BandMin,
        double BandMax)
    {
        public static CandidateSummary Empty => new(false, string.Empty, string.Empty, string.Empty, string.Empty, 0.0, 0.0);
    }
}
