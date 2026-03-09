using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed class SqliteDashboardRepository : IDashboardRepository
{
    private const string CollectStateFileName = "collect_state.json";
    private static readonly bool AwardOnlyMode = true;
    private const int SnapshotCacheLimit = 24;

    private readonly string _dbPath;
    private readonly string _masterExcelPath;
    private readonly string _collectStatePath;
    private readonly NaraApiDashboardRepository _apiRepository;
    private readonly DashboardOutputService _outputService;
    private readonly IAppPathResolver _pathResolver;
    private readonly SemaphoreSlim _baselineSync = new(1, 1);
    private readonly object _snapshotCacheSync = new();
    private readonly Dictionary<string, DashboardSnapshot> _snapshotCache = new(StringComparer.Ordinal);
    private bool _baselineEnsured;
    private DateTime? _masterLatestOpenDateCache;
    private List<ParticipantRecord>? _participantsCache;
    private HashSet<string>? _awardedNoticeKeysCache;
    private List<CollectorNoticeRowModel>? _collectorBidCache;
    private List<CollectorNoticeRowModel>? _collectorAwardCache;

    public SqliteDashboardRepository(string? dbPath = null, IAppPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new DefaultAppPathResolver();
        _outputService = new DashboardOutputService();
        _dbPath = string.IsNullOrWhiteSpace(dbPath) ? _outputService.DatabasePath : dbPath;
        _masterExcelPath = _pathResolver.ResolveMasterPath(null);
        _collectStatePath = Path.Combine(_outputService.AppRootDirectory, CollectStateFileName);
        var apiOutputDirectory = Path.Combine(_outputService.AppRootDirectory, "api-skill-output");
        _apiRepository = new NaraApiDashboardRepository(
            masterPath: _masterExcelPath,
            outputDirectory: apiOutputDirectory,
            seedMasterPath: _masterExcelPath,
            pathResolver: _pathResolver);
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(
        DashboardFilter filter,
        IProgress<LoadingProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false,
        DateTime? incrementalStartOverride = null)
    {
        var effectiveFilter = NormalizeFilter(filter);
        await EnsureDatabaseSeededAsync(progress, cancellationToken);

        if (!forceRefresh)
        {
            progress?.Report(new LoadingProgress("cache", 2, "SQLite snapshot 캐시 재사용을 확인합니다."));
            var cachedSnapshot = await TryLoadSnapshotFromSqliteAsync(effectiveFilter, cancellationToken);
            if (cachedSnapshot is not null)
            {
                progress?.Report(new LoadingProgress("cache", 100, "SQLite snapshot 캐시를 재사용했습니다."));
                return new DashboardSnapshot(
                    cachedSnapshot.Kpis,
                    cachedSnapshot.NoticeRows,
                    $"{cachedSnapshot.DataSource} · 캐시 재사용",
                    cachedSnapshot.SavedFilePath,
                    cachedSnapshot.PositionMapPath,
                    cachedSnapshot.RecommendationPath,
                    cachedSnapshot.CompanyOptions,
                    cachedSnapshot.Participants,
                    cachedSnapshot.IncrementalBidNoticeRows,
                    cachedSnapshot.IncrementalAwardNoticeRows);
            }

            progress?.Report(new LoadingProgress("cache", 8, "재사용 가능한 캐시가 없어 실시간 API 수집으로 전환합니다."));
        }

        var initMessage = forceRefresh
            ? "강제 새로고침(forceRefresh=true): 실시간 API 수집을 시작합니다."
            : "실시간 API 수집을 시작합니다.";
        progress?.Report(new LoadingProgress("init", 12, initMessage));
        var collectFilter = forceRefresh
            ? await BuildIncrementalCollectFilterAsync(effectiveFilter, progress, incrementalStartOverride, cancellationToken)
            : effectiveFilter;
        var liveSnapshot = await TryLoadLiveSnapshotAsync(collectFilter, effectiveFilter, progress, cancellationToken);
        if (liveSnapshot is not null)
        {
            return liveSnapshot;
        }

        if (forceRefresh)
        {
            progress?.Report(new LoadingProgress("fallback", 92, "실시간 API 수집 실패 → 기존 캐시/내장 데이터를 재사용합니다."));
            var fallbackSnapshot = await TryLoadSnapshotFromSqliteAsync(effectiveFilter, cancellationToken);
            if (fallbackSnapshot is not null)
            {
                return new DashboardSnapshot(
                    fallbackSnapshot.Kpis,
                    fallbackSnapshot.NoticeRows,
                    $"{fallbackSnapshot.DataSource} · API 실패로 캐시 재사용",
                    fallbackSnapshot.SavedFilePath,
                    fallbackSnapshot.PositionMapPath,
                    fallbackSnapshot.RecommendationPath,
                    fallbackSnapshot.CompanyOptions,
                    fallbackSnapshot.Participants,
                    fallbackSnapshot.IncrementalBidNoticeRows,
                    fallbackSnapshot.IncrementalAwardNoticeRows);
            }
        }

        var errorMessage = forceRefresh
            ? "강제 새로고침(forceRefresh=true) 실시간 API 수집에 실패했습니다. API 키(.env), 네트워크, 조회 기간을 확인하세요."
            : "캐시 재사용 및 API 실시간 수집에 모두 실패했습니다. API 키(.env), 네트워크, 조회 기간을 확인하세요.";
        throw new InvalidOperationException(errorMessage);
    }

    private async Task<DashboardSnapshot?> TryLoadLiveSnapshotAsync(
        DashboardFilter collectFilter,
        DashboardFilter viewFilter,
        IProgress<LoadingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ApiCollectResult? live;
        try
        {
            live = await _apiRepository.TryCollectAsync(collectFilter, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            progress?.Report(new LoadingProgress("error", 0, $"API 수집 오류: {ex.GetType().Name} - {ex.Message}"));
            return null;
        }

        if (live is null)
        {
            return null;
        }

        var collectCompletedAt = DateTime.Now;
        var bidRetentionStart = GetBidCollectorRetentionStart(collectCompletedAt);
        var bidRetentionEndExclusive = GetBidCollectorRetentionEndExclusive(collectCompletedAt);
        var awardRetentionStart = await GetAwardCollectorRetentionStartAsync(collectCompletedAt, cancellationToken);
        var collectorReloadStart = bidRetentionStart < awardRetentionStart ? bidRetentionStart : awardRetentionStart;
        var collectorReloadEnd = bidRetentionEndExclusive > collectCompletedAt ? bidRetentionEndExclusive : collectCompletedAt;
        await EnsureCollectorNoticeCacheAsync(cancellationToken);
        var collectorReloadFilter = new DashboardFilter(
            StartDate: collectorReloadStart,
            EndDate: collectorReloadEnd,
            Region: "전국",
            Keyword: string.Empty,
            OrderingAgency: string.Empty,
            DataType: "낙찰",
            Company: string.Empty,
            MinAdjustRate: null,
            MaxAdjustRate: null);
        var refreshedCollectorFeed = NaraApiDashboardRepository.TestHook_LoadCollectorNoticeFeed(
            Path.Combine(_outputService.AppRootDirectory, "api-skill-output"),
            collectorReloadFilter,
            _pathResolver.ResolveApiRuleProfilePath());
        var mergedCollectorBidRows = MergeCollectorNoticeRows([], refreshedCollectorFeed.BidRows, bidRetentionStart, bidRetentionEndExclusive);
        var mergedCollectorAwardRows = MergeCollectorNoticeRows([], refreshedCollectorFeed.AwardRows, awardRetentionStart);
        await SaveCollectorNoticeCacheAsync(mergedCollectorBidRows, mergedCollectorAwardRows, bidRetentionStart, bidRetentionEndExclusive, awardRetentionStart, cancellationToken);
        SetCollectorNoticeCache(mergedCollectorBidRows, mergedCollectorAwardRows);

        var incrementalBidNotices = FilterBidCollectorNoticeRows(mergedCollectorBidRows, viewFilter, collectCompletedAt);
        var incrementalAwardNotices = FilterAwardCollectorNoticeRows(mergedCollectorAwardRows, viewFilter, awardRetentionStart);
        var collectorNoticeCount = incrementalBidNotices.Count + incrementalAwardNotices.Count;
        if (live.Participants.Count == 0)
        {
            WriteCollectCheckpoint(collectCompletedAt);
            progress?.Report(
                new LoadingProgress(
                    "checkpoint",
                    96,
                    $"증분 수집 완료(참여업체 0건 · 공고 {collectorNoticeCount:N0}건) · 다음 시작 기준: {collectCompletedAt:yyyy-MM-dd HH:mm:ss}"));

            var reloadedOnEmpty = await TryLoadSnapshotFromSqliteAsync(viewFilter, cancellationToken);
            if (reloadedOnEmpty is not null)
            {
                return new DashboardSnapshot(
                    reloadedOnEmpty.Kpis,
                    reloadedOnEmpty.NoticeRows,
                    $"{reloadedOnEmpty.DataSource} · 증분수집({collectFilter.StartDate:yyyy-MM-dd HH:mm}~{collectFilter.EndDate:yyyy-MM-dd HH:mm}) 신규 0건",
                    reloadedOnEmpty.SavedFilePath,
                    reloadedOnEmpty.PositionMapPath,
                    reloadedOnEmpty.RecommendationPath,
                    reloadedOnEmpty.CompanyOptions,
                    reloadedOnEmpty.Participants,
                    incrementalBidNotices,
                    incrementalAwardNotices);
            }

            return new DashboardSnapshot(
                live.Snapshot.Kpis,
                live.Snapshot.NoticeRows,
                $"파이프라인(실시간): 증분수집({collectFilter.StartDate:yyyy-MM-dd HH:mm}~{collectFilter.EndDate:yyyy-MM-dd HH:mm}) 신규 0건",
                live.Snapshot.SavedFilePath,
                live.Snapshot.PositionMapPath,
                live.Snapshot.RecommendationPath,
                live.Snapshot.CompanyOptions,
                live.Snapshot.Participants,
                incrementalBidNotices,
                incrementalAwardNotices);
        }

        var existingParticipants = await TryLoadAllParticipantsAsync(cancellationToken);
        var mergedParticipants = MergeParticipants(existingParticipants, live.Participants);
        mergedParticipants = EnrichAdjustRatesByNotice(mergedParticipants);
        mergedParticipants = KeepAwardOnlyData(mergedParticipants);
        SetParticipantsCache(mergedParticipants);

        var invalidNonAwardRows = live.Participants
            .GroupBy(x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => !g.Any(IsAwardRow))
            .SelectMany(g => g)
            .Count();
        var invalidRegionRows = live.Participants.Count(x => !IsStrictRegionCategory(x.Region));
        var crossCheck = BuildCrossCheck(existingParticipants, live.Participants, mergedParticipants);
        progress?.Report(
            new LoadingProgress(
                "verify",
                79,
                $"교차검증: 신규 {crossCheck.NewRows:N0} · 중복 {crossCheck.IncomingDuplicateRows:N0} · 기존충돌 {crossCheck.OverlapRows:N0} · 누락행 {crossCheck.MissingRowsAfterMerge:N0} · 누락공고 {crossCheck.MissingNoticesAfterMerge:N0}"));
        progress?.Report(
            new LoadingProgress(
                "verify",
                80,
                $"전처리검증: 낙찰규칙위반 {invalidNonAwardRows:N0}건 · 지역규칙위반 {invalidRegionRows:N0}건"));

        progress?.Report(
            new LoadingProgress(
                "pipeline",
                78,
                $"파이프라인 고정: 기존 {existingParticipants.Count:N0}행 + 신규 {live.Participants.Count:N0}행 → 병합 {mergedParticipants.Count:N0}행 저장"));
        var saveResult = await _outputService.SaveAllAsync(
            mergedParticipants,
            viewFilter,
            mergedParticipants,
            progress,
            cancellationToken);
        WriteCollectCheckpoint(collectCompletedAt);
        progress?.Report(
            new LoadingProgress(
                "checkpoint",
                96,
                $"증분 수집 완료 · 다음 시작 기준: {collectCompletedAt:yyyy-MM-dd HH:mm:ss}"));

        var reloadedSnapshot = await TryLoadSnapshotFromSqliteAsync(viewFilter, cancellationToken);
        if (reloadedSnapshot is not null)
        {
            var reloadedSource =
                $"파이프라인(실시간): 증분수집 {collectFilter.StartDate:yyyy-MM-dd HH:mm}~{collectFilter.EndDate:yyyy-MM-dd HH:mm} · API원천 {live.RawRowCount:N0}행 → 신규 {live.Participants.Count:N0}행 → 누적 {mergedParticipants.Count:N0}행";
            return new DashboardSnapshot(
                reloadedSnapshot.Kpis,
                reloadedSnapshot.NoticeRows,
                reloadedSource,
                saveResult.ExportFilePath,
                reloadedSnapshot.PositionMapPath,
                reloadedSnapshot.RecommendationPath,
                reloadedSnapshot.CompanyOptions,
                reloadedSnapshot.Participants,
                incrementalBidNotices,
                incrementalAwardNotices);
        }

        var source =
            $"파이프라인(실시간): 증분수집 {collectFilter.StartDate:yyyy-MM-dd HH:mm}~{collectFilter.EndDate:yyyy-MM-dd HH:mm} · API원천 {live.RawRowCount:N0}행 · 전처리 저장 {Path.GetFileName(saveResult.ExportFilePath)}";
        return new DashboardSnapshot(
            live.Snapshot.Kpis,
            live.Snapshot.NoticeRows,
            source,
            saveResult.ExportFilePath,
            saveResult.PositionMapPath,
            saveResult.RecommendationPath,
            live.Snapshot.CompanyOptions,
            live.Snapshot.Participants,
            incrementalBidNotices,
            incrementalAwardNotices);
    }

    private async Task<DashboardSnapshot?> TryLoadSnapshotFromSqliteAsync(DashboardFilter filter, CancellationToken cancellationToken)
    {
        if (TryGetSnapshotFromFilterCache(filter, out var cachedByFilter) && cachedByFilter is not null)
        {
            return cachedByFilter;
        }

        await EnsureCollectorNoticeCacheAsync(cancellationToken);

        if (_participantsCache is { Count: > 0 } cacheRows)
        {
            var memorySnapshot = await BuildSnapshotFromMemoryCacheAsync(cacheRows, filter, cancellationToken);
            StoreSnapshotInFilterCache(filter, memorySnapshot);
            return memorySnapshot;
        }

        if (!File.Exists(_dbPath))
        {
            return null;
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            var kpis = await LoadKpisAsync(connection, filter, cancellationToken);
            var notices = await LoadNoticesAsync(connection, filter, cancellationToken);
            if (kpis.Count == 0)
            {
                kpis =
                [
                    new("조회 공고 수", "0", "건"),
                    new("낙찰참여업체 수", "0", "건"),
                    new("낙찰데이터 수", "0", "건"),
                    new("문화재연구원 참여건", "0", "건"),
                    new("문화재연구원 낙찰건", "0", "건"),
                    new("평균 기초대비사정률", "0.000", "%"),
                    new("낙찰률", "0.00", "%"),
                    new("데이터구분", string.IsNullOrWhiteSpace(filter.DataType) ? "전체" : filter.DataType, "선택"),
                    new("기간", $"{Math.Max(1, (filter.EndDate - filter.StartDate).Days + 1):N0}", "일"),
                ];
            }

            var chartParticipants = await LoadParticipantsAsync(connection, filter, cancellationToken);
            var companyOptions = await LoadCompanyOptionsAsync(
                connection,
                filter with { Company = string.Empty },
                cancellationToken);

            var snapshot = new DashboardSnapshot(
                kpis,
                notices,
                $"SQLite 로컬DB: {_dbPath}",
                PositionMapPath: string.Empty,
                RecommendationPath: string.Empty,
                CompanyOptions: companyOptions,
                Participants: chartParticipants,
                IncrementalBidNoticeRows: FilterBidCollectorNoticeRows(_collectorBidCache ?? [], filter),
                IncrementalAwardNoticeRows: FilterAwardCollectorNoticeRows(_collectorAwardCache ?? [], filter));
            StoreSnapshotInFilterCache(filter, snapshot);
            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureDatabaseSeededAsync(
        IProgress<LoadingProgress>? progress,
        CancellationToken cancellationToken)
    {
        await _baselineSync.WaitAsync(cancellationToken);
        try
        {
            if (_baselineEnsured)
            {
                return;
            }
            var existingRows = await TryLoadAllParticipantsAsync(cancellationToken);
            if (existingRows.Count > 0)
            {
                progress?.Report(new LoadingProgress("seed", 6, $"기본 내장 데이터가 이미 반영되어 있습니다. (기존 {existingRows.Count:N0}행 재사용)"));
                SetParticipantsCache(existingRows);
                await EnsureCollectorNoticeCacheAsync(cancellationToken);
                _baselineEnsured = true;
                return;
            }

            if (!File.Exists(_masterExcelPath))
            {
                progress?.Report(new LoadingProgress("seed", 6, $"기본 내장 데이터 파일이 없어 빈 캐시로 시작합니다. ({_masterExcelPath})"));
                await EnsureCollectorNoticeCacheAsync(cancellationToken);
                _baselineEnsured = true;
                return;
            }

            progress?.Report(new LoadingProgress("seed", 4, $"기본 내장 데이터 로딩: {_masterExcelPath}"));
            var seededRows = await Task.Run(
                () => LoadParticipantsFromMasterExcel(_masterExcelPath),
                cancellationToken);
            if (seededRows.Count == 0)
            {
                progress?.Report(new LoadingProgress("seed", 6, "기본 내장 데이터가 비어 있어 빈 캐시로 시작합니다."));
                await EnsureCollectorNoticeCacheAsync(cancellationToken);
                _baselineEnsured = true;
                return;
            }

            var enrichedRows = await Task.Run(
                () => EnrichAdjustRatesByNotice(seededRows),
                cancellationToken);
            _masterLatestOpenDateCache = seededRows
                .Where(x => x.OpenDate > DateTime.MinValue)
                .Select(x => (DateTime?)x.OpenDate)
                .Max();

            await _outputService.ReplaceParticipantsInDatabaseAsync(enrichedRows, cancellationToken);
            SetParticipantsCache(enrichedRows);
            await EnsureCollectorNoticeCacheAsync(cancellationToken);
            progress?.Report(
                new LoadingProgress(
                    "seed",
                    6,
                    $"기본 데이터 반영 완료: 내장 {seededRows.Count:N0}행 → 누적 {enrichedRows.Count:N0}행"));

            _baselineEnsured = true;
        }
        finally
        {
            _baselineSync.Release();
        }
    }

    private async Task<List<ParticipantRecord>> TryLoadAllParticipantsAsync(CancellationToken cancellationToken)
    {
        if (_participantsCache is { Count: > 0 })
        {
            return _participantsCache.ToList();
        }

        if (!File.Exists(_dbPath))
        {
            return [];
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                               SELECT
                                 notice_no, notice_number, notice_name, ordering_agency, region, rank,
                                 company_name, normalized_company_name, open_date, bid_date,
                                 bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate,
                                 estimate_price, lower_limit_price, base_amount, price_adjustment_rate,
                                 lower_rate, lower_diff, biz_no, ceo_name, remark, is_awarded
                               FROM participants;
                               """;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var rows = await ReadParticipantsAsync(reader, cancellationToken);
            SetParticipantsCache(rows);
            return rows.ToList();
        }
        catch
        {
            return [];
        }
    }

    private void SetParticipantsCache(IReadOnlyList<ParticipantRecord> rows)
    {
        _participantsCache = rows.ToList();
        _awardedNoticeKeysCache = _participantsCache
            .Where(x => x.IsAwarded)
            .Select(GetNoticeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ClearSnapshotCache();
    }

    private bool TryGetSnapshotFromFilterCache(DashboardFilter filter, out DashboardSnapshot? snapshot)
    {
        var key = BuildFilterCacheKey(filter);
        lock (_snapshotCacheSync)
        {
            return _snapshotCache.TryGetValue(key, out snapshot);
        }
    }

    private async Task EnsureCollectorNoticeCacheAsync(CancellationToken cancellationToken)
    {
        if (_collectorBidCache is not null && _collectorAwardCache is not null)
        {
            return;
        }

        if (!File.Exists(_dbPath))
        {
            SetCollectorNoticeCache([], []);
            return;
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var (bidRows, awardRows) = await LoadCollectorNoticeCacheAsync(connection, cancellationToken);
            SetCollectorNoticeCache(bidRows, awardRows);
        }
        catch
        {
            SetCollectorNoticeCache([], []);
        }
    }

    private void SetCollectorNoticeCache(
        IReadOnlyList<CollectorNoticeRowModel> bidRows,
        IReadOnlyList<CollectorNoticeRowModel> awardRows)
    {
        _collectorBidCache = bidRows.ToList();
        _collectorAwardCache = awardRows.ToList();
        ClearSnapshotCache();
    }

    private void StoreSnapshotInFilterCache(DashboardFilter filter, DashboardSnapshot snapshot)
    {
        var key = BuildFilterCacheKey(filter);
        lock (_snapshotCacheSync)
        {
            if (_snapshotCache.Count >= SnapshotCacheLimit && !_snapshotCache.ContainsKey(key))
            {
                _snapshotCache.Clear();
            }

            _snapshotCache[key] = snapshot;
        }
    }

    private void ClearSnapshotCache()
    {
        lock (_snapshotCacheSync)
        {
            _snapshotCache.Clear();
        }
    }

    private static string BuildFilterCacheKey(DashboardFilter filter)
    {
        return string.Join(
            "|",
            filter.StartDate.ToString("yyyyMMdd"),
            filter.EndDate.ToString("yyyyMMdd"),
            NormalizeCacheToken(filter.Region),
            NormalizeCacheToken(filter.Keyword),
            NormalizeCacheToken(filter.OrderingAgency),
            NormalizeCacheToken(filter.DataType),
            NormalizeCacheToken(filter.Company),
            filter.MinAdjustRate?.ToString("F5", CultureInfo.InvariantCulture) ?? string.Empty,
            filter.MaxAdjustRate?.ToString("F5", CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static string NormalizeCacheToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private Task<DashboardSnapshot> BuildSnapshotFromMemoryCacheAsync(
        IReadOnlyList<ParticipantRecord> cacheRows,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => BuildSnapshotFromMemoryCache(cacheRows, filter),
            cancellationToken);
    }

    private DashboardSnapshot BuildSnapshotFromMemoryCache(
        IReadOnlyList<ParticipantRecord> cacheRows,
        DashboardFilter filter)
    {
        var keywords = SplitKeywords(filter.Keyword);
        var companyTokens = SplitCompanyTokens(filter.Company)
            .Select(NormalizeToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedRegions = ParseSelectedRegions(filter.Region);

        var minAdjust = filter.MinAdjustRate;
        var maxAdjust = filter.MaxAdjustRate;
        if (minAdjust.HasValue && maxAdjust.HasValue && minAdjust.Value > maxAdjust.Value)
        {
            (minAdjust, maxAdjust) = (maxAdjust, minAdjust);
        }

        var start = filter.StartDate.Date;
        var endExclusive = filter.EndDate.Date.AddDays(1);
        var awardedNoticeKeys = _awardedNoticeKeysCache
            ?? cacheRows
                .Where(x => x.IsAwarded)
                .Select(GetNoticeKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool MatchesBaseFilter(ParticipantRecord row)
        {
            if (row.OpenDate < start || row.OpenDate >= endExclusive)
            {
                return false;
            }

            if (!MatchesRegion(row.Region, selectedRegions))
            {
                return false;
            }

            if (!MatchesOrderingAgency(row.OrderingAgency, filter.OrderingAgency))
            {
                return false;
            }

            if (!MatchesKeywords(row.NoticeName, row.NoticeNumber, row.NoticeNo, keywords))
            {
                return false;
            }

            if (IsRetiredCompany(row.NormalizedCompanyName, row.CompanyName))
            {
                return false;
            }

            return MatchesDataType(row, filter.DataType, awardedNoticeKeys);
        }

        var baseRows = cacheRows.Where(MatchesBaseFilter).ToList();

        bool MatchesCompanyAndAdjust(ParticipantRecord row)
        {
            if (!MatchesCompany(row, companyTokens))
            {
                return false;
            }

            return MatchesAdjustRate(row.AdjustRate, minAdjust, maxAdjust);
        }

        var filteredRows = baseRows
            .Where(MatchesCompanyAndAdjust)
            .OrderByDescending(x => x.OpenDate)
            .ThenBy(x => x.NoticeNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ToList();

        var companyOptions = baseRows
            .Select(x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName : x.NormalizedCompanyName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .Take(600)
            .ToList();

        var builder = new NaraApiSnapshotBuilder();
        var built = builder.BuildSnapshot(filteredRows, filter, $"SQLite 로컬DB: {_dbPath} · 메모리 캐시");
        return new DashboardSnapshot(
            built.Kpis,
            built.NoticeRows,
            built.DataSource,
            built.SavedFilePath,
            built.PositionMapPath,
            built.RecommendationPath,
            companyOptions,
            filteredRows,
            FilterBidCollectorNoticeRows(_collectorBidCache ?? [], filter),
            FilterAwardCollectorNoticeRows(_collectorAwardCache ?? [], filter));
    }

    private static HashSet<string> ParseSelectedRegions(string? regionText)
    {
        if (string.IsNullOrWhiteSpace(regionText))
        {
            return [];
        }

        var normalized = NormalizeRegionValue(regionText);
        var tokens = normalized
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        tokens.Remove("전체");
        return tokens;
    }

    private static bool MatchesRegion(string? sourceRegion, IReadOnlySet<string> selectedRegions)
    {
        if (selectedRegions.Count == 0 || selectedRegions.Contains("전체"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(sourceRegion))
        {
            return false;
        }

        var normalizedSource = NormalizeRegionValue(sourceRegion);
        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return false;
        }

        var sourceTokens = normalizedSource
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (sourceTokens.Count == 0)
        {
            return false;
        }

        foreach (var token in selectedRegions)
        {
            if (sourceTokens.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesOrderingAgency(string? sourceAgency, string? filterAgency)
    {
        if (string.IsNullOrWhiteSpace(filterAgency))
        {
            return true;
        }

        return sourceAgency?.Contains(filterAgency.Trim(), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool MatchesKeywords(
        string? noticeName,
        string? noticeNumber,
        string? noticeNo,
        IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        var safeNoticeName = noticeName ?? string.Empty;
        var safeNoticeNumber = string.IsNullOrWhiteSpace(noticeNumber) ? noticeNo ?? string.Empty : noticeNumber;

        foreach (var keyword in keywords)
        {
            if (safeNoticeName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || safeNoticeNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesDataType(
        ParticipantRecord row,
        string? selectedDataType,
        IReadOnlySet<string> awardedNoticeKeys)
    {
        if (string.Equals(selectedDataType, "전체", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(selectedDataType, "입찰", StringComparison.OrdinalIgnoreCase))
        {
            return !row.IsAwarded;
        }

        if (!string.Equals(selectedDataType, "낙찰", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var noticeKey = GetNoticeKey(row);
        return !string.IsNullOrWhiteSpace(noticeKey) && awardedNoticeKeys.Contains(noticeKey);
    }

    private static bool MatchesCompany(ParticipantRecord row, IReadOnlyList<string> normalizedCompanyTokens)
    {
        if (normalizedCompanyTokens.Count == 0)
        {
            return true;
        }

        var normalizedCompany = NormalizeToken(row.NormalizedCompanyName);
        var rawCompany = NormalizeToken(row.CompanyName);
        foreach (var token in normalizedCompanyTokens)
        {
            if ((normalizedCompany.Contains(token, StringComparison.OrdinalIgnoreCase))
                || (rawCompany.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAdjustRate(double? adjustRate, double? minAdjust, double? maxAdjust)
    {
        if (!minAdjust.HasValue || !maxAdjust.HasValue)
        {
            return true;
        }

        if (!adjustRate.HasValue)
        {
            return false;
        }

        return adjustRate.Value >= minAdjust.Value && adjustRate.Value <= maxAdjust.Value;
    }

    private static async Task<List<ParticipantRecord>> ReadParticipantsAsync(
        SqliteDataReader reader,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParticipantRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var openDateText = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            var openDate = DateTime.TryParse(openDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOpenDate)
                ? parsedOpenDate
                : DateTime.MinValue;

            DateTime? bidDate = null;
            if (!reader.IsDBNull(9))
            {
                var bidDateText = reader.GetString(9);
                if (DateTime.TryParse(bidDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedBidDate))
                {
                    bidDate = parsedBidDate;
                }
            }

            rows.Add(new ParticipantRecord(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                openDate,
                bidDate,
                reader.IsDBNull(10) ? null : reader.GetInt64(10),
                reader.IsDBNull(11) ? null : reader.GetDouble(11),
                reader.IsDBNull(12) ? null : reader.GetDouble(12),
                reader.IsDBNull(13) ? null : reader.GetDouble(13),
                reader.IsDBNull(14) ? null : reader.GetInt64(14),
                reader.IsDBNull(15) ? null : reader.GetInt64(15),
                reader.IsDBNull(16) ? null : reader.GetInt64(16),
                reader.IsDBNull(17) ? null : reader.GetDouble(17),
                reader.IsDBNull(18) ? null : reader.GetDouble(18),
                reader.IsDBNull(19) ? null : reader.GetInt64(19),
                reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                !reader.IsDBNull(23) && reader.GetInt32(23) == 1));
        }

        return rows
            .Where(x => !IsRetiredCompany(x.NormalizedCompanyName, x.CompanyName))
            .ToList();
    }

    private static List<ParticipantRecord> LoadParticipantsFromMasterExcel(string masterExcelPath)
    {
        using var workbook = new XLWorkbook(masterExcelPath);
        var worksheet = workbook.Worksheets.First();
        var headerMap = BuildHeaderMap(worksheet);
        if (headerMap.Count == 0)
        {
            return [];
        }

        var rows = new List<ParticipantRecord>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var noticeNumber = ReadCellText(row, headerMap, "공고번호");
            if (string.IsNullOrWhiteSpace(noticeNumber))
            {
                continue;
            }

            var companyName = ReadCellText(row, headerMap, "업체명");
            var normalizedCompanyName = NormalizeCompanyName(companyName);
            if (IsRetiredCompany(normalizedCompanyName, companyName))
            {
                continue;
            }
            var rank = ReadCellInt(row, headerMap, "순위");
            var openDate = ReadCellDateTime(row, headerMap, "개찰일시") ?? DateTime.MinValue;
            var bidDate = ReadCellDateTime(row, headerMap, "투찰일시");

            rows.Add(
                new ParticipantRecord(
                    NoticeNo: ExtractNoticeNo(noticeNumber),
                    NoticeNumber: noticeNumber.Trim(),
                    NoticeName: ReadCellText(row, headerMap, "공고명"),
                    OrderingAgency: ReadCellText(row, headerMap, "발주처"),
                    Region: NormalizeRegionValue(ReadCellText(row, headerMap, "지역제한")),
                    Rank: rank,
                    CompanyName: companyName,
                    NormalizedCompanyName: normalizedCompanyName,
                    OpenDate: openDate,
                    BidDate: bidDate,
                    BidAmount: ReadCellLong(row, headerMap, "투찰금액"),
                    BidRateVsEstimate: ReadCellDouble(row, headerMap, "예가대비투찰률"),
                    BidRateVsBase: ReadCellDouble(row, headerMap, "기초대비투찰률"),
                    AdjustRate: ReadCellDouble(row, headerMap, "기초대비사정률"),
                    EstimatePrice: ReadCellLong(row, headerMap, "예정가격"),
                    LowerLimitPrice: ReadCellLong(row, headerMap, "낙찰하한가"),
                    BaseAmount: ReadCellLong(row, headerMap, "기초금액"),
                    PriceAdjustmentRate: ReadCellDouble(row, headerMap, "사정률"),
                    LowerRate: ReadCellDouble(row, headerMap, "발주처투찰률"),
                    LowerDiff: ReadCellLong(row, headerMap, "낙찰하한가차이"),
                    BizNo: string.Empty,
                    CeoName: string.Empty,
                    Remark: string.Empty,
                    IsAwarded: rank == 1));
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(1);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!map.ContainsKey(text))
            {
                map[text] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static string ReadCellText(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        if (!headerMap.TryGetValue(column, out var columnIndex))
        {
            return string.Empty;
        }

        return row.Cell(columnIndex).GetValue<string>().Trim();
    }

    private static double? ReadCellDouble(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        var text = ReadCellText(row, headerMap, column);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = text.Replace(",", "", StringComparison.Ordinal).Trim();
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ReadCellLong(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        var number = ReadCellDouble(row, headerMap, column);
        return number.HasValue
            ? (long)Math.Round(number.Value, 0, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int ReadCellInt(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        var number = ReadCellDouble(row, headerMap, column);
        return number.HasValue
            ? (int)Math.Round(number.Value, 0, MidpointRounding.AwayFromZero)
            : -1;
    }

    private static DateTime? ReadCellDateTime(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        if (!headerMap.TryGetValue(column, out var columnIndex))
        {
            return null;
        }

        var cell = row.Cell(columnIndex);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime();
        }

        var text = cell.GetValue<string>().Trim();
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static List<ParticipantRecord> MergeParticipants(
        IReadOnlyList<ParticipantRecord> existing,
        IReadOnlyList<ParticipantRecord> incoming)
    {
        var merged = new Dictionary<string, ParticipantRecord>(StringComparer.OrdinalIgnoreCase);

        void Upsert(ParticipantRecord row)
        {
            var key = BuildParticipantKey(row);
            if (!merged.TryGetValue(key, out var current))
            {
                merged[key] = row;
                return;
            }

            merged[key] = SelectLatest(current, row);
        }

        foreach (var row in existing)
        {
            Upsert(row);
        }

        foreach (var row in incoming)
        {
            Upsert(row);
        }

        return merged.Values
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ParticipantRecord SelectLatest(ParticipantRecord left, ParticipantRecord right)
    {
        if (right.OpenDate > left.OpenDate)
        {
            return right;
        }

        if (left.OpenDate > right.OpenDate)
        {
            return left;
        }

        var leftScore = CompletionScore(left);
        var rightScore = CompletionScore(right);
        return rightScore >= leftScore ? right : left;
    }

    private static int CompletionScore(ParticipantRecord record)
    {
        var score = 0;
        if (record.BidDate.HasValue)
        {
            score++;
        }
        if (record.BidAmount.HasValue)
        {
            score++;
        }
        if (record.BidRateVsEstimate.HasValue)
        {
            score++;
        }
        if (record.BidRateVsBase.HasValue)
        {
            score++;
        }
        if (record.AdjustRate.HasValue)
        {
            score++;
        }
        if (record.EstimatePrice.HasValue)
        {
            score++;
        }
        if (record.LowerLimitPrice.HasValue)
        {
            score++;
        }
        if (record.BaseAmount.HasValue)
        {
            score++;
        }
        if (record.PriceAdjustmentRate.HasValue)
        {
            score++;
        }
        if (record.LowerRate.HasValue)
        {
            score++;
        }
        if (record.LowerDiff.HasValue)
        {
            score++;
        }
        return score;
    }

    private static string BuildParticipantKey(ParticipantRecord row)
    {
        var normalizedNotice = NormalizeToken(row.NoticeNumber);
        var normalizedCompany = NormalizeToken(string.IsNullOrWhiteSpace(row.NormalizedCompanyName)
            ? row.CompanyName
            : row.NormalizedCompanyName);
        var bidAmountToken = row.BidAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{normalizedNotice}|{normalizedCompany}|{row.Rank}|{bidAmountToken}";
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", "", RegexOptions.IgnoreCase)
            .ToUpperInvariant();
    }

    private static string ExtractNoticeNo(string noticeNumber)
    {
        var text = noticeNumber.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var dashIndex = text.LastIndexOf("-", StringComparison.Ordinal);
        if (dashIndex <= 0)
        {
            return text;
        }

        var suffix = text[(dashIndex + 1)..];
        if (!suffix.All(char.IsDigit))
        {
            return text;
        }

        if (text.StartsWith("R", StringComparison.OrdinalIgnoreCase))
        {
            return text[..dashIndex];
        }

        return suffix.Length <= 3 ? text[..dashIndex] : text;
    }

    private static string NormalizeCompanyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var output = name.Trim();
        var patterns = new[]
        {
            @"\(재\)", @"\(재단\)", "재단법인", "재단",
            @"\(주\)", "주식회사",
            @"\(사\)", "사단법인",
            @"\(유\)", "유한회사",
            @"\(합\)", "합명회사",
            @"\(합자\)", "합자회사",
        };

        foreach (var pattern in patterns)
        {
            output = Regex.Replace(output, pattern, "", RegexOptions.IgnoreCase);
        }

        output = Regex.Replace(output, "문화유산연구원", "문화재연구원", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, "문화재보존센터", "문화재연구원", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, "고고학연구소", "문화재연구원", RegexOptions.IgnoreCase);
        if (output.Contains("문화유산마을", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("문화재연구원", StringComparison.OrdinalIgnoreCase))
        {
            output = "문화유산마을 문화재연구원";
        }
        output = Regex.Replace(output, @"[()（）]+", " ");
        output = Regex.Replace(output, @"\s+", " ");
        return output.Trim();
    }

    private static string NormalizeRegionValue(string? rawRegion)
    {
        if (string.IsNullOrWhiteSpace(rawRegion))
        {
            return string.Empty;
        }

        var normalized = rawRegion
            .Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("대구광역시", "대구", StringComparison.Ordinal)
            .Replace("경상북도", "경북", StringComparison.Ordinal)
            .Replace("경상북", "경북", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("/", ",", StringComparison.Ordinal)
            .Replace(";", ",", StringComparison.Ordinal)
            .Replace("|", ",", StringComparison.Ordinal)
            .Replace("·", ",", StringComparison.Ordinal)
            .Replace("ㆍ", ",", StringComparison.Ordinal);

        var tokens = normalized
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token =>
            {
                if (token.Contains("전국", StringComparison.Ordinal))
                {
                    return "전국";
                }

                if (token.Contains("대구", StringComparison.Ordinal))
                {
                    return "대구";
                }

                if (token.Contains("경북", StringComparison.Ordinal))
                {
                    return "경북";
                }

                return token;
            })
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return tokens.Count == 0
            ? string.Empty
            : string.Join(",", tokens);
    }

    private static bool IsRetiredCompany(string? normalizedCompanyName, string? rawCompanyName)
    {
        var normalized = NormalizeToken(normalizedCompanyName);
        if (normalized.Contains("한국문화재연구원", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("효림", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var raw = NormalizeToken(rawCompanyName);
        return raw.Contains("한국문화재연구원", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("효림", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<List<KpiCardModel>> LoadKpisAsync(SqliteConnection connection, DashboardFilter filter, CancellationToken cancellationToken)
    {
        var keywords = SplitKeywords(filter.Keyword);
        var keywordClause = BuildNoticeSearchClause(keywords, "kw");
        var regionClause = BuildRegionFilterClause();
        var companyTokens = SplitCompanyTokens(filter.Company);
        var companyClause = BuildCompanyFilterClause(companyTokens, "cp");
        var excludedCompanyClause = BuildExcludedCompanyClause();
        var adjustRateClause = BuildAdjustRateFilterClause();

        var dataTypeClause = BuildDataTypeFilterClause();
        var sql = $"""
                   SELECT
                     COUNT(DISTINCT notice_number) AS notice_count,
                     COUNT(*) AS participant_count,
                     SUM(CASE WHEN is_awarded = 0 THEN 1 ELSE 0 END) AS bid_count,
                     SUM(CASE WHEN is_awarded = 1 THEN 1 ELSE 0 END) AS award_count,
                     SUM(CASE WHEN (normalized_company_name LIKE '%동국문화재연구원%' OR normalized_company_name LIKE '%문화재연구원%' OR normalized_company_name LIKE '%문화유산연구원%') THEN 1 ELSE 0 END) AS culture_count,
                     SUM(CASE WHEN (normalized_company_name LIKE '%동국문화재연구원%' OR normalized_company_name LIKE '%문화재연구원%' OR normalized_company_name LIKE '%문화유산연구원%') AND is_awarded = 1 THEN 1 ELSE 0 END) AS culture_award_count,
                     AVG(adjust_rate) AS avg_adjust_rate,
                     AVG(CASE WHEN is_awarded = 1 THEN 1.0 ELSE 0.0 END) * 100.0 AS award_rate
                   FROM participants
                   WHERE open_date BETWEEN $start AND $end
                     AND {regionClause}
                     AND {companyClause}
                     AND {excludedCompanyClause}
                     AND {adjustRateClause}
                     AND ($agency = '' OR ordering_agency LIKE '%' || $agency || '%')
                     AND {dataTypeClause}
                     AND {keywordClause};
                   """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$start", filter.StartDate.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$end", filter.EndDate.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$region", filter.Region);
        cmd.Parameters.AddWithValue("$adjust_min", filter.MinAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$adjust_max", filter.MaxAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$agency", filter.OrderingAgency);
        cmd.Parameters.AddWithValue("$data_type", filter.DataType);
        AddKeywordParameters(cmd, keywords, "kw");
        AddCompanyParameters(cmd, companyTokens, "cp");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return [];
        }

        var noticeCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        _ = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        var bidCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        var awardCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
        var cultureCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
        var cultureAwardCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
        var avgAdjustRate = reader.IsDBNull(6) ? 0.0 : reader.GetDouble(6);
        var awardRate = reader.IsDBNull(7) ? 0.0 : reader.GetDouble(7);

        return
        [
            new("조회 공고 수", noticeCount.ToString("N0"), "건"),
            new("낙찰참여업체 수", (bidCount + awardCount).ToString("N0"), "건"),
            new("낙찰데이터 수", awardCount.ToString("N0"), "건"),
                new("문화재연구원 참여건", cultureCount.ToString("N0"), "건"),
                new("문화재연구원 낙찰건", cultureAwardCount.ToString("N0"), "건"),
            new("평균 기초대비사정률", avgAdjustRate.ToString("F3"), "%"),
            new("낙찰률", awardRate.ToString("F2"), "%"),
            new("데이터구분", filter.DataType, "선택"),
            new("기간", $"{(filter.EndDate - filter.StartDate).Days + 1}", "일"),
        ];
    }

    private async Task<DashboardFilter> BuildIncrementalCollectFilterAsync(
        DashboardFilter baseFilter,
        IProgress<LoadingProgress>? progress,
        DateTime? incrementalStartOverride,
        CancellationToken cancellationToken)
    {
        await EnsureCollectorNoticeCacheAsync(cancellationToken);
        var hasCollectorHistory = (_collectorBidCache?.Count ?? 0) > 0 || (_collectorAwardCache?.Count ?? 0) > 0;
        var now = DateTime.Now;
        if (!hasCollectorHistory)
        {
            var backfillStart = now.AddMonths(-3);
            if (backfillStart < baseFilter.StartDate)
            {
                backfillStart = baseFilter.StartDate;
            }

            progress?.Report(
                new LoadingProgress(
                    "checkpoint",
                    11,
                    $"공고 캐시 초기화: 최근 3개월({backfillStart:yyyy-MM-dd HH:mm:ss}~{now:yyyy-MM-dd HH:mm:ss}) 백필 수집"));

            return baseFilter with
            {
                StartDate = backfillStart,
                EndDate = now,
            };
        }

        var checkpoint = ReadCollectCheckpoint();
        var start = baseFilter.StartDate;
        var latestKnownOpenDate = await TryGetLatestKnownOpenDateAsync(cancellationToken);
        if (latestKnownOpenDate.HasValue)
        {
            var nextStart = latestKnownOpenDate.Value.AddMinutes(1);
            if (nextStart > start)
            {
                start = nextStart;
            }
        }

        if (checkpoint.HasValue && checkpoint.Value > start)
        {
            start = checkpoint.Value;
        }
        if (incrementalStartOverride.HasValue && incrementalStartOverride.Value > start)
        {
            start = incrementalStartOverride.Value;
        }

        if (start > now)
        {
            start = now;
        }

        progress?.Report(
            new LoadingProgress(
                "checkpoint",
                11,
                checkpoint.HasValue || incrementalStartOverride.HasValue || latestKnownOpenDate.HasValue
                    ? $"증분수집 기준: 기존최신개찰 {latestKnownOpenDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"} / 마지막완료 {checkpoint?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"} / 세션시작 {incrementalStartOverride?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"} 이후"
                    : "증분수집 기준 없음: 선택 기간 시작부터 수집"));

        return baseFilter with
        {
            StartDate = start,
            EndDate = now,
        };
    }

    private async Task<DateTime?> TryGetLatestKnownOpenDateAsync(CancellationToken cancellationToken)
    {
        if (_participantsCache is { Count: > 0 })
        {
            return _participantsCache
                .Where(x => x.OpenDate > DateTime.MinValue)
                .Select(x => (DateTime?)x.OpenDate)
                .Max();
        }

        var rows = await TryLoadAllParticipantsAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(x => x.OpenDate > DateTime.MinValue)
            .Select(x => (DateTime?)x.OpenDate)
            .Max();
    }

    private async Task<DateTime?> TryGetMasterLatestOpenDateAsync(CancellationToken cancellationToken)
    {
        if (_masterLatestOpenDateCache.HasValue)
        {
            return _masterLatestOpenDateCache;
        }

        if (!File.Exists(_masterExcelPath))
        {
            return null;
        }

        try
        {
            var rows = await Task.Run(
                () => LoadParticipantsFromMasterExcel(_masterExcelPath),
                cancellationToken);
            var latest = rows
                .Where(x => x.OpenDate > DateTime.MinValue)
                .Select(x => (DateTime?)x.OpenDate)
                .Max();
            _masterLatestOpenDateCache = latest;
            return latest;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime GetBidCollectorRetentionStart(DateTime now)
    {
        return now.Date.AddDays(-15);
    }

    internal static DateTime TestHook_GetBidCollectorRetentionStart(DateTime now)
    {
        return GetBidCollectorRetentionStart(now);
    }

    private static DateTime GetBidCollectorRetentionEndExclusive(DateTime now)
    {
        return now.Date.AddDays(16);
    }

    internal static DateTime TestHook_GetBidCollectorRetentionEndExclusive(DateTime now)
    {
        return GetBidCollectorRetentionEndExclusive(now);
    }

    private static DateTime GetAwardCollectorRetentionStart(DateTime? masterLatestOpenDate, DateTime now)
    {
        return masterLatestOpenDate?.AddMinutes(1) ?? now.AddMonths(-3);
    }

    internal static DateTime TestHook_GetAwardCollectorRetentionStart(DateTime? masterLatestOpenDate, DateTime now)
    {
        return GetAwardCollectorRetentionStart(masterLatestOpenDate, now);
    }

    private async Task<DateTime> GetAwardCollectorRetentionStartAsync(DateTime now, CancellationToken cancellationToken)
    {
        var masterLatestOpenDate = await TryGetMasterLatestOpenDateAsync(cancellationToken);
        return GetAwardCollectorRetentionStart(masterLatestOpenDate, now);
    }

    private DateTime? ReadCollectCheckpoint()
    {
        try
        {
            if (!File.Exists(_collectStatePath))
            {
                return null;
            }

            var json = File.ReadAllText(_collectStatePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<CollectState>(json);
            if (state is null || string.IsNullOrWhiteSpace(state.LastCollectCompletedAt))
            {
                return null;
            }

            return DateTime.TryParse(
                state.LastCollectCompletedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteCollectCheckpoint(DateTime timestamp)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_collectStatePath)!);
            var payload = new CollectState(timestamp.ToString("O", CultureInfo.InvariantCulture));
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_collectStatePath, json);
        }
        catch
        {
            // 체크포인트 기록 실패는 수집 흐름을 막지 않는다.
        }
    }

    private static DashboardFilter NormalizeFilter(DashboardFilter filter)
    {
        if (!AwardOnlyMode)
        {
            return filter;
        }

        var start = filter.StartDate.Date;
        var end = filter.EndDate.Date < start ? start : filter.EndDate.Date;
        return filter with
        {
            StartDate = start,
            EndDate = end,
            DataType = "낙찰",
        };
    }

    private static bool IsAwardRow(ParticipantRecord row)
    {
        return row.IsAwarded || row.Rank == 1;
    }

    private static bool IsStrictRegionCategory(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return false;
        }

        var normalized = NormalizeRegionValue(region);
        return normalized.Contains("전국", StringComparison.Ordinal)
            || normalized.Contains("대구", StringComparison.Ordinal)
            || normalized.Contains("경북", StringComparison.Ordinal);
    }

    private static MergeCrossCheckResult BuildCrossCheck(
        IReadOnlyList<ParticipantRecord> existing,
        IReadOnlyList<ParticipantRecord> incoming,
        IReadOnlyList<ParticipantRecord> merged)
    {
        var existingKeys = existing
            .Select(BuildParticipantKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incomingKeys = incoming
            .Select(BuildParticipantKey)
            .ToList();
        var incomingUniqueKeys = incomingKeys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mergedKeys = merged
            .Select(BuildParticipantKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var incomingDuplicateRows = incoming.Count - incomingUniqueKeys.Count;
        var overlapRows = incomingUniqueKeys.Count(existingKeys.Contains);
        var newRows = incomingUniqueKeys.Count - overlapRows;
        var missingRowsAfterMerge = incomingUniqueKeys.Count(x => !mergedKeys.Contains(x));

        var incomingNotices = incoming
            .Select(x => NormalizeToken(string.IsNullOrWhiteSpace(x.NoticeNo) ? x.NoticeNumber : x.NoticeNo))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mergedNotices = merged
            .Select(x => NormalizeToken(string.IsNullOrWhiteSpace(x.NoticeNo) ? x.NoticeNumber : x.NoticeNo))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingNoticesAfterMerge = incomingNotices.Count(x => !mergedNotices.Contains(x));

        return new MergeCrossCheckResult(
            IncomingDuplicateRows: incomingDuplicateRows,
            OverlapRows: overlapRows,
            NewRows: newRows,
            MissingRowsAfterMerge: missingRowsAfterMerge,
            MissingNoticesAfterMerge: missingNoticesAfterMerge);
    }

    private static List<ParticipantRecord> KeepAwardOnlyData(
        IReadOnlyList<ParticipantRecord> rows)
    {
        var awardedNoticeKeys = rows
            .Where(IsAwardRow)
            .Select(GetNoticeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(x =>
            {
                var key = GetNoticeKey(x);
                return !string.IsNullOrWhiteSpace(key)
                    && awardedNoticeKeys.Contains(key);
            })
            .GroupBy(BuildParticipantKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.BidDate ?? x.OpenDate)
                .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                .First())
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ParticipantRecord> EnrichAdjustRatesByNotice(
        IReadOnlyList<ParticipantRecord> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var output = new List<ParticipantRecord>(rows.Count);
        foreach (var noticeGroup in rows.GroupBy(
                     x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                     StringComparer.OrdinalIgnoreCase))
        {
            var list = noticeGroup.ToList();
            var reference = list.FirstOrDefault(x =>
                                IsAwardRow(x)
                                && x.AdjustRate.HasValue
                                && x.BidRateVsEstimate.HasValue
                                && Math.Abs(x.BidRateVsEstimate.Value) > 0.000001)
                            ?? list.FirstOrDefault(x =>
                                x.AdjustRate.HasValue
                                && x.BidRateVsEstimate.HasValue
                                && Math.Abs(x.BidRateVsEstimate.Value) > 0.000001);
            if (reference is null)
            {
                output.AddRange(list);
                continue;
            }

            var factor = reference.AdjustRate!.Value / reference.BidRateVsEstimate!.Value;
            foreach (var row in list)
            {
                if (row.AdjustRate.HasValue || !row.BidRateVsEstimate.HasValue)
                {
                    output.Add(row);
                    continue;
                }

                var enrichedAdjust = Math.Round(
                    row.BidRateVsEstimate.Value * factor,
                    5,
                    MidpointRounding.AwayFromZero);
                output.Add(row with { AdjustRate = enrichedAdjust });
            }
        }

        return output
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetNoticeKey(ParticipantRecord row)
    {
        return NormalizeToken(string.IsNullOrWhiteSpace(row.NoticeNumber) ? row.NoticeNo : row.NoticeNumber);
    }

    private static async Task<List<NoticeRowModel>> LoadNoticesAsync(SqliteConnection connection, DashboardFilter filter, CancellationToken cancellationToken)
    {
        var keywords = SplitKeywords(filter.Keyword);
        var keywordClause = BuildNoticeSearchClause(keywords, "kw");
        var regionClause = BuildRegionFilterClause();
        var companyTokens = SplitCompanyTokens(filter.Company);
        var companyClause = BuildCompanyFilterClause(companyTokens, "cp");
        var excludedCompanyClause = BuildExcludedCompanyClause();
        var adjustRateClause = BuildAdjustRateFilterClause();

        var dataTypeClause = BuildDataTypeFilterClause();
        var sql = $"""
                   WITH filtered AS (
                     SELECT
                       notice_no, notice_number, notice_name, ordering_agency,
                       open_date, adjust_rate, is_awarded, rank
                     FROM participants
                     WHERE open_date BETWEEN $start AND $end
                       AND {regionClause}
                       AND {companyClause}
                       AND {excludedCompanyClause}
                       AND {adjustRateClause}
                       AND ($agency = '' OR ordering_agency LIKE '%' || $agency || '%')
                       AND {dataTypeClause}
                       AND {keywordClause}
                   ),
                   ranked AS (
                     SELECT
                       notice_number, notice_name, ordering_agency, open_date, adjust_rate, is_awarded,
                       ROW_NUMBER() OVER (
                         PARTITION BY notice_number
                         ORDER BY
                           CASE WHEN rank = 1 THEN 0 ELSE 1 END,
                           CASE WHEN is_awarded = 1 THEN 0 ELSE 1 END,
                           rank ASC,
                           open_date DESC,
                           notice_number DESC
                       ) AS row_no
                     FROM filtered
                   )
                   SELECT notice_number, notice_name, ordering_agency, open_date, adjust_rate, is_awarded
                   FROM ranked
                   WHERE row_no = 1
                   ORDER BY open_date DESC
                   LIMIT 300;
                   """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$start", filter.StartDate.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$end", filter.EndDate.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$region", filter.Region);
        cmd.Parameters.AddWithValue("$adjust_min", filter.MinAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$adjust_max", filter.MaxAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$agency", filter.OrderingAgency);
        cmd.Parameters.AddWithValue("$data_type", filter.DataType);
        AddKeywordParameters(cmd, keywords, "kw");
        AddCompanyParameters(cmd, companyTokens, "cp");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var rows = new List<NoticeRowModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var noticeNumber = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var noticeName = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var orderingAgency = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var openDate = reader.IsDBNull(3) ? DateTime.MinValue : DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
            var adjustRate = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);
            var isAwarded = !reader.IsDBNull(5) && reader.GetInt32(5) == 1;

            rows.Add(new NoticeRowModel(noticeNumber, noticeName, orderingAgency, openDate, adjustRate, isAwarded));
        }

        return rows;
    }

    private static IReadOnlyList<NoticeRowModel> BuildIncrementalNoticeRows(
        IReadOnlyList<ParticipantRecord> participants,
        bool includeAwarded)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        return participants
            .GroupBy(x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var rows = g.ToList();
                var hasAward = rows.Any(IsAwardRow);
                if (hasAward != includeAwarded)
                {
                    return null;
                }

                var representative = rows
                    .OrderBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                    .ThenByDescending(x => x.OpenDate)
                    .FirstOrDefault();
                if (representative is null)
                {
                    return null;
                }

                return new NoticeRowModel(
                    representative.NoticeNumber,
                    representative.NoticeName,
                    representative.OrderingAgency,
                    representative.OpenDate,
                    representative.AdjustRate ?? 0.0,
                    hasAward);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.OpenDate)
            .Take(200)
            .ToList();
    }

    private static List<CollectorNoticeRowModel> MergeCollectorNoticeRows(
        IReadOnlyList<CollectorNoticeRowModel> existingRows,
        IReadOnlyList<CollectorNoticeRowModel> incomingRows,
        DateTime retentionStart,
        DateTime? retentionEndExclusive = null)
    {
        var merged = new Dictionary<string, CollectorNoticeRowModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existingRows.Concat(incomingRows))
        {
            if (row.NoticeDate < retentionStart
                || (retentionEndExclusive.HasValue && row.NoticeDate >= retentionEndExclusive.Value)
                || string.IsNullOrWhiteSpace(row.NoticeNumber))
            {
                continue;
            }

            if (!merged.TryGetValue(row.NoticeNumber, out var current)
                || row.NoticeDate >= current.NoticeDate)
            {
                merged[row.NoticeNumber] = row;
            }
        }

        return merged.Values
            .OrderByDescending(x => x.NoticeDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<CollectorNoticeRowModel> FilterBidCollectorNoticeRows(
        IReadOnlyList<CollectorNoticeRowModel> rows,
        DashboardFilter filter,
        DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.Now;
        var start = GetBidCollectorRetentionStart(now);
        var endExclusive = GetBidCollectorRetentionEndExclusive(now);
        var region = (filter.Region ?? string.Empty).Trim();

        return rows
            .Where(x => x.NoticeDate >= start && x.NoticeDate < endExclusive)
            .Where(x =>
                string.IsNullOrWhiteSpace(region)
                || string.Equals(region, "전국", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.RegionText)
                    && x.RegionText.Contains(region, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.NoticeDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<CollectorNoticeRowModel> FilterAwardCollectorNoticeRows(
        IReadOnlyList<CollectorNoticeRowModel> rows,
        DashboardFilter filter,
        DateTime? awardRetentionStartOverride = null)
    {
        var start = awardRetentionStartOverride
            ?? GetAwardCollectorRetentionStart(_masterLatestOpenDateCache, DateTime.Now);
        var end = filter.EndDate.Date.AddDays(1);
        var region = (filter.Region ?? string.Empty).Trim();

        return rows
            .Where(x => x.NoticeDate >= start && x.NoticeDate < end)
            .Where(x =>
                string.IsNullOrWhiteSpace(region)
                || string.Equals(region, "전국", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.RegionText)
                    && x.RegionText.Contains(region, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.NoticeDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SaveCollectorNoticeCacheAsync(
        IReadOnlyList<CollectorNoticeRowModel> bidRows,
        IReadOnlyList<CollectorNoticeRowModel> awardRows,
        DateTime bidRetentionStart,
        DateTime bidRetentionEndExclusive,
        DateTime awardRetentionStart,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? ".");
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureCollectorNoticeSchemaAsync(connection, cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using (var purgeCommand = connection.CreateCommand())
        {
            purgeCommand.Transaction = transaction;
            purgeCommand.CommandText = """
                                       DELETE FROM collector_notices
                                       WHERE notice_type IN ('bid', 'award');
                                       """;
            await purgeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string upsertSql = """
                                 INSERT INTO collector_notices (
                                     notice_type,
                                     notice_number,
                                     notice_name,
                                     ordering_agency,
                                     notice_date,
                                     region_text,
                                     participant_count_text,
                                     updated_at
                                 ) VALUES (
                                     $notice_type,
                                     $notice_number,
                                     $notice_name,
                                     $ordering_agency,
                                     $notice_date,
                                     $region_text,
                                     $participant_count_text,
                                     $updated_at
                                 )
                                 ON CONFLICT(notice_type, notice_number) DO UPDATE SET
                                     notice_name = excluded.notice_name,
                                     ordering_agency = excluded.ordering_agency,
                                     notice_date = excluded.notice_date,
                                     region_text = excluded.region_text,
                                     participant_count_text = excluded.participant_count_text,
                                     updated_at = excluded.updated_at;
                                 """;

        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.Transaction = transaction;
        upsertCommand.CommandText = upsertSql;
        var pNoticeType = upsertCommand.Parameters.Add("$notice_type", SqliteType.Text);
        var pNoticeNumber = upsertCommand.Parameters.Add("$notice_number", SqliteType.Text);
        var pNoticeName = upsertCommand.Parameters.Add("$notice_name", SqliteType.Text);
        var pOrderingAgency = upsertCommand.Parameters.Add("$ordering_agency", SqliteType.Text);
        var pNoticeDate = upsertCommand.Parameters.Add("$notice_date", SqliteType.Text);
        var pRegionText = upsertCommand.Parameters.Add("$region_text", SqliteType.Text);
        var pParticipantCountText = upsertCommand.Parameters.Add("$participant_count_text", SqliteType.Text);
        var pUpdatedAt = upsertCommand.Parameters.Add("$updated_at", SqliteType.Text);

        upsertCommand.Prepare();
        var updatedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        foreach (var pair in new[]
                 {
                     ("bid", bidRows),
                     ("award", awardRows),
                 })
        {
            foreach (var row in pair.Item2)
            {
                var minDate = string.Equals(pair.Item1, "bid", StringComparison.OrdinalIgnoreCase)
                    ? bidRetentionStart
                    : awardRetentionStart;
                if (row.NoticeDate < minDate
                    || (string.Equals(pair.Item1, "bid", StringComparison.OrdinalIgnoreCase) && row.NoticeDate >= bidRetentionEndExclusive)
                    || string.IsNullOrWhiteSpace(row.NoticeNumber))
                {
                    continue;
                }

                pNoticeType.Value = pair.Item1;
                pNoticeNumber.Value = row.NoticeNumber;
                pNoticeName.Value = row.NoticeName ?? string.Empty;
                pOrderingAgency.Value = row.OrderingAgency ?? string.Empty;
                pNoticeDate.Value = row.NoticeDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                pRegionText.Value = row.RegionText ?? string.Empty;
                pParticipantCountText.Value = row.ParticipantCountText ?? string.Empty;
                pUpdatedAt.Value = updatedAtText;
                await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureCollectorNoticeSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS collector_notices (
                               notice_type TEXT NOT NULL,
                               notice_number TEXT NOT NULL,
                               notice_name TEXT NOT NULL,
                               ordering_agency TEXT NOT NULL,
                               notice_date TEXT NOT NULL,
                               region_text TEXT NOT NULL,
                               participant_count_text TEXT NOT NULL,
                               updated_at TEXT NOT NULL,
                               PRIMARY KEY (notice_type, notice_number)
                           );
                           CREATE INDEX IF NOT EXISTS ix_collector_notices_date ON collector_notices(notice_date DESC);
                           CREATE INDEX IF NOT EXISTS ix_collector_notices_region ON collector_notices(region_text);
                           """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<(List<CollectorNoticeRowModel> BidRows, List<CollectorNoticeRowModel> AwardRows)> LoadCollectorNoticeCacheAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureCollectorNoticeSchemaAsync(connection, cancellationToken);
        var now = DateTime.Now;
        var bidRetentionStart = GetBidCollectorRetentionStart(now);
        var bidRetentionEndExclusive = GetBidCollectorRetentionEndExclusive(now);
        var awardRetentionStart = await GetAwardCollectorRetentionStartAsync(now, cancellationToken);
        const string sql = """
                           SELECT notice_type, notice_number, notice_name, ordering_agency, notice_date, region_text, participant_count_text
                           FROM collector_notices
                           WHERE (notice_type = 'bid' AND notice_date >= $bid_retain_from AND notice_date < $bid_retain_to)
                              OR (notice_type = 'award' AND notice_date >= $award_retain_from)
                           ORDER BY notice_date DESC, notice_number ASC;
                           """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$bid_retain_from", bidRetentionStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$bid_retain_to", bidRetentionEndExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$award_retain_from", awardRetentionStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var bidRows = new List<CollectorNoticeRowModel>();
        var awardRows = new List<CollectorNoticeRowModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var noticeType = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var noticeNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var noticeName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var orderingAgency = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var noticeDate = reader.IsDBNull(4)
                ? DateTime.MinValue
                : DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture);
            var regionText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            var participantCountText = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);

            var row = new CollectorNoticeRowModel(
                noticeDate,
                noticeNumber,
                noticeName,
                orderingAgency,
                regionText,
                participantCountText);

            if (string.Equals(noticeType, "award", StringComparison.OrdinalIgnoreCase))
            {
                awardRows.Add(row);
            }
            else
            {
                bidRows.Add(row);
            }
        }

        return (bidRows, awardRows);
    }

    private static async Task<List<ParticipantRecord>> LoadParticipantsAsync(
        SqliteConnection connection,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        var keywords = SplitKeywords(filter.Keyword);
        var keywordClause = BuildNoticeSearchClause(keywords, "kw");
        var regionClause = BuildRegionFilterClause();
        var companyTokens = SplitCompanyTokens(filter.Company);
        var companyClause = BuildCompanyFilterClause(companyTokens, "cp");
        var excludedCompanyClause = BuildExcludedCompanyClause();
        var adjustRateClause = BuildAdjustRateFilterClause();

        var dataTypeClause = BuildDataTypeFilterClause();
        var sql = $"""
                   SELECT
                     notice_no, notice_number, notice_name, ordering_agency, region, rank,
                     company_name, normalized_company_name, open_date, bid_date,
                     bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate,
                     estimate_price, lower_limit_price, base_amount, price_adjustment_rate,
                     lower_rate, lower_diff, biz_no, ceo_name, remark, is_awarded
                   FROM participants
                   WHERE open_date BETWEEN $start AND $end
                     AND {regionClause}
                     AND {companyClause}
                     AND {excludedCompanyClause}
                     AND {adjustRateClause}
                     AND ($agency = '' OR ordering_agency LIKE '%' || $agency || '%')
                     AND {dataTypeClause}
                     AND {keywordClause}
                   ORDER BY open_date DESC, notice_no, rank ASC;
                   """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$start", filter.StartDate.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$end", filter.EndDate.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$region", filter.Region);
        cmd.Parameters.AddWithValue("$adjust_min", filter.MinAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$adjust_max", filter.MaxAdjustRate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$agency", filter.OrderingAgency);
        cmd.Parameters.AddWithValue("$data_type", filter.DataType);
        AddKeywordParameters(cmd, keywords, "kw");
        AddCompanyParameters(cmd, companyTokens, "cp");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await ReadParticipantsAsync(reader, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> LoadCompanyOptionsAsync(
        SqliteConnection connection,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        var keywords = SplitKeywords(filter.Keyword);
        var keywordClause = BuildNoticeSearchClause(keywords, "kw");
        var regionClause = BuildRegionFilterClause();
        var excludedCompanyClause = BuildExcludedCompanyClause();

        var dataTypeClause = BuildDataTypeFilterClause();
        var sql = $"""
                   SELECT DISTINCT
                     CASE
                       WHEN TRIM(normalized_company_name) <> '' THEN normalized_company_name
                       ELSE company_name
                     END AS company_name
                   FROM participants
                   WHERE open_date BETWEEN $start AND $end
                     AND {regionClause}
                     AND {excludedCompanyClause}
                     AND ($agency = '' OR ordering_agency LIKE '%' || $agency || '%')
                     AND {dataTypeClause}
                     AND {keywordClause}
                   ORDER BY company_name COLLATE NOCASE
                   LIMIT 600;
                   """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$start", filter.StartDate.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$end", filter.EndDate.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$region", filter.Region);
        cmd.Parameters.AddWithValue("$agency", filter.OrderingAgency);
        cmd.Parameters.AddWithValue("$data_type", filter.DataType);
        AddKeywordParameters(cmd, keywords, "kw");

        return await ReadCompanyNamesAsync(cmd, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadCompanyNamesAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var companies = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var company = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            if (!string.IsNullOrWhiteSpace(company))
            {
                companies.Add(company);
            }
        }

        return companies;
    }

    private static string BuildRegionFilterClause()
    {
        const string normalizedRegionExpr = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(IFNULL(region, ''), '대구광역시', '대구'), '경상북도', '경북'), '경상북', '경북'), ' ', ''), '/', ','), ';', ','), '|', ','), '·', ','), 'ㆍ', ',')";
        const string normalizedSelectedExpr = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(IFNULL($region, ''), '대구광역시', '대구'), '경상북도', '경북'), '경상북', '경북'), ' ', ''), '/', ','), ';', ','), '|', ','), '·', ','), 'ㆍ', ',')";
        var hasSelectedNationwide = $"INSTR(',' || {normalizedSelectedExpr} || ',', ',전국,') > 0";
        var hasSelectedDaegu = $"INSTR(',' || {normalizedSelectedExpr} || ',', ',대구,') > 0";
        var hasSelectedGyeongbuk = $"INSTR(',' || {normalizedSelectedExpr} || ',', ',경북,') > 0";

        var hasSourceNationwide = $"""
                                   (
                                     {normalizedRegionExpr} = '전국'
                                     OR {normalizedRegionExpr} LIKE '전국,%'
                                     OR {normalizedRegionExpr} LIKE '%,전국'
                                     OR {normalizedRegionExpr} LIKE '%,전국,%'
                                   )
                                   """;
        var hasSourceDaegu = $"""
                              (
                                {normalizedRegionExpr} = '대구'
                                OR {normalizedRegionExpr} LIKE '대구,%'
                                OR {normalizedRegionExpr} LIKE '%,대구'
                                OR {normalizedRegionExpr} LIKE '%,대구,%'
                              )
                              """;
        var hasSourceGyeongbuk = $"""
                                 (
                                   {normalizedRegionExpr} = '경북'
                                   OR {normalizedRegionExpr} LIKE '경북,%'
                                   OR {normalizedRegionExpr} LIKE '%,경북'
                                   OR {normalizedRegionExpr} LIKE '%,경북,%'
                                 )
                                 """;

        return $"""
                (
                  $region = ''
                  OR $region = '전체'
                  OR (
                    {hasSelectedNationwide}
                    AND {hasSourceNationwide}
                  )
                  OR (
                    {hasSelectedDaegu}
                    AND {hasSourceDaegu}
                  )
                  OR (
                    {hasSelectedGyeongbuk}
                    AND {hasSourceGyeongbuk}
                  )
                )
                """;
    }

    private static string BuildCompanyFilterClause(IReadOnlyList<string> companyTokens, string prefix)
    {
        if (companyTokens.Count == 0)
        {
            return "1=1";
        }

        const string normalizedCompanyExpr = "REPLACE(IFNULL(normalized_company_name, ''), ' ', '')";
        const string normalizedRawCompanyExpr = "REPLACE(IFNULL(company_name, ''), ' ', '')";
        var clauses = new List<string>(companyTokens.Count);
        for (var i = 0; i < companyTokens.Count; i++)
        {
            clauses.Add($"""
                         (
                           {normalizedCompanyExpr} LIKE '%' || REPLACE(${prefix}{i}, ' ', '') || '%'
                           OR {normalizedRawCompanyExpr} LIKE '%' || REPLACE(${prefix}{i}, ' ', '') || '%'
                         )
                         """);
        }

        return $"({string.Join(" OR ", clauses)})";
    }

    private static List<string> SplitCompanyTokens(string companyText)
    {
        if (string.IsNullOrWhiteSpace(companyText))
        {
            return [];
        }

        var tokens = companyText
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        if (tokens.Count == 0)
        {
            return [];
        }

        return tokens.Any(IsAllCompanyToken) ? [] : tokens;
    }

    private static bool IsAllCompanyToken(string token)
    {
        return string.Equals(token, "전체", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "전체업체", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "전체경쟁업체", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCompanyParameters(SqliteCommand cmd, IReadOnlyList<string> companyTokens, string prefix)
    {
        for (var i = 0; i < companyTokens.Count; i++)
        {
            cmd.Parameters.AddWithValue($"${prefix}{i}", companyTokens[i]);
        }
    }

    private static string BuildExcludedCompanyClause()
    {
        const string normalizedCompanyExpr = "REPLACE(IFNULL(normalized_company_name, ''), ' ', '')";
        const string normalizedRawCompanyExpr = "REPLACE(IFNULL(company_name, ''), ' ', '')";

        return $"""
                (
                  {normalizedCompanyExpr} NOT LIKE '%한국문화재연구원%'
                  AND {normalizedRawCompanyExpr} NOT LIKE '%한국문화재연구원%'
                  AND {normalizedCompanyExpr} NOT LIKE '%효림%'
                  AND {normalizedRawCompanyExpr} NOT LIKE '%효림%'
                )
                """;
    }

    private static string BuildAdjustRateFilterClause()
    {
        return """
                (
                  $adjust_min IS NULL
                  OR $adjust_max IS NULL
                  OR (
                    adjust_rate IS NOT NULL
                    AND adjust_rate BETWEEN $adjust_min AND $adjust_max
                  )
                )
                """;
    }

    private static string BuildDataTypeFilterClause()
    {
        return """
                (
                  $data_type = '전체'
                  OR ($data_type = '입찰' AND is_awarded = 0)
                  OR (
                    $data_type = '낙찰'
                    AND EXISTS (
                      SELECT 1
                      FROM participants aw
                      WHERE
                        (
                          CASE
                            WHEN TRIM(IFNULL(aw.notice_number, '')) <> '' THEN aw.notice_number
                            ELSE aw.notice_no
                          END
                        ) = (
                          CASE
                            WHEN TRIM(IFNULL(participants.notice_number, '')) <> '' THEN participants.notice_number
                            ELSE participants.notice_no
                          END
                        )
                        AND aw.is_awarded = 1
                    )
                  )
                )
                """;
    }

    private static List<string> SplitKeywords(string keywordText)
    {
        return keywordText
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static string BuildNoticeSearchClause(IReadOnlyList<string> keywords, string prefix)
    {
        if (keywords.Count == 0)
        {
            return "1=1";
        }

        var clauses = new List<string>(keywords.Count);
        for (var i = 0; i < keywords.Count; i++)
        {
            clauses.Add($"(notice_name LIKE ${prefix}{i} OR notice_number LIKE ${prefix}{i})");
        }
        return $"({string.Join(" OR ", clauses)})";
    }

    private static void AddKeywordParameters(SqliteCommand cmd, IReadOnlyList<string> keywords, string prefix)
    {
        for (var i = 0; i < keywords.Count; i++)
        {
            cmd.Parameters.AddWithValue($"${prefix}{i}", $"%{keywords[i]}%");
        }
    }
}

public sealed record CollectState(string LastCollectCompletedAt);

public sealed record MergeCrossCheckResult(
    int IncomingDuplicateRows,
    int OverlapRows,
    int NewRows,
    int MissingRowsAfterMerge,
    int MissingNoticesAfterMerge);
