using System.Globalization;
using System.IO;
using System.Text.Json;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed class NaraApiDashboardRepository
{
    private static readonly string[] DefaultKeywords =
    [
        "발굴조사",
        "정밀발굴조사",
        "시굴조사",
        "지표조사",
        "표본조사",
        "매장유산",
        "문화재조사",
        "문화재발굴조사",
    ];
    private static readonly string[] DefaultDomainContextTokens =
    [
        "발굴조사",
        "정밀발굴조사",
        "시굴조사",
        "지표조사",
        "표본조사",
        "매장유산",
        "매장문화재",
        "문화재조사",
        "문화재발굴조사",
        "문화재",
    ];

    private static readonly string[] DefaultExcludedServiceTokens =
    [
        "공사",
        "물품",
        "구매",
        "제조",
        "설치",
        "보수",
        "정비",
        "철거",
        "조성공사",
        "건설공사",
    ];

    private static readonly string[] DefaultRequiredServiceTokens =
    [
        "용역",
        "기술용역",
    ];

    private static readonly string[] DefaultExcludedNoticeKindTokens =
    [
        "취소공고",
    ];

    private static readonly string[] DefaultCollectorDomainContextTokens =
    [
        .. DefaultDomainContextTokens,
        "국가유산영향진단",
        "영향진단",
        "유해발굴",
    ];

    private static readonly string[] DefaultCollectorRequiredServiceTokens = [];

    private static readonly string[] DefaultCollectorExcludedServiceTokens = [];

    private static readonly string[] DefaultCollectorExcludedNoticeTokens =
    [
        "임차",
        "보고서",
        "발간",
        "제작",
        "인부",
        "측량",
        "촬영",
        "스캔",
        "모집단",
        "경제지표",
        "통계",
        "조사구",
        "단가계약",
        "중장비",
        "방안 연구",
    ];

    private static readonly string[] DefaultCollectorAllowedGroups =
    [
        "servc",
    ];

    private static readonly string[] GenericCollectorRegionTokens =
    [
        "본사소재지",
        "본사또는참여지사소재지",
        "본사 또는 참여지사 소재지",
        "소재지",
    ];

    private static readonly (string Token, string Label)[] BroadCollectorRegionMap =
    [
        ("전국", "전국"),
        ("서울", "서울"),
        ("부산", "부산"),
        ("대구", "대구"),
        ("인천", "인천"),
        ("광주", "광주"),
        ("대전", "대전"),
        ("울산", "울산"),
        ("세종", "세종"),
        ("경기", "경기"),
        ("강원", "강원"),
        ("충북", "충북"),
        ("충청북", "충북"),
        ("충남", "충남"),
        ("충청남", "충남"),
        ("전북", "전북"),
        ("전라북", "전북"),
        ("전남", "전남"),
        ("전라남", "전남"),
        ("경북", "경북"),
        ("경상북", "경북"),
        ("경남", "경남"),
        ("경상남", "경남"),
        ("제주", "제주"),
    ];

    private readonly string _masterPath;
    private readonly string _outputDirectory;
    private readonly string _collectionLogPath;
    private readonly string? _seedMasterPath;
    private readonly IAppPathResolver _pathResolver;
    private readonly NaraApiScriptRunner _scriptRunner;
    private readonly NaraApiParticipantParser _parser;
    private readonly NaraApiSnapshotBuilder _snapshotBuilder;

    public NaraApiDashboardRepository(
        string? masterPath = null,
        string? outputDirectory = null,
        string? seedMasterPath = null,
        IAppPathResolver? pathResolver = null)
        : this(
            masterPath,
            outputDirectory,
            seedMasterPath,
            pathResolver,
            scriptRunner: null,
            parser: null,
            snapshotBuilder: null)
    {
    }

    internal NaraApiDashboardRepository(
        string? masterPath,
        string? outputDirectory,
        string? seedMasterPath,
        IAppPathResolver? pathResolver,
        NaraApiScriptRunner? scriptRunner,
        NaraApiParticipantParser? parser,
        NaraApiSnapshotBuilder? snapshotBuilder)
    {
        _pathResolver = pathResolver ?? new DefaultAppPathResolver();
        _masterPath = _pathResolver.ResolveMasterPath(masterPath);
        _outputDirectory = _pathResolver.ResolveOutputDirectory(outputDirectory);
        _collectionLogPath = Path.Combine(_outputDirectory, "collection_history.jsonl");
        _seedMasterPath = string.IsNullOrWhiteSpace(seedMasterPath) ? null : seedMasterPath.Trim();
        _scriptRunner = scriptRunner ?? new NaraApiScriptRunner();
        _parser = parser ?? new NaraApiParticipantParser();
        _snapshotBuilder = snapshotBuilder ?? new NaraApiSnapshotBuilder();
    }

    public async Task<ApiCollectResult?> TryCollectAsync(
        DashboardFilter filter,
        IProgress<LoadingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var scriptPath = _pathResolver.ResolveSkillScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            progress?.Report(new LoadingProgress("error", 0, $"API 스킬 스크립트를 찾지 못했습니다: {scriptPath ?? "(null)"}"));
            return null;
        }

        EnsureSeedMasterIfNeeded(progress);
        Directory.CreateDirectory(Path.GetDirectoryName(_masterPath) ?? ".");
        Directory.CreateDirectory(_outputDirectory);

        var startMonth = filter.StartDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var endMonth = filter.EndDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var startDateTime = filter.StartDate.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var endDateTime = filter.EndDate.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var keywords = NaraApiParticipantParser.NormalizeKeywords(filter.Keyword);
        if (string.IsNullOrWhiteSpace(keywords))
        {
            keywords = LoadDefaultKeywords(_pathResolver.ResolveApiRuleProfilePath());
        }

        var envPath = _pathResolver.ResolveEnvPath();

        progress?.Report(
            new LoadingProgress(
                "collect",
                18,
                $"API 스킬 실행: {Path.GetFileName(scriptPath)} ({startDateTime}~{endDateTime})"));

        ApiSkillRunResult runResult;
        try
        {
            runResult = await _scriptRunner.RunAsync(
                scriptPath,
                startMonth,
                endMonth,
                startDateTime,
                endDateTime,
                keywords,
                _masterPath,
                _outputDirectory,
                _collectionLogPath,
                envPath,
                progress,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new LoadingProgress("stop", 0, "사용자 요청으로 API 스킬 실행을 중단했습니다."));
            throw;
        }

        if (!runResult.Success)
        {
            progress?.Report(new LoadingProgress("error", 0, $"API 스킬 실행 실패: {runResult.Summary}"));
            return null;
        }

        var collectionSourcePath = ResolveCollectionSourcePath(runResult);
        if (string.IsNullOrWhiteSpace(collectionSourcePath) || !File.Exists(collectionSourcePath))
        {
            progress?.Report(
                new LoadingProgress(
                    "error",
                    0,
                    $"API 스킬 실행 후 결과 파일을 찾지 못했습니다: {collectionSourcePath ?? "(null)"}"));
            return null;
        }

        progress?.Report(new LoadingProgress("parse", 64, $"증분 결과 엑셀 로딩: {collectionSourcePath}"));

        var participants = await Task.Run(
            () => _parser.LoadParticipantsFromMasterExcel(collectionSourcePath, filter, cancellationToken),
            cancellationToken);
        var collectorFeed = await Task.Run(
            () => LoadCollectorNoticeFeed(_outputDirectory, filter, _pathResolver.ResolveApiRuleProfilePath(), cancellationToken),
            cancellationToken);

        var snapshot = _snapshotBuilder.BuildSnapshot(participants, filter, runResult.SourceLabel);
        var rawRowCount = runResult.RawRowCount > 0 ? runResult.RawRowCount : participants.Count;

        progress?.Report(
            new LoadingProgress(
                "collect",
                74,
                $"API 스킬 수집 완료: 원천 {rawRowCount:N0}행 · 적용후 {participants.Count:N0}행"));

        return new ApiCollectResult(
            Snapshot: snapshot,
            Participants: participants,
            RawRowCount: rawRowCount,
            IncrementalBidNoticeRows: collectorFeed.BidRows,
            IncrementalAwardNoticeRows: collectorFeed.AwardRows);
    }

    internal static string TestHook_NormalizeKeywords(string raw)
    {
        return NaraApiParticipantParser.NormalizeKeywords(raw);
    }

    internal static string TestHook_LoadDefaultKeywords(string? apiRuleProfilePath = null)
    {
        return LoadDefaultKeywords(apiRuleProfilePath);
    }

    internal static string TestHook_ExtractNoticeNo(string noticeNumber)
    {
        return NaraApiParticipantParser.ExtractNoticeNo(noticeNumber);
    }

    internal static string TestHook_NormalizeRegionValue(string? value)
    {
        return NaraApiParticipantParser.NormalizeRegionValue(value);
    }

    internal static CollectorFeedResult TestHook_LoadCollectorNoticeFeed(
        string outputDirectory,
        DashboardFilter filter,
        string? apiRuleProfilePath = null)
    {
        return LoadCollectorNoticeFeed(outputDirectory, filter, apiRuleProfilePath, CancellationToken.None);
    }

    private void EnsureSeedMasterIfNeeded(IProgress<LoadingProgress>? progress)
    {
        if (File.Exists(_masterPath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_seedMasterPath) || !File.Exists(_seedMasterPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_masterPath) ?? ".");
            File.Copy(_seedMasterPath, _masterPath, overwrite: false);
            progress?.Report(new LoadingProgress("seed", 14, $"API 마스터 초기화: {_seedMasterPath} -> {_masterPath}"));
        }
        catch (Exception ex)
        {
            progress?.Report(new LoadingProgress("seed", 14, $"API 마스터 초기화 실패(무시): {ex.Message}"));
        }
    }

    private string ResolveCollectionSourcePath(ApiSkillRunResult runResult)
    {
        var candidates = new[]
        {
            runResult.RawMasterPath,
            runResult.FinalMasterPath,
            _masterPath,
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    }

    private static CollectorFeedResult LoadCollectorNoticeFeed(
        string outputDirectory,
        DashboardFilter filter,
        string? apiRuleProfilePath,
        CancellationToken cancellationToken)
    {
        var profile = LoadCollectorRuleProfile(apiRuleProfilePath);
        var bidRows = new Dictionary<string, CollectorNoticeRowModel>(StringComparer.OrdinalIgnoreCase);
        var awardRows = new Dictionary<string, CollectorNoticeRowModel>(StringComparer.OrdinalIgnoreCase);
        var noticeRegionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var month in EnumerateMonths(filter.StartDate, filter.EndDate))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var monthDirectory = Path.Combine(outputDirectory, "raw", month);
            var bidPath = Path.Combine(monthDirectory, "bid_notice_raw.jsonl");
            if (File.Exists(bidPath))
            {
                foreach (var row in LoadBidNoticeRows(bidPath, filter, profile, cancellationToken))
                {
                    bidRows[row.NoticeNumber] = row;
                    if (!string.IsNullOrWhiteSpace(row.RegionText)
                        && !string.Equals(row.RegionText, "미상", StringComparison.OrdinalIgnoreCase))
                    {
                        noticeRegionMap[row.NoticeNumber] = row.RegionText;
                    }
                }
            }

            var awardPath = Path.Combine(monthDirectory, "openg_result_raw.jsonl");
            if (File.Exists(awardPath))
            {
                foreach (var row in LoadAwardNoticeRows(awardPath, filter, profile, noticeRegionMap, cancellationToken))
                {
                    awardRows[row.NoticeNumber] = row;
                }
            }
        }

        var orderedBidRows = bidRows.Values
            .OrderByDescending(x => x.NoticeDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orderedAwardRows = awardRows.Values
            .OrderByDescending(x => x.NoticeDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CollectorFeedResult(orderedBidRows, orderedAwardRows);
    }

    private static IEnumerable<CollectorNoticeRowModel> LoadBidNoticeRows(
        string path,
        DashboardFilter filter,
        CollectorRuleProfile profile,
        CancellationToken cancellationToken)
    {
        foreach (var line in File.ReadLines(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var noticeNumber = GetString(root, "bidNtceNo");
            if (string.IsNullOrWhiteSpace(noticeNumber))
            {
                continue;
            }

            var noticeName = GetString(root, "bidNtceNm");
            var sourceGroup = GetString(root, "_src_group");
            if (!MatchesCollectorScope(noticeName, sourceGroup, profile))
            {
                continue;
            }

            var agency = FirstNonEmpty(GetString(root, "dminsttNm"), GetString(root, "ntceInsttNm"));
            var bidDeadline = GetDateTime(root, "bidClseDt", "opengDt", "bidNtceDt");
            var scheduledOpenDate = GetDateTime(root, "opengDt", "bidClseDt", "bidNtceDt");
            var changedAt = GetDateTime(root, "rgstDt", "chgDt", "bidNtceDt") ?? scheduledOpenDate;
            var noticeDate = bidDeadline ?? scheduledOpenDate ?? changedAt;
            if (!noticeDate.HasValue || !IsWithinWindow(noticeDate.Value, filter.StartDate, filter.EndDate))
            {
                continue;
            }

            var regionText = InferCollectorRegion(
                GetString(root, "rgnLmtBidLocplcJdgmBssNm"),
                GetString(root, "jntcontrctDutyRgnNm1"),
                GetString(root, "jntcontrctDutyRgnNm2"),
                GetString(root, "jntcontrctDutyRgnNm3"),
                agency);
            if (!MatchesCollectorRegion(regionText, filter.Region))
            {
                continue;
            }

            yield return new CollectorNoticeRowModel(
                NoticeDate: noticeDate.Value,
                NoticeNumber: noticeNumber.Trim(),
                NoticeName: noticeName,
                OrderingAgency: agency,
                RegionText: string.IsNullOrWhiteSpace(regionText) ? "미상" : regionText,
                ParticipantCountText: "-");
        }
    }

    private static IEnumerable<CollectorNoticeRowModel> LoadAwardNoticeRows(
        string path,
        DashboardFilter filter,
        CollectorRuleProfile profile,
        IReadOnlyDictionary<string, string> noticeRegionMap,
        CancellationToken cancellationToken)
    {
        foreach (var line in File.ReadLines(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var noticeNumber = GetString(root, "bidNtceNo");
            if (string.IsNullOrWhiteSpace(noticeNumber))
            {
                continue;
            }

            var noticeName = GetString(root, "bidNtceNm");
            var sourceGroup = GetString(root, "_src_group");
            if (!MatchesCollectorScope(noticeName, sourceGroup, profile))
            {
                continue;
            }

            var openDate = GetDateTime(root, "opengDt", "inptDt");
            if (!openDate.HasValue || !IsWithinWindow(openDate.Value, filter.StartDate, filter.EndDate))
            {
                continue;
            }

            noticeRegionMap.TryGetValue(noticeNumber.Trim(), out var regionText);
            var agency = FirstNonEmpty(GetString(root, "dminsttNm"), GetString(root, "ntceInsttNm"));
            if (string.IsNullOrWhiteSpace(regionText) || string.Equals(regionText, "미상", StringComparison.OrdinalIgnoreCase))
            {
                regionText = InferCollectorRegion(agency);
            }
            if (!MatchesCollectorRegion(regionText, filter.Region))
            {
                continue;
            }

            var participantCount = GetInt(root, "prtcptCnum");
            yield return new CollectorNoticeRowModel(
                NoticeDate: openDate.Value,
                NoticeNumber: noticeNumber.Trim(),
                NoticeName: noticeName,
                OrderingAgency: agency,
                RegionText: string.IsNullOrWhiteSpace(regionText) ? "미상" : regionText,
                ParticipantCountText: participantCount > 0 ? $"{participantCount:N0}개사" : "-");
        }
    }

    private static CollectorRuleProfile LoadCollectorRuleProfile(string? apiRuleProfilePath)
    {
        if (!string.IsNullOrWhiteSpace(apiRuleProfilePath) && File.Exists(apiRuleProfilePath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(apiRuleProfilePath));
                var root = document.RootElement;
                return new CollectorRuleProfile(
                    DomainContextTokens: ReadStringArray(root, "collector_domain_context_tokens", DefaultCollectorDomainContextTokens),
                    RequiredServiceTokens: ReadStringArray(root, "collector_required_service_tokens", DefaultCollectorRequiredServiceTokens, allowEmpty: true),
                    ExcludedServiceTokens: ReadStringArray(root, "collector_excluded_service_tokens", DefaultCollectorExcludedServiceTokens, allowEmpty: true),
                    ExcludedNoticeKindTokens: ReadStringArray(root, "excluded_notice_kind_tokens", DefaultExcludedNoticeKindTokens),
                    CollectorExcludedNoticeTokens: ReadStringArray(root, "collector_excluded_notice_tokens", DefaultCollectorExcludedNoticeTokens),
                    CollectorAllowedGroups: ReadStringArray(root, "collector_allowed_groups", DefaultCollectorAllowedGroups));
            }
            catch
            {
                // rule_profile 로드 실패 시 기본값 사용
            }
        }

        return new CollectorRuleProfile(
            DefaultCollectorDomainContextTokens,
            DefaultCollectorRequiredServiceTokens,
            DefaultCollectorExcludedServiceTokens,
            DefaultExcludedNoticeKindTokens,
            DefaultCollectorExcludedNoticeTokens,
            DefaultCollectorAllowedGroups);
    }

    private static string LoadDefaultKeywords(string? apiRuleProfilePath)
    {
        if (!string.IsNullOrWhiteSpace(apiRuleProfilePath) && File.Exists(apiRuleProfilePath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(apiRuleProfilePath));
                var values = ReadStringArray(document.RootElement, "primary_keywords", DefaultKeywords);
                var normalized = NaraApiParticipantParser.NormalizeKeywords(string.Join(",", values));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }
            catch
            {
                // rule_profile 키워드 로드 실패 시 기본값 사용
            }
        }

        return string.Join(",", DefaultKeywords);
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> fallback,
        bool allowEmpty = false)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        var values = element
            .EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()?.Trim() : null)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count == 0)
        {
            return allowEmpty ? Array.Empty<string>() : fallback;
        }

        return values;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool MatchesCollectorScope(string noticeName, string sourceGroup, CollectorRuleProfile profile)
    {
        if (string.IsNullOrWhiteSpace(noticeName))
        {
            return false;
        }

        var text = noticeName.Trim();
        if (profile.CollectorAllowedGroups.Count > 0
            && !profile.CollectorAllowedGroups.Any(token => string.Equals(token, sourceGroup, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var hasDomainToken = profile.DomainContextTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (!hasDomainToken)
        {
            return false;
        }

        var hasRequiredServiceToken = profile.RequiredServiceTokens.Count == 0
            || profile.RequiredServiceTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (!hasRequiredServiceToken)
        {
            return false;
        }

        if (profile.ExcludedNoticeKindTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (profile.ExcludedServiceTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !profile.CollectorExcludedNoticeTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCollectorRegion(string? inferredRegion, string? selectedRegion)
    {
        if (string.IsNullOrWhiteSpace(selectedRegion) || string.Equals(selectedRegion, "전국", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(inferredRegion))
        {
            return false;
        }

        return inferredRegion.Contains(selectedRegion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string InferCollectorRegion(params string[] values)
    {
        var combined = string.Join(" ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(combined))
        {
            return string.Empty;
        }

        var normalized = NaraApiParticipantParser.NormalizeRegionValue(combined);
        return NormalizeCollectorRegionText(normalized);
    }

    private static string NormalizeCollectorRegionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (GenericCollectorRegionTokens.Any(token => string.Equals(normalized, token, StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        foreach (var (token, label) in BroadCollectorRegionMap)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
        }

        return string.Empty;
    }

    private static bool IsWithinWindow(DateTime value, DateTime start, DateTime end)
    {
        return value >= start && value <= end;
    }

    private static IEnumerable<string> EnumerateMonths(DateTime start, DateTime end)
    {
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        while (cursor <= last)
        {
            yield return cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            cursor = cursor.AddMonths(1);
        }
    }

    private static string GetString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString()?.Trim() ?? string.Empty;
            }

            if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return element.ToString().Trim();
            }
        }

        return string.Empty;
    }

    private static int GetInt(JsonElement root, params string[] propertyNames)
    {
        var text = GetString(root, propertyNames);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static DateTime? GetDateTime(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var text = GetString(root, propertyName);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                || DateTime.TryParse(text, out parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}

public sealed record ApiCollectResult(
    DashboardSnapshot Snapshot,
    IReadOnlyList<ParticipantRecord> Participants,
    int RawRowCount,
    IReadOnlyList<CollectorNoticeRowModel> IncrementalBidNoticeRows,
    IReadOnlyList<CollectorNoticeRowModel> IncrementalAwardNoticeRows);

internal sealed record CollectorFeedResult(
    IReadOnlyList<CollectorNoticeRowModel> BidRows,
    IReadOnlyList<CollectorNoticeRowModel> AwardRows);

internal sealed record CollectorRuleProfile(
    IReadOnlyList<string> DomainContextTokens,
    IReadOnlyList<string> RequiredServiceTokens,
    IReadOnlyList<string> ExcludedServiceTokens,
    IReadOnlyList<string> ExcludedNoticeKindTokens,
    IReadOnlyList<string> CollectorExcludedNoticeTokens,
    IReadOnlyList<string> CollectorAllowedGroups);
