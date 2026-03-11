using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;

namespace NaraCollector.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string AllCompanyOption = "전체경쟁업체";
    private const string LegacyAllCompanyOption = "전체업체";
    private const string AutoCollectKeywordPreset = "시굴조사,발굴조사,표본조사,지표조사";
    private const string DonggukCompanyName = "동국문화재연구원";
    private const int MaxComparedCompanyCount = 20;
    private static readonly TimeSpan AutoApplyDebounceInterval = TimeSpan.FromMilliseconds(250);
    private static readonly string[] RetiredCompanyKeywords = ["효림", "한국문화재연구원"];

    private readonly IDashboardRepository _repository;
    private readonly ConsolePipelineInsightService _consolePipelineInsightService;
    private readonly MainWindowFilterState _filterState = new();
    private readonly MainWindowSnapshotCoordinator _snapshotCoordinator = new();
    private readonly MainWindowAlertPublisher _alertPublisher = new();
    private readonly DispatcherTimer _autoApplyDebounceTimer;

    private string _selectedPeriodPreset = "최근 3개월";
    private DateTime _startDate = DateTime.Today.AddMonths(-3);
    private DateTime _endDate = DateTime.Today;
    private string _selectedRegion = "전국";
    private bool _includeNationwide = true;
    private bool _includeDaegu;
    private bool _includeGyeongbuk;
    private bool _isSyncingRegionChecks;
    private bool _isSyncingCompanyChecks;
    private string _selectedDataType = "낙찰";
    private string _selectedCompany = AllCompanyOption;
    private string _selectedChartPrecision = "0.01";
    private string _selectedMinAdjustRate = "99.00000";
    private string _selectedMaxAdjustRate = "101.00000";
    private string _companySearchText = string.Empty;
    private string _keywordText = string.Empty;
    private string _orderingAgencyText = string.Empty;
    private string _lastSyncText = "마지막 동기화: -";
    private string _loadStatusText = "대기 중";
    private double _loadProgress;
    private bool _isLoading;
    private bool _isApplyingSnapshot;
    private bool _suppressAutoApply;
    private bool _reloadRequested;
    private string _savedFilePath = string.Empty;
    private string _wholeMarketSummaryText = "전체업체 분석 준비 중";
    private string _tacticalHeroTitle = "동국 기준 전술판";
    private string _tacticalHeroSummaryText = "필터를 적용하면 동국 기준 후보 밴드와 방해업체가 정리됩니다.";
    private string _tacticalPrimaryRecommendationText = "추천 밴드 없음";
    private string _tacticalPrimaryAnchorText = "우측 앵커 없음";
    private string _tacticalMarketStateText = "분석 대기";
    private string _tacticalDonggukPositionText = "동국 기준값 없음";
    private string _tacticalBlockerHeadlineText = "방해업체 분석 대기";
    private string _tacticalEvidenceSummaryText = "콘솔 분석 파일이 연결되면 금지/경계/후보 요약이 표시됩니다.";
    private string _consolePipelineStatusText = "콘솔 분석 결과를 아직 읽지 않았습니다.";
    private string _consolePipelineSummaryText = "콘솔 분석 파이프라인을 연결하면 후보밴드/군집/충돌 요약이 여기에 표시됩니다.";
    private string _consolePipelineCrowdingText = "군집 분석 결과 없음";
    private string _consolePipelineCollisionText = "충돌 분석 결과 없음";
    private string _consolePipelineShadowText = "전략 분석 결과 없음";
    private string _consolePipelineRecommendationText = "추천 밴드 없음";
    private string _consolePipelineRecommendationReasonText = "콘솔 파이프라인을 먼저 실행해야 합니다.";
    private string _interactivePointerRateText = "차트 위에서 마우스를 움직이면 현재 사정률이 표시됩니다.";
    private string _interactivePointerHintText = "후보 밴드 내부/외부 여부와 거리를 실시간으로 확인할 수 있습니다.";
    private string _incrementalBidSummaryText = "업데이트 대기";
    private string _incrementalAwardSummaryText = "업데이트 대기";
    private string _noticeAdjustRateTrendSummaryText = "날짜순 최근 6개월 이내 · 데이터 대기";
    private string _nextBidRecommendationText = "다음 입찰공고 추천자리 대기";
    private string _bidRecommendationReasonText = "입찰공고를 선택하면 추천 근거가 표시됩니다.";
    private string _highlightedTrendNoticeNumber = string.Empty;
    private NoticeRowModel? _selectedNotice;
    private CollectorNoticeRowModel? _selectedBidNoticeItem;
    private CollectorNoticeRowModel? _selectedAwardNoticeItem;
    private CancellationTokenSource? _loadCts;
    private IReadOnlyList<ParticipantRecord> _currentParticipants = Array.Empty<ParticipantRecord>();
    private IReadOnlyList<ParticipantRecord> _tacticalBattlefieldParticipants = Array.Empty<ParticipantRecord>();
    private IReadOnlyList<ParticipantRecord> _companyBaselineParticipants = Array.Empty<ParticipantRecord>();
    private DashboardFilter? _companyBaselineFilter;
    private DashboardFilter? _lastAppliedFilter;
    private DashboardFilter? _pendingAutoApplyFilter;
    private string _companyBaselineDataSource = string.Empty;
    private DateTime? _incrementalStartOverride;
    private ConsolePipelineInsight _latestConsolePipelineInsight = ConsolePipelineInsight.Empty("전국", "missing", "콘솔 분석 결과가 아직 없습니다.");

    public MainWindowViewModel(IDashboardRepository? repository = null, ConsolePipelineInsightService? consolePipelineInsightService = null)
    {
        _repository = repository ?? new SqliteDashboardRepository();
        _consolePipelineInsightService = consolePipelineInsightService ?? new ConsolePipelineInsightService();
        _autoApplyDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AutoApplyDebounceInterval,
        };
        _autoApplyDebounceTimer.Tick += AutoApplyDebounceTimerTick;

        CollectDataCommand = new AsyncRelayCommand(() => LoadSnapshotAsync(forceRefresh: true), () => !IsLoading);
        ApplyFilterCommand = new AsyncRelayCommand(() => LoadSnapshotAsync(forceRefresh: false), () => !IsLoading);
        StopCollectCommand = new RelayCommand(StopLoading, () => IsLoading);
        ResetFilterCommand = new RelayCommand(ResetFilters);

        SeedStaticOptions();
    }

    public ObservableCollection<string> PeriodPresets { get; } = [];
    public ObservableCollection<string> RegionOptions { get; } = [];
    public ObservableCollection<string> DataTypeOptions { get; } = [];
    public ObservableCollection<string> CompanyOptions { get; } = [];
    public ObservableCollection<string> SelectedCompanyNames { get; } = [];
    public ObservableCollection<CompanyCheckItem> CompanyCheckItems { get; } = [];
    public ObservableCollection<string> ChartPrecisionOptions { get; } = [];
    public ObservableCollection<string> AdjustRateRangeOptions { get; } = [];
    public ObservableCollection<KpiCardModel> KpiCards { get; } = [];
    public ObservableCollection<NoticeRowModel> NoticeItems { get; } = [];
    public ObservableCollection<CollectorNoticeRowModel> IncrementalBidNoticeItems { get; } = [];
    public ObservableCollection<CollectorNoticeRowModel> IncrementalAwardNoticeItems { get; } = [];
    public ObservableCollection<CollectorNoticeRowModel> VisibleBidNoticeItems { get; } = [];
    public ObservableCollection<CollectorNoticeRowModel> VisibleAwardNoticeItems { get; } = [];
    public ObservableCollection<CompanyAggregateRowModel> CompanyAggregateRows { get; } = [];
    public ObservableCollection<AdjustRateBinRowModel> AdjustRateBinRows { get; } = [];
    public ObservableCollection<TacticalCompanyRowModel> TacticalBlockerRows { get; } = [];
    public ObservableCollection<TacticalSignalRowModel> TacticalSignalRows { get; } = [];
    public ObservableCollection<MetricBarRowModel> ThreatMetricRows { get; } = [];
    public ObservableCollection<MetricBarRowModel> OverlapMetricRows { get; } = [];
    public ObservableCollection<MetricBarRowModel> ProximityMetricRows { get; } = [];
    public ObservableCollection<TrendPointModel> DonggukTrendPoints { get; } = [];
    public ObservableCollection<TrendPointModel> NoticeAdjustRateTrendPoints { get; } = [];
    public ObservableCollection<TrendPointModel> NoticeAwardReferenceTrendPoints { get; } = [];
    public ObservableCollection<HistogramBinModel> DensityHistogramBins { get; } = [];
    public ObservableCollection<ZoneSegmentModel> ZoneCompositionRows { get; } = [];
    public ObservableCollection<TacticalCompanyRowModel> TacticalRightAnchorRows { get; } = [];
    public ObservableCollection<TacticalNoticeRowModel> TacticalNoticeRows { get; } = [];
    public ObservableCollection<string> LoadEvents { get; } = [];

    public ICommand CollectDataCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand StopCollectCommand { get; }
    public ICommand ResetFilterCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<IReadOnlyList<RankAlertModel>>? RankAlertsDetected;
    public string SelectedPeriodPreset
    {
        get => _selectedPeriodPreset;
        set
        {
            if (SetProperty(ref _selectedPeriodPreset, value))
            {
                ApplyPeriodPreset(value);
                TriggerApplyFromFilterChange();
            }
        }
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                TriggerApplyFromFilterChange();
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (SetProperty(ref _selectedRegion, value))
            {
                SyncRegionChecksFromSelectedRegion(value);
                TriggerApplyFromFilterChange();
            }
        }
    }

    public bool IncludeNationwide
    {
        get => _includeNationwide;
        set
        {
            if (SetProperty(ref _includeNationwide, value))
            {
                if (value)
                {
                    ApplySingleRegionSelection("전국");
                }
                else
                {
                    EnsureAtLeastOneRegionSelected();
                    UpdateSelectedRegionFromChecks();
                }

                TriggerApplyFromFilterChange();
            }
        }
    }

    public bool IncludeDaegu
    {
        get => _includeDaegu;
        set
        {
            if (SetProperty(ref _includeDaegu, value))
            {
                if (value)
                {
                    ApplySingleRegionSelection("대구");
                }
                else
                {
                    EnsureAtLeastOneRegionSelected();
                    UpdateSelectedRegionFromChecks();
                }

                TriggerApplyFromFilterChange();
            }
        }
    }

    public bool IncludeGyeongbuk
    {
        get => _includeGyeongbuk;
        set
        {
            if (SetProperty(ref _includeGyeongbuk, value))
            {
                if (value)
                {
                    ApplySingleRegionSelection("경북");
                }
                else
                {
                    EnsureAtLeastOneRegionSelected();
                    UpdateSelectedRegionFromChecks();
                }

                TriggerApplyFromFilterChange();
            }
        }
    }

    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            if (SetProperty(ref _selectedDataType, value))
            {
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string SelectedCompany
    {
        get => _selectedCompany;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? AllCompanyOption : value.Trim();
            if (SetProperty(ref _selectedCompany, normalized))
            {
                SyncCompanyChecksFromSelectedCompany(normalized);
                UpdateSelectedCompanyFromChecks();
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string SelectedCompanySummary
    {
        get
        {
            var selectedNames = CompanyCheckItems
                .Where(x => x.IsChecked)
                .Select(x => x.Name)
                .ToList();
            if (selectedNames.Count == 0)
            {
                return "전체경쟁업체";
            }

            return string.Join(", ", selectedNames);
        }
    }

    public string SelectedCompanyCountText
        => $"선택 {CompanyCheckItems.Count(x => x.IsChecked)}/{MaxComparedCompanyCount}";

    public string CurrentFilterSummary
    {
        get
        {
            var keywordSummary = string.IsNullOrWhiteSpace(KeywordText)
                ? "키워드 전체"
                : $"키워드 {KeywordText.Trim()}";
            var orderingAgencySummary = string.IsNullOrWhiteSpace(OrderingAgencyText)
                ? "발주처 전체"
                : $"발주처 {OrderingAgencyText.Trim()}";
            return $"{SelectedPeriodPreset} · {StartDate:yyyy-MM-dd}~{EndDate:yyyy-MM-dd} · {SelectedRegion} · 사정률 {SelectedMinAdjustRate}~{SelectedMaxAdjustRate} · 업체 {SelectedCompanySummary} ({SelectedCompanyCountText}) · {keywordSummary} · {orderingAgencySummary}";
        }
    }

    public string SelectedChartPrecision
    {
        get => _selectedChartPrecision;
        set => SetProperty(ref _selectedChartPrecision, value);
    }

    public string CompanySearchText
    {
        get => _companySearchText;
        set
        {
            if (SetProperty(ref _companySearchText, value))
            {
                ApplyCompanySearchFilter();
            }
        }
    }

    public string SelectedMinAdjustRate
    {
        get => _selectedMinAdjustRate;
        set
        {
            if (SetProperty(ref _selectedMinAdjustRate, value))
            {
                EnsureValidAdjustRange();
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string SelectedMaxAdjustRate
    {
        get => _selectedMaxAdjustRate;
        set
        {
            if (SetProperty(ref _selectedMaxAdjustRate, value))
            {
                EnsureValidAdjustRange();
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string KeywordText
    {
        get => _keywordText;
        set
        {
            if (SetProperty(ref _keywordText, value))
            {
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string OrderingAgencyText
    {
        get => _orderingAgencyText;
        set
        {
            if (SetProperty(ref _orderingAgencyText, value))
            {
                TriggerApplyFromFilterChange();
            }
        }
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public string LoadStatusText
    {
        get => _loadStatusText;
        private set => SetProperty(ref _loadStatusText, value);
    }

    public double LoadProgress
    {
        get => _loadProgress;
        private set => SetProperty(ref _loadProgress, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SavedFilePath
    {
        get => _savedFilePath;
        private set => SetProperty(ref _savedFilePath, value);
    }

    public string WholeMarketSummaryText
    {
        get => _wholeMarketSummaryText;
        private set => SetProperty(ref _wholeMarketSummaryText, value);
    }

    public string TacticalHeroTitle
    {
        get => _tacticalHeroTitle;
        private set => SetProperty(ref _tacticalHeroTitle, value);
    }

    public string TacticalHeroSummaryText
    {
        get => _tacticalHeroSummaryText;
        private set => SetProperty(ref _tacticalHeroSummaryText, value);
    }

    public string TacticalPrimaryRecommendationText
    {
        get => _tacticalPrimaryRecommendationText;
        private set => SetProperty(ref _tacticalPrimaryRecommendationText, value);
    }

    public string TacticalPrimaryAnchorText
    {
        get => _tacticalPrimaryAnchorText;
        private set => SetProperty(ref _tacticalPrimaryAnchorText, value);
    }

    public string TacticalMarketStateText
    {
        get => _tacticalMarketStateText;
        private set => SetProperty(ref _tacticalMarketStateText, value);
    }

    public string TacticalDonggukPositionText
    {
        get => _tacticalDonggukPositionText;
        private set => SetProperty(ref _tacticalDonggukPositionText, value);
    }

    public string TacticalBlockerHeadlineText
    {
        get => _tacticalBlockerHeadlineText;
        private set => SetProperty(ref _tacticalBlockerHeadlineText, value);
    }

    public string TacticalEvidenceSummaryText
    {
        get => _tacticalEvidenceSummaryText;
        private set => SetProperty(ref _tacticalEvidenceSummaryText, value);
    }

    public double? RecommendationBandMin => _latestConsolePipelineInsight.RecommendationBandMin;

    public double? RecommendationBandMax => _latestConsolePipelineInsight.RecommendationBandMax;

    public string ConsolePipelineStatusText
    {
        get => _consolePipelineStatusText;
        private set => SetProperty(ref _consolePipelineStatusText, value);
    }

    public string ConsolePipelineSummaryText
    {
        get => _consolePipelineSummaryText;
        private set => SetProperty(ref _consolePipelineSummaryText, value);
    }

    public string ConsolePipelineCrowdingText
    {
        get => _consolePipelineCrowdingText;
        private set => SetProperty(ref _consolePipelineCrowdingText, value);
    }

    public string ConsolePipelineCollisionText
    {
        get => _consolePipelineCollisionText;
        private set => SetProperty(ref _consolePipelineCollisionText, value);
    }

    public string ConsolePipelineShadowText
    {
        get => _consolePipelineShadowText;
        private set => SetProperty(ref _consolePipelineShadowText, value);
    }

    public string ConsolePipelineRecommendationText
    {
        get => _consolePipelineRecommendationText;
        private set => SetProperty(ref _consolePipelineRecommendationText, value);
    }

    public string ConsolePipelineRecommendationReasonText
    {
        get => _consolePipelineRecommendationReasonText;
        private set => SetProperty(ref _consolePipelineRecommendationReasonText, value);
    }

    public string InteractivePointerRateText
    {
        get => _interactivePointerRateText;
        private set => SetProperty(ref _interactivePointerRateText, value);
    }

    public string InteractivePointerHintText
    {
        get => _interactivePointerHintText;
        private set => SetProperty(ref _interactivePointerHintText, value);
    }

    public string IncrementalBidSummaryText
    {
        get => _incrementalBidSummaryText;
        private set => SetProperty(ref _incrementalBidSummaryText, value);
    }

    public string IncrementalAwardSummaryText
    {
        get => _incrementalAwardSummaryText;
        private set => SetProperty(ref _incrementalAwardSummaryText, value);
    }

    public string NoticeAdjustRateTrendSummaryText
    {
        get => _noticeAdjustRateTrendSummaryText;
        private set => SetProperty(ref _noticeAdjustRateTrendSummaryText, value);
    }

    public string NextBidRecommendationText
    {
        get => _nextBidRecommendationText;
        private set => SetProperty(ref _nextBidRecommendationText, value);
    }

    public string BidRecommendationReasonText
    {
        get => _bidRecommendationReasonText;
        private set => SetProperty(ref _bidRecommendationReasonText, value);
    }

    public string HighlightedTrendNoticeNumber
    {
        get => _highlightedTrendNoticeNumber;
        private set => SetProperty(ref _highlightedTrendNoticeNumber, value);
    }

    public bool IsApplyingSnapshot
    {
        get => _isApplyingSnapshot;
        private set => SetProperty(ref _isApplyingSnapshot, value);
    }

    public NoticeRowModel? SelectedNotice
    {
        get => _selectedNotice;
        set => SetProperty(ref _selectedNotice, value);
    }

    public CollectorNoticeRowModel? SelectedBidNoticeItem
    {
        get => _selectedBidNoticeItem;
        set
        {
            if (!SetProperty(ref _selectedBidNoticeItem, value))
            {
                return;
            }

            RefreshFirstPageBidRecommendation();
        }
    }

    public CollectorNoticeRowModel? SelectedAwardNoticeItem
    {
        get => _selectedAwardNoticeItem;
        set
        {
            if (!SetProperty(ref _selectedAwardNoticeItem, value))
            {
                return;
            }

            HighlightedTrendNoticeNumber = value?.NoticeNumber ?? string.Empty;
        }
    }

    public IReadOnlyList<ParticipantRecord> CurrentParticipants
    {
        get => _currentParticipants;
        private set => SetProperty(ref _currentParticipants, value);
    }

    public IReadOnlyList<ParticipantRecord> TacticalBattlefieldParticipants
    {
        get => _tacticalBattlefieldParticipants;
        private set => SetProperty(ref _tacticalBattlefieldParticipants, value);
    }

    public void AppendExternalEvent(string message)
    {
        AppendEvent(message);
    }

    public void ApplyCompanyPickFromChart(string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return;
        }

        var token = companyName.Trim();
        if (IsAllCompanyToken(token))
        {
            SelectedCompany = AllCompanyOption;
            return;
        }

        if (CompanyCheckItems.Count == 0)
        {
            if (!string.Equals(_selectedCompany, token, StringComparison.Ordinal))
            {
                _selectedCompany = token;
                RaisePropertyChanged(nameof(SelectedCompany));
            }

            TriggerApplyFromFilterChange();
            return;
        }

        _isSyncingCompanyChecks = true;
        var matchedCount = 0;
        try
        {
            foreach (var item in CompanyCheckItems)
            {
                var isMatch = ContainsCompanyToken(item.Name, token);
                item.IsChecked = isMatch;
                if (isMatch)
                {
                    matchedCount++;
                }
            }
        }
        finally
        {
            _isSyncingCompanyChecks = false;
        }

        if (matchedCount == 0)
        {
            AppendEvent($"차트 선택업체 매칭 실패(원문 유지): {token}");
            if (!string.Equals(_selectedCompany, token, StringComparison.Ordinal))
            {
                _selectedCompany = token;
                RaisePropertyChanged(nameof(SelectedCompany));
            }

            TriggerApplyFromFilterChange();
            return;
        }

        UpdateSelectedCompanyFromChecks();
    }

    public void StartRealtimeAutoCollectSession(DateTime startedAt)
    {
        _incrementalStartOverride = startedAt;
        KeywordText = AutoCollectKeywordPreset;
        AppendEvent(
            $"실시간 증분 기준 설정: {startedAt:yyyy-MM-dd HH:mm:ss} 이후 · 키워드={AutoCollectKeywordPreset}");
    }

    public void StopRealtimeAutoCollectSession()
    {
        _incrementalStartOverride = null;
        AppendEvent("실시간 자동수집 세션 종료");
    }

    private void TriggerApplyFromFilterChange()
    {
        RaisePropertyChanged(nameof(CurrentFilterSummary));
        RaisePropertyChanged(nameof(SelectedCompanyCountText));

        if (_suppressAutoApply || IsApplyingSnapshot)
        {
            return;
        }

        var currentFilter = BuildFilter();
        if (_lastAppliedFilter is not null
            && currentFilter == _lastAppliedFilter
            && !IsLoading)
        {
            _pendingAutoApplyFilter = null;
            return;
        }

        if (TryApplyFromCompanyBaseline(currentFilter))
        {
            _pendingAutoApplyFilter = null;
            return;
        }

        _pendingAutoApplyFilter = currentFilter;
        if (IsLoading)
        {
            _reloadRequested = true;
            return;
        }

        QueueDebouncedAutoApply();
    }

    private void QueueDebouncedAutoApply()
    {
        _autoApplyDebounceTimer.Stop();
        _autoApplyDebounceTimer.Start();
    }

    private void AutoApplyDebounceTimerTick(object? sender, EventArgs e)
    {
        _autoApplyDebounceTimer.Stop();

        if (_suppressAutoApply || IsApplyingSnapshot)
        {
            return;
        }

        var targetFilter = _pendingAutoApplyFilter ?? BuildFilter();
        if (_lastAppliedFilter is not null
            && targetFilter == _lastAppliedFilter
            && !IsLoading)
        {
            _pendingAutoApplyFilter = null;
            return;
        }

        if (IsLoading)
        {
            _reloadRequested = true;
            return;
        }

        _pendingAutoApplyFilter = null;
        if (ApplyFilterCommand.CanExecute(null))
        {
            ApplyFilterCommand.Execute(null);
        }
    }

    private async Task LoadSnapshotAsync(bool forceRefresh)
    {
        if (IsLoading)
        {
            return;
        }

        var filter = BuildFilter();
        if (!forceRefresh
            && _snapshotCoordinator.TryGetSnapshot(filter, out var cachedSnapshot)
            && cachedSnapshot is not null)
        {
            IsLoading = true;
            LoadProgress = 0;
            LoadStatusText = "캐시 데이터 적용 중";
            AppendEvent($"캐시 적용: {SelectedRegion} / {SelectedDataType}");

            try
            {
                ApplySnapshot(cachedSnapshot, filter);
                LastSyncText = $"데이터소스: {cachedSnapshot.DataSource} (캐시 재사용) · 마지막 동기화: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                LoadProgress = 100;
                LoadStatusText = $"완료(캐시) · 공고 {cachedSnapshot.NoticeRows.Count:N0}건";
            }
            finally
            {
                IsLoading = false;
            }

            return;
        }

        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsLoading = true;
        LoadProgress = 0;
        LoadStatusText = forceRefresh ? "데이터 수집 시작 (API 강제수집)" : "필터 적용 시작 (캐시 없음)";
        LoadEvents.Clear();
        AppendEvent(forceRefresh ? "모드: forceRefresh=true" : "모드: forceRefresh=false");

        try
        {
            var progress = new Progress<LoadingProgress>(p =>
            {
                LoadProgress = Math.Clamp(p.Percent, 0, 100);
                LoadStatusText = string.IsNullOrWhiteSpace(p.Message) ? p.Stage : p.Message;
                AppendEvent($"{p.Stage} · {p.Message}");
            });

            var snapshot = await _repository.GetSnapshotAsync(
                filter,
                progress,
                cts.Token,
                forceRefresh,
                forceRefresh ? _incrementalStartOverride : null);
            _snapshotCoordinator.StoreSnapshot(filter, snapshot);

            ApplySnapshot(snapshot, filter);
            LastSyncText = $"데이터소스: {snapshot.DataSource} · 마지막 동기화: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            LoadProgress = 100;
            LoadStatusText = $"완료 · 공고 {snapshot.NoticeRows.Count:N0}건";
            if (!string.IsNullOrWhiteSpace(SavedFilePath))
            {
                AppendEvent($"전처리 저장 파일: {SavedFilePath}");
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            LoadStatusText = "수집이 중단되었습니다.";
            AppendEvent("사용자 요청으로 수집이 중단되었습니다.");
        }
        catch (Exception ex)
        {
            LoadStatusText = $"오류: {ex.Message}";
            AppendEvent($"오류: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                _loadCts.Dispose();
                _loadCts = null;
            }

            IsLoading = false;
            if (_reloadRequested)
            {
                _reloadRequested = false;
                if (!_suppressAutoApply)
                {
                    _pendingAutoApplyFilter = BuildFilter();
                    QueueDebouncedAutoApply();
                }
            }
        }
    }

    private void ResetFilters()
    {
        _suppressAutoApply = true;
        try
        {
            SelectedPeriodPreset = "최근 3개월";
            ApplyPeriodPreset("최근 3개월");
            SelectedRegion = "전국";
            SelectedDataType = "낙찰";
            SelectedCompany = AllCompanyOption;
            SelectedMinAdjustRate = "99.00000";
            SelectedMaxAdjustRate = "101.00000";
            CompanySearchText = string.Empty;
            KeywordText = string.Empty;
            OrderingAgencyText = string.Empty;
        }
        finally
        {
            _suppressAutoApply = false;
        }

        TriggerApplyFromFilterChange();
    }

    private void SeedStaticOptions()
    {
        PeriodPresets.Add("최근 3개월");
        PeriodPresets.Add("최근 1개월");
        PeriodPresets.Add("최근 6개월");
        PeriodPresets.Add("최근 1년");
        PeriodPresets.Add("전체기간");

        RegionOptions.Add("전국");
        RegionOptions.Add("경북");

        DataTypeOptions.Add("낙찰");
        SelectedDataType = "낙찰";
        SyncRegionChecksFromSelectedRegion("전국");

        CompanyOptions.Add(AllCompanyOption);

        ChartPrecisionOptions.Add("0.01");
        ChartPrecisionOptions.Add("0.001");

        for (var value = 98.0; value <= 102.000001; value += 0.001)
        {
            AdjustRateRangeOptions.Add(value.ToString("F5"));
        }
    }

    private void ApplyPeriodPreset(string value)
    {
        _suppressAutoApply = true;
        try
        {
            EndDate = DateTime.Today;
            StartDate = value switch
            {
                "전체기간" => new DateTime(2000, 1, 1),
                "최근 1개월" => DateTime.Today.AddMonths(-1),
                "최근 3개월" => DateTime.Today.AddMonths(-3),
                "최근 6개월" => DateTime.Today.AddMonths(-6),
                "최근 1년" => DateTime.Today.AddYears(-1),
                _ => DateTime.Today.AddMonths(-3),
            };
        }
        finally
        {
            _suppressAutoApply = false;
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void UpdateSelectedRegionFromChecks()
    {
        if (_isSyncingRegionChecks)
        {
            return;
        }

        var selectedRegion = IncludeNationwide
            ? "전국"
            : IncludeGyeongbuk
                ? "경북"
                : "전국";

        if (!string.Equals(_selectedRegion, selectedRegion, StringComparison.Ordinal))
        {
            _selectedRegion = selectedRegion;
            RaisePropertyChanged(nameof(SelectedRegion));
        }
    }

    private void SyncRegionChecksFromSelectedRegion(string? selectedRegion)
    {
        if (_isSyncingRegionChecks)
        {
            return;
        }

        var tokens = (selectedRegion ?? string.Empty)
            .Split([',', '/', ';', '|', '·', 'ㆍ'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var target = tokens.Contains("경북")
            ? "경북"
            : "전국";

        ApplySingleRegionSelection(target);
    }

    private void EnsureAtLeastOneRegionSelected()
    {
        if (IncludeNationwide || IncludeDaegu || IncludeGyeongbuk)
        {
            return;
        }

        ApplySingleRegionSelection("전국");
    }

    private void ApplySingleRegionSelection(string regionToken)
    {
        if (_isSyncingRegionChecks)
        {
            return;
        }

        _isSyncingRegionChecks = true;
        try
        {
            SetProperty(ref _includeNationwide, string.Equals(regionToken, "전국", StringComparison.Ordinal), nameof(IncludeNationwide));
            SetProperty(ref _includeDaegu, string.Equals(regionToken, "대구", StringComparison.Ordinal), nameof(IncludeDaegu));
            SetProperty(ref _includeGyeongbuk, string.Equals(regionToken, "경북", StringComparison.Ordinal), nameof(IncludeGyeongbuk));
        }
        finally
        {
            _isSyncingRegionChecks = false;
        }

        UpdateSelectedRegionFromChecks();
    }

    private void ReplaceCompanyCheckItems(IEnumerable<string> companies)
    {
        var previouslySelected = CompanyCheckItems
            .Where(x => x.IsChecked)
            .Select(x => CompanyNamePresentation.Canonicalize(x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedCompanies = companies
            .Select(CompanyNamePresentation.Canonicalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var displayMap = CompanyNamePresentation.BuildDisplayMap(normalizedCompanies);

        _isSyncingCompanyChecks = true;
        try
        {
            foreach (var item in CompanyCheckItems)
            {
                item.PropertyChanged -= CompanyCheckItemPropertyChanged;
            }

            CompanyCheckItems.Clear();
            foreach (var name in normalizedCompanies
                         .Where(x => !IsRetiredCompanyName(x)))
            {
                var item = new CompanyCheckItem(name, displayMap.TryGetValue(name, out var displayName) ? displayName : name, previouslySelected.Contains(name));
                item.PropertyChanged += CompanyCheckItemPropertyChanged;
                CompanyCheckItems.Add(item);
            }
        }
        finally
        {
            _isSyncingCompanyChecks = false;
        }

        SyncCompanyChecksFromSelectedCompany(_selectedCompany);
        UpdateSelectedCompanyFromChecks();
        ApplyCompanySearchFilter();
    }

    private void CompanyCheckItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingCompanyChecks || e.PropertyName != nameof(CompanyCheckItem.IsChecked))
        {
            return;
        }

        if (sender is not CompanyCheckItem changedItem)
        {
            return;
        }

        if (changedItem.IsChecked)
        {
            var checkedCount = CompanyCheckItems.Count(x => x.IsChecked);
            if (checkedCount > MaxComparedCompanyCount)
            {
                _isSyncingCompanyChecks = true;
                try
                {
                    changedItem.IsChecked = false;
                }
                finally
                {
                    _isSyncingCompanyChecks = false;
                }

                AppendEvent($"경쟁업체 비교는 최대 {MaxComparedCompanyCount}개까지만 선택할 수 있습니다.");
            }
        }

        UpdateSelectedCompanyFromChecks();
    }

    private void SyncCompanyChecksFromSelectedCompany(string? selectedCompany)
    {
        if (_isSyncingCompanyChecks || CompanyCheckItems.Count == 0)
        {
            return;
        }

        var tokens = SplitCompanyTokens(selectedCompany)
            .Take(MaxComparedCompanyCount)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectAll = tokens.Count == 0 || tokens.Any(IsAllCompanyToken);

        _isSyncingCompanyChecks = true;
        try
        {
            foreach (var item in CompanyCheckItems)
            {
                item.IsChecked = !selectAll && tokens.Any(x => ContainsCompanyToken(item.Name, x));
            }
        }
        finally
        {
            _isSyncingCompanyChecks = false;
        }
    }

    private void UpdateSelectedCompanyFromChecks()
    {
        var selectedNames = CompanyCheckItems
            .Where(x => x.IsChecked)
            .Select(x => x.Name)
            .Take(MaxComparedCompanyCount)
            .ToList();
        ReplaceCollection(SelectedCompanyNames, selectedNames);

        var nextValue = selectedNames.Count == 0
            ? AllCompanyOption
            : string.Join(",", selectedNames);
        if (!string.Equals(_selectedCompany, nextValue, StringComparison.Ordinal))
        {
            _selectedCompany = nextValue;
            RaisePropertyChanged(nameof(SelectedCompany));
            TriggerApplyFromFilterChange();
        }

        RaisePropertyChanged(nameof(SelectedCompanySummary));
        RaisePropertyChanged(nameof(SelectedCompanyCountText));
        RaisePropertyChanged(nameof(CurrentFilterSummary));
    }

    private void ApplyCompanySearchFilter()
    {
        var query = CompanySearchText?.Trim() ?? string.Empty;
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        foreach (var item in CompanyCheckItems)
        {
            item.IsVisible = !hasQuery
                             || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void ResetCompanySelectionToAll()
    {
        CompanySearchText = string.Empty;
        SelectedCompany = AllCompanyOption;
    }

    public MainWindowViewModel CreateChartPopupViewModel()
    {
        var popupViewModel = new MainWindowViewModel(_repository, _consolePipelineInsightService);
        popupViewModel.CopyPopupFilterStateFrom(this);
        return popupViewModel;
    }

    private void CopyPopupFilterStateFrom(MainWindowViewModel source)
    {
        _suppressAutoApply = true;
        try
        {
            SelectedPeriodPreset = source.SelectedPeriodPreset;
            StartDate = source.StartDate;
            EndDate = source.EndDate;
            SelectedRegion = source.SelectedRegion;
            SelectedDataType = source.SelectedDataType;
            SelectedCompany = source.SelectedCompany;
            SelectedChartPrecision = source.SelectedChartPrecision;
            SelectedMinAdjustRate = source.SelectedMinAdjustRate;
            SelectedMaxAdjustRate = source.SelectedMaxAdjustRate;
            KeywordText = source.KeywordText;
            OrderingAgencyText = source.OrderingAgencyText;
            CompanySearchText = source.CompanySearchText;
        }
        finally
        {
            _suppressAutoApply = false;
        }

        _pendingAutoApplyFilter = BuildFilter();
        if (ApplyFilterCommand.CanExecute(null))
        {
            ApplyFilterCommand.Execute(null);
        }
    }

    private static bool IsAllCompanyToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), "전체", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Trim(), LegacyAllCompanyOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Trim(), AllCompanyOption, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitCompanyTokens(string? companyText)
    {
        if (string.IsNullOrWhiteSpace(companyText))
        {
            return [];
        }

        return companyText
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsAllCompanyToken(x))
            .Where(x => !IsRetiredCompanyName(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxComparedCompanyCount)
            .ToList();
    }

    private static bool ContainsCompanyToken(string sourceCompany, string token)
    {
        var source = sourceCompany.Replace(" ", string.Empty, StringComparison.Ordinal);
        var target = token.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        return source.Contains(target, StringComparison.OrdinalIgnoreCase)
               || target.Contains(source, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetiredCompanyName(string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return false;
        }

        var normalized = companyName
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (var keyword in RetiredCompanyKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryApplyFromCompanyBaseline(DashboardFilter targetFilter)
    {
        if (_companyBaselineFilter is null || _companyBaselineParticipants.Count == 0)
        {
            return false;
        }

        if (!IsAllCompanyToken(_companyBaselineFilter.Company))
        {
            return false;
        }

        if (!AreFiltersEqualExceptCompany(_companyBaselineFilter, targetFilter))
        {
            return false;
        }

        if (_lastAppliedFilter is not null
            && string.Equals(_lastAppliedFilter.Company, targetFilter.Company, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tokens = SplitCompanyTokens(targetFilter.Company);
        var filtered = tokens.Count == 0
            ? _companyBaselineParticipants.ToList()
            : _companyBaselineParticipants
                .Where(x => tokens.Any(token =>
                    ContainsCompanyToken(x.CompanyName, token)
                    || ContainsCompanyToken(x.NormalizedCompanyName, token)))
                .ToList();
        filtered = filtered
            .Where(x => !IsRetiredCompanyName(x.CompanyName) && !IsRetiredCompanyName(x.NormalizedCompanyName))
            .ToList();

        var builder = new NaraApiSnapshotBuilder();
        var localSnapshot = builder.BuildSnapshot(
            filtered,
            targetFilter,
            $"{_companyBaselineDataSource} · 회사필터(로컬)");

        var companyOptions = _companyBaselineParticipants
            .Select(x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName : x.NormalizedCompanyName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsRetiredCompanyName(x))
            .Select(CompanyNamePresentation.Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var projectedSnapshot = new DashboardSnapshot(
            localSnapshot.Kpis,
            localSnapshot.NoticeRows,
            localSnapshot.DataSource,
            SavedFilePath,
            localSnapshot.PositionMapPath,
            localSnapshot.RecommendationPath,
            companyOptions,
            filtered);

        LoadStatusText = "경쟁업체 필터 적용 중(로컬)";
        ApplySnapshot(projectedSnapshot, targetFilter);
        LastSyncText = $"데이터소스: {projectedSnapshot.DataSource} · 마지막 동기화: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        LoadProgress = 100;
        LoadStatusText = $"완료(로컬필터) · 공고 {projectedSnapshot.NoticeRows.Count:N0}건";
        AppendEvent("경쟁업체 필터를 로컬 캐시로 즉시 반영했습니다.");
        return true;
    }

    private static bool AreFiltersEqualExceptCompany(DashboardFilter left, DashboardFilter right)
    {
        return left.StartDate == right.StartDate
               && left.EndDate == right.EndDate
               && string.Equals(left.Region, right.Region, StringComparison.Ordinal)
               && string.Equals(left.Keyword, right.Keyword, StringComparison.Ordinal)
               && string.Equals(left.OrderingAgency, right.OrderingAgency, StringComparison.Ordinal)
               && string.Equals(left.DataType, right.DataType, StringComparison.Ordinal)
               && Nullable.Equals(left.MinAdjustRate, right.MinAdjustRate)
               && Nullable.Equals(left.MaxAdjustRate, right.MaxAdjustRate);
    }

    private DashboardFilter BuildFilter()
    {
        return _filterState.BuildFilter(
            StartDate,
            EndDate,
            SelectedRegion,
            KeywordText,
            OrderingAgencyText,
            SelectedCompany,
            SelectedMinAdjustRate,
            SelectedMaxAdjustRate);
    }

    private void EnsureValidAdjustRange()
    {
        var normalized = _filterState.EnsureValidAdjustRange(SelectedMinAdjustRate, SelectedMaxAdjustRate);
        if (!normalized.WasSwapped)
        {
            return;
        }

        SelectedMinAdjustRate = normalized.MinText;
        SelectedMaxAdjustRate = normalized.MaxText;
    }

    private void ApplySnapshot(DashboardSnapshot snapshot, DashboardFilter appliedFilter)
    {
        IsApplyingSnapshot = true;
        try
        {
            ReplaceCollection(KpiCards, snapshot.Kpis);
            ReplaceCollection(NoticeItems, snapshot.NoticeRows);
            ReplaceCollection(IncrementalBidNoticeItems, snapshot.IncrementalBidNoticeRows ?? []);
            ReplaceCollection(IncrementalAwardNoticeItems, snapshot.IncrementalAwardNoticeRows ?? []);
            var visibleBidRows = ResolveVisibleBidNoticeRows(IncrementalBidNoticeItems, IncrementalAwardNoticeItems, snapshot.NoticeRows);
            ReplaceCollection(VisibleBidNoticeItems, visibleBidRows);
            IncrementalBidSummaryText = BuildBidNoticeSummary(IncrementalBidNoticeItems, visibleBidRows.Count);
            SavedFilePath = snapshot.SavedFilePath;
            var participants = (snapshot.Participants ?? Array.Empty<ParticipantRecord>())
                .Where(x =>
                    !IsRetiredCompanyName(x.CompanyName)
                    && !IsRetiredCompanyName(x.NormalizedCompanyName))
                .ToList();
            var donggukAwardRows = DonggukAdjustRateTrendBuilder.BuildAwardNoticeRows(participants, EndDate);
            ReplaceCollection(VisibleAwardNoticeItems, donggukAwardRows);
            IncrementalAwardSummaryText = BuildDonggukAwardNoticeSummary(donggukAwardRows);
            ReplaceCollection(NoticeAdjustRateTrendPoints, DonggukAdjustRateTrendBuilder.BuildPoints(participants, EndDate));
            ReplaceCollection(NoticeAwardReferenceTrendPoints, DonggukAdjustRateTrendBuilder.BuildAwardReferencePoints(participants, EndDate));
            NoticeAdjustRateTrendSummaryText = DonggukAdjustRateTrendBuilder.BuildSummary(participants, EndDate);
            SelectedBidNoticeItem = null;
            SelectedAwardNoticeItem = null;
            RefreshFirstPageBidRecommendation();
            CurrentParticipants = participants;
            PublishDonggukRankAlerts(participants);
            RefreshWholeMarketAnalytics(participants);
            RefreshConsolePipelineInsight(appliedFilter.Region);
            RefreshTacticalDashboard(participants);

            var currentCompany = string.IsNullOrWhiteSpace(SelectedCompany) ? AllCompanyOption : SelectedCompany;
            if (string.Equals(currentCompany, LegacyAllCompanyOption, StringComparison.Ordinal))
            {
                currentCompany = AllCompanyOption;
            }

            var baseOptions = snapshot.CompanyOptions is { Count: > 0 }
                ? snapshot.CompanyOptions
                : Array.Empty<string>();

            var resolvedCompanies = new List<string> { AllCompanyOption };
            resolvedCompanies.AddRange(
                baseOptions
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x)
                        && !IsRetiredCompanyName(x)
                        && !string.Equals(x, AllCompanyOption, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x, LegacyAllCompanyOption, StringComparison.OrdinalIgnoreCase))
                    .Select(CompanyNamePresentation.Canonicalize)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));

            ReplaceCollection(CompanyOptions, resolvedCompanies);
            ReplaceCompanyCheckItems(resolvedCompanies.Skip(1));
            AppendEvent($"경쟁업체 목록 로드: {Math.Max(0, resolvedCompanies.Count - 1):N0}개");

            var availableCompanies = resolvedCompanies
                .Skip(1)
                .ToList();
            var selectedTokens = SplitCompanyTokens(currentCompany);
            var restoredSelection = selectedTokens
                .Select(token => availableCompanies.FirstOrDefault(x => ContainsCompanyToken(x, token)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (IsAllCompanyToken(currentCompany) || restoredSelection.Count == 0)
            {
                SelectedCompany = AllCompanyOption;
            }
            else
            {
                SelectedCompany = string.Join(",", restoredSelection);
            }

            if (participants.Count == 0)
            {
                AppendEvent($"차트 데이터가 없습니다. (지역={SelectedRegion})");
            }

            _lastAppliedFilter = appliedFilter;
            if (IsAllCompanyToken(appliedFilter.Company))
            {
                _companyBaselineFilter = appliedFilter;
                _companyBaselineParticipants = participants.ToList();
                _companyBaselineDataSource = snapshot.DataSource;
            }
        }
        finally
        {
            IsApplyingSnapshot = false;
        }
    }

    private void RefreshWholeMarketAnalytics(IReadOnlyList<ParticipantRecord>? participants)
    {
        var source = participants?
            .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
            .Where(x => !IsRetiredCompanyName(x.CompanyName) && !IsRetiredCompanyName(x.NormalizedCompanyName))
            .ToList()
            ?? [];
        if (source.Count == 0)
        {
            ReplaceCollection(CompanyAggregateRows, []);
            ReplaceCollection(AdjustRateBinRows, []);
            WholeMarketSummaryText = "전체업체 분석 데이터가 없습니다.";
            return;
        }

        var companyGroups = source
            .GroupBy(
                x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName)
                    ? x.CompanyName.Trim()
                    : x.NormalizedCompanyName.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var withRate = g.Where(x => x.AdjustRate.HasValue).ToList();
                var awardCount = g.Count(x => x.IsAwarded || x.Rank == 1);
                var averageAdjustRate = withRate.Count == 0
                    ? 0.0
                    : withRate.Average(x => x.AdjustRate!.Value);
                var participation = g.Count();
                var awardRate = participation == 0 ? 0.0 : awardCount * 100.0 / participation;
                return new CompanyAggregateRowModel(
                    g.Key,
                    participation,
                    awardCount,
                    awardRate,
                    averageAdjustRate);
            })
            .OrderByDescending(x => x.ParticipationCount)
            .ThenByDescending(x => x.AwardCount)
            .ThenBy(x => x.CompanyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var topCompanies = companyGroups.Take(80).ToList();
        ReplaceCollection(CompanyAggregateRows, topCompanies);

        var minAdjust = _filterState.ParseAdjustRate(SelectedMinAdjustRate) ?? 99.0;
        var maxAdjust = _filterState.ParseAdjustRate(SelectedMaxAdjustRate) ?? 101.0;
        if (minAdjust > maxAdjust)
        {
            (minAdjust, maxAdjust) = (maxAdjust, minAdjust);
        }

        var rateRows = source
            .Where(x =>
                x.AdjustRate.HasValue
                && x.AdjustRate.Value >= minAdjust
                && x.AdjustRate.Value <= maxAdjust)
            .Select(x => x.AdjustRate!.Value)
            .ToList();

        if (rateRows.Count == 0)
        {
            ReplaceCollection(AdjustRateBinRows, []);
            WholeMarketSummaryText = $"전체 업체 {companyGroups.Count:N0}개 · 사정률 구간 내 표본 0건";
            return;
        }

        var binStart = Math.Floor(minAdjust * 10.0) / 10.0;
        var binEnd = Math.Ceiling(maxAdjust * 10.0) / 10.0;
        var binCount = (int)Math.Round((binEnd - binStart) * 10.0, MidpointRounding.AwayFromZero) + 1;
        if (binCount < 1)
        {
            binCount = 1;
        }

        var countsByBin = new int[binCount];
        var firstBinLowerBound = binStart - 0.05;
        foreach (var rate in rateRows)
        {
            var index = (int)Math.Floor(((rate - firstBinLowerBound) / 0.1) + 1e-9);
            if (index < 0 || index >= binCount)
            {
                continue;
            }

            countsByBin[index]++;
        }

        var bins = new List<AdjustRateBinRowModel>(binCount);
        for (var index = 0; index < binCount; index++)
        {
            var center = Math.Round(binStart + (index * 0.1), 1, MidpointRounding.AwayFromZero);
            var count = countsByBin[index];
            var share = count == 0 ? 0.0 : (count * 100.0 / rateRows.Count);
            bins.Add(new AdjustRateBinRowModel(center.ToString("F1"), count, share));
        }

        var topBins = bins
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.RangeLabel, StringComparer.Ordinal)
            .Take(30)
            .ToList();
        ReplaceCollection(AdjustRateBinRows, topBins);

        WholeMarketSummaryText = $"전체 업체 {companyGroups.Count:N0}개 (상위 {topCompanies.Count:N0}개 표시) · 사정률 표본 {rateRows.Count:N0}건";
    }

    private void RefreshTacticalDashboard(IReadOnlyList<ParticipantRecord>? participants)
    {
        var source = participants?
            .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
            .Where(x => !IsRetiredCompanyName(x.CompanyName) && !IsRetiredCompanyName(x.NormalizedCompanyName))
            .ToList()
            ?? [];

        var tacticalSource = FilterRecentThreeMonthParticipants(source);
        var tacticalInsight = GetAlignedTacticalInsight();
        if (tacticalSource.Count == 0)
        {
            TacticalHeroTitle = $"{SelectedRegion} 최근 3개월 밀착 전술판";
            TacticalHeroSummaryText = "최근 3개월 기준 참여업체 데이터가 없습니다.";
            TacticalPrimaryRecommendationText = tacticalInsight.RecommendationText;
            TacticalPrimaryAnchorText = "우측 앵커 데이터 없음";
            TacticalMarketStateText = "데이터 없음";
            TacticalDonggukPositionText = "동국 기준값 없음";
            TacticalBlockerHeadlineText = "방해업체 데이터 없음";
            TacticalEvidenceSummaryText = tacticalInsight.SummaryText;
            TacticalBattlefieldParticipants = Array.Empty<ParticipantRecord>();
            ReplaceCollection(TacticalBlockerRows, []);
            ReplaceCollection(TacticalSignalRows, []);
            ReplaceCollection(ThreatMetricRows, []);
            ReplaceCollection(OverlapMetricRows, []);
            ReplaceCollection(ProximityMetricRows, []);
            ReplaceCollection(DonggukTrendPoints, []);
            ReplaceCollection(DensityHistogramBins, []);
            ReplaceCollection(ZoneCompositionRows, []);
            ReplaceCollection(TacticalRightAnchorRows, []);
            ReplaceCollection(TacticalNoticeRows, []);
            return;
        }

        var donggukRates = tacticalSource
            .Where(IsDonggukParticipant)
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => x.AdjustRate!.Value)
            .ToList();
        var donggukCenter = CalculateMedian(donggukRates);
        TacticalDonggukPositionText = donggukCenter.HasValue
            ? $"동국 중심 {donggukCenter.Value.ToString("F5", CultureInfo.InvariantCulture)}%"
            : "동국 기준값 부족";

        var blockerRows = BuildTacticalBlockerRows(tacticalSource, donggukCenter)
            .Take(8)
            .ToList();
        ReplaceCollection(TacticalBlockerRows, blockerRows);
        TacticalBattlefieldParticipants = BuildTacticalBattlefieldParticipants(tacticalSource, blockerRows);

        var topBlocker = blockerRows.FirstOrDefault();
        TacticalBlockerHeadlineText = topBlocker is null
            ? "근접 방해업체 없음"
            : $"{topBlocker.CompanyName} · {topBlocker.ThreatLabel} · Δ {topBlocker.RelativeRateText}%";

        var insight = tacticalInsight;
        TacticalHeroTitle = $"{SelectedRegion} 최근 3개월 밀착 전술판";
        TacticalPrimaryRecommendationText = insight.IsAvailable
            ? insight.RecommendationText
            : "최근 3개월 기준 추천 밴드를 아직 고정할 수 없습니다.";
        TacticalPrimaryAnchorText = !string.IsNullOrWhiteSpace(insight.PrimaryAnchorCompany)
            ? $"우측 앵커 {insight.PrimaryAnchorCompany}"
            : topBlocker is not null
                ? $"근접 방해 {topBlocker.CompanyName}"
                : "우측 앵커 데이터 없음";
        TacticalMarketStateText = DetermineTacticalMarketState(insight, topBlocker);
        TacticalHeroSummaryText = BuildTacticalHeroSummary(insight, donggukCenter, topBlocker, tacticalSource.Count, donggukRates.Count);
        TacticalEvidenceSummaryText = insight.IsAvailable
            ? $"{insight.ShadowText} · {insight.CollisionText}"
            : $"{WholeMarketSummaryText} · 동국 표본 {donggukRates.Count:N0}건";

        ReplaceCollection(
            TacticalSignalRows,
            BuildTacticalSignalRows(insight, donggukCenter, topBlocker));
        ReplaceCollection(ThreatMetricRows, BuildThreatMetricRows(blockerRows));
        ReplaceCollection(OverlapMetricRows, BuildOverlapMetricRows(blockerRows));
        ReplaceCollection(ProximityMetricRows, BuildProximityMetricRows(blockerRows));
        ReplaceCollection(DonggukTrendPoints, BuildDonggukTrendPoints(tacticalSource));
        ReplaceCollection(DensityHistogramBins, BuildDensityHistogramBins(tacticalSource));
        ReplaceCollection(ZoneCompositionRows, BuildZoneCompositionRows(insight));
        ReplaceCollection(TacticalRightAnchorRows, BuildRightAnchorRows(blockerRows));
        ReplaceCollection(TacticalNoticeRows, BuildTacticalNoticeRows(tacticalSource, blockerRows));
    }

    private IReadOnlyList<TacticalCompanyRowModel> BuildTacticalBlockerRows(
        IReadOnlyList<ParticipantRecord> participants,
        double? donggukCenter)
    {
        var donggukNoticeSet = participants
            .Where(IsDonggukParticipant)
            .Select(x => x.NoticeNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (donggukNoticeSet.Count == 0)
        {
            return [];
        }

        var groups = participants
            .Where(x => !IsDonggukParticipant(x))
            .GroupBy(
                x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName)
                    ? CompanyNamePresentation.Canonicalize(x.CompanyName)
                    : CompanyNamePresentation.Canonicalize(x.NormalizedCompanyName),
                StringComparer.OrdinalIgnoreCase);

        var rows = groups
            .Select(g =>
            {
                var rates = g.Where(x => x.AdjustRate.HasValue).Select(x => x.AdjustRate!.Value).ToList();
                var center = CalculateMedian(rates);
                var overlapCount = g
                    .Select(x => x.NoticeNumber)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(donggukNoticeSet.Contains);
                var delta = donggukCenter.HasValue && center.HasValue
                    ? center.Value - donggukCenter.Value
                    : (double?)null;
                var proximity = delta.HasValue ? Math.Abs(delta.Value) : 9.999;
                var threatScore = (Math.Min(overlapCount, 20) * 6.0)
                                  + Math.Max(0.0, 0.080 - proximity) * 1000.0
                                  + Math.Min(rates.Count, 20);
                if (delta.HasValue && delta.Value >= 0.0 && delta.Value <= 0.030)
                {
                    threatScore += 8.0;
                }

                return new
                {
                    CompanyName = g.Key,
                    Delta = delta,
                    OverlapCount = overlapCount,
                    SampleCount = rates.Count,
                    ThreatScore = threatScore,
                    ThreatLabel = BuildThreatLabel(delta, overlapCount, rates.Count),
                };
            })
            .Where(x => x.OverlapCount > 0 || x.SampleCount >= 3)
            .OrderByDescending(x => x.ThreatScore)
            .ThenBy(x => x.Delta.HasValue ? Math.Abs(x.Delta.Value) : double.MaxValue)
            .ThenByDescending(x => x.OverlapCount)
            .Take(12)
            .Select(x => new TacticalCompanyRowModel(
                x.CompanyName,
                x.Delta.HasValue
                    ? x.Delta.Value.ToString("+0.00000;-0.00000;0.00000", CultureInfo.InvariantCulture)
                    : "-",
                $"{x.OverlapCount:N0}건",
                $"{x.SampleCount:N0}",
                x.ThreatLabel,
                x.ThreatScore.ToString("0", CultureInfo.InvariantCulture)))
            .ToList();

        return rows;
    }

    private IReadOnlyList<TacticalSignalRowModel> BuildTacticalSignalRows(
        ConsolePipelineInsight insight,
        double? donggukCenter,
        TacticalCompanyRowModel? topBlocker)
    {
        var zoneText = insight.ForbiddenZoneCount.HasValue || insight.BoundaryZoneCount.HasValue || insight.CandidateBandCount.HasValue
            ? $"금지 {insight.ForbiddenZoneCount ?? 0:N0} · 경계 {insight.BoundaryZoneCount ?? 0:N0} · 후보 {insight.CandidateBandCount ?? 0:N0}"
            : "조건부우위 집계 없음";

        return new List<TacticalSignalRowModel>
        {
            new(
                "현재 후보",
                string.IsNullOrWhiteSpace(insight.RecommendationBandText) ? TacticalPrimaryRecommendationText : insight.RecommendationBandText,
                string.IsNullOrWhiteSpace(insight.RecommendationUncertainty) ? TacticalPrimaryAnchorText : $"불확실성 {insight.RecommendationUncertainty}"),
            new(
                "조건부우위",
                zoneText,
                insight.ShadowText),
            new(
                "동국 중심",
                donggukCenter.HasValue ? donggukCenter.Value.ToString("F5", CultureInfo.InvariantCulture) + "%" : "데이터 부족",
                topBlocker is null ? "근접 방해업체 없음" : $"1위 {topBlocker.CompanyName} · {topBlocker.ThreatLabel}"),
            new(
                "판단 포인터",
                InteractivePointerRateText,
                InteractivePointerHintText),
        };
    }

    private IReadOnlyList<ParticipantRecord> FilterRecentThreeMonthParticipants(IReadOnlyList<ParticipantRecord> participants)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var endDate = EndDate == default ? DateTime.Today : EndDate.Date;
        var startBoundary = endDate.AddMonths(-3).Date;
        return participants
            .Where(x => x.OpenDate.Date >= startBoundary && x.OpenDate.Date <= endDate)
            .ToList();
    }

    private IReadOnlyList<ParticipantRecord> BuildTacticalBattlefieldParticipants(
        IReadOnlyList<ParticipantRecord> participants,
        IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        var targetCompanies = blockerRows
            .Select(x => CompanyNamePresentation.Canonicalize(x.CompanyName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        targetCompanies.Add(DonggukCompanyName);

        return participants
            .Where(x =>
            {
                var normalized = string.IsNullOrWhiteSpace(x.NormalizedCompanyName)
                    ? CompanyNamePresentation.Canonicalize(x.CompanyName)
                    : CompanyNamePresentation.Canonicalize(x.NormalizedCompanyName);
                return targetCompanies.Contains(normalized);
            })
            .ToList();
    }

    private IReadOnlyList<TacticalCompanyRowModel> BuildRightAnchorRows(IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        return blockerRows
            .Select(x => new { Row = x, Delta = ParseSignedDoubleText(x.RelativeRateText) })
            .Where(x => x.Delta.HasValue && x.Delta.Value >= 0.0)
            .OrderBy(x => x.Delta!.Value)
            .ThenByDescending(x => ParseNumericText(x.Row.OverlapText))
            .Take(8)
            .Select(x => x.Row)
            .ToList();
    }

    private IReadOnlyList<TacticalNoticeRowModel> BuildTacticalNoticeRows(
        IReadOnlyList<ParticipantRecord> participants,
        IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        if (participants.Count == 0 || blockerRows.Count == 0)
        {
            return [];
        }

        var blockerSet = blockerRows
            .Select(x => CompanyNamePresentation.Canonicalize(x.CompanyName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return participants
            .GroupBy(x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber)
            .Select(group =>
            {
                var rows = group.ToList();
                var donggukRow = rows
                    .Where(IsDonggukParticipant)
                    .Where(x => x.AdjustRate.HasValue)
                    .OrderByDescending(x => x.OpenDate)
                    .FirstOrDefault();
                if (donggukRow is null)
                {
                    return null;
                }

                var closeCompanies = rows
                    .Where(x => !IsDonggukParticipant(x))
                    .Select(x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName)
                        ? CompanyNamePresentation.Canonicalize(x.CompanyName)
                        : CompanyNamePresentation.Canonicalize(x.NormalizedCompanyName))
                    .Where(x => blockerSet.Contains(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (closeCompanies.Count == 0)
                {
                    return null;
                }

                var closeLabel = string.Join(", ", closeCompanies.Take(3));
                if (closeCompanies.Count > 3)
                {
                    closeLabel += $" 외 {closeCompanies.Count - 3:N0}";
                }

                return new TacticalNoticeRowModel(
                    donggukRow.OpenDate,
                    string.IsNullOrWhiteSpace(donggukRow.NoticeNumber) ? donggukRow.NoticeNo : donggukRow.NoticeNumber,
                    donggukRow.NoticeName,
                    closeLabel,
                    closeCompanies.Count,
                    donggukRow.AdjustRate?.ToString("F5", CultureInfo.InvariantCulture) ?? "-");
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.OpenDate)
            .Take(20)
            .ToList();
    }

    private IReadOnlyList<MetricBarRowModel> BuildThreatMetricRows(IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        return blockerRows
            .Take(6)
            .Select(x => new MetricBarRowModel(
                CompanyNamePresentation.BuildShortLabel(x.CompanyName),
                x.CompanyName,
                ParseNumericText(x.ThreatScoreText),
                x.ThreatScoreText,
                x.ThreatLabel))
            .ToList();
    }

    private IReadOnlyList<MetricBarRowModel> BuildOverlapMetricRows(IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        return blockerRows
            .Take(6)
            .Select(x => new MetricBarRowModel(
                CompanyNamePresentation.BuildShortLabel(x.CompanyName),
                x.CompanyName,
                ParseNumericText(x.OverlapText),
                x.OverlapText,
                x.ThreatLabel))
            .ToList();
    }

    private IReadOnlyList<MetricBarRowModel> BuildProximityMetricRows(IReadOnlyList<TacticalCompanyRowModel> blockerRows)
    {
        var rightSideRows = blockerRows
            .Select(x => new { Row = x, Delta = ParseSignedDoubleText(x.RelativeRateText) })
            .Where(x => x.Delta.HasValue && x.Delta.Value >= 0.0)
            .OrderBy(x => x.Delta!.Value)
            .Select(x => x.Row)
            .ToList();
        var sourceRows = rightSideRows.Count > 0 ? rightSideRows : blockerRows.ToList();

        return sourceRows
            .Take(6)
            .Select(x =>
            {
                var delta = ParseSignedDoubleText(x.RelativeRateText);
                var proximity = delta.HasValue ? Math.Max(0.0, 0.080 - Math.Abs(delta.Value)) * 1000.0 : 0.0;
                return new MetricBarRowModel(
                    CompanyNamePresentation.BuildShortLabel(x.CompanyName),
                    x.CompanyName,
                    proximity,
                    x.RelativeRateText,
                    x.ThreatLabel);
            })
            .ToList();
    }

    private IReadOnlyList<TrendPointModel> BuildDonggukTrendPoints(IReadOnlyList<ParticipantRecord> participants)
    {
        return participants
            .Where(IsDonggukParticipant)
            .Where(x => x.AdjustRate.HasValue)
            .GroupBy(x => new DateTime(x.OpenDate.Year, x.OpenDate.Month, 1))
            .OrderBy(x => x.Key)
            .TakeLast(6)
            .Select(g => new TrendPointModel(
                g.Key.ToString("MM", CultureInfo.InvariantCulture),
                CalculateMedian(g.Select(x => x.AdjustRate!.Value).ToList()) ?? 0.0))
            .ToList();
    }

    private IReadOnlyList<HistogramBinModel> BuildDensityHistogramBins(IReadOnlyList<ParticipantRecord> participants)
    {
        var minRate = _filterState.ParseAdjustRate(SelectedMinAdjustRate) ?? 99.5;
        var maxRate = _filterState.ParseAdjustRate(SelectedMaxAdjustRate) ?? 100.0;
        if (minRate > maxRate)
        {
            (minRate, maxRate) = (maxRate, minRate);
        }

        var rates = participants
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => x.AdjustRate!.Value)
            .Where(x => x >= minRate && x <= maxRate)
            .ToList();
        if (rates.Count == 0)
        {
            return [];
        }

        const int binCount = 18;
        var step = (maxRate - minRate) / binCount;
        if (step <= 0)
        {
            step = 0.01;
        }

        var bins = Enumerable.Range(0, binCount)
            .Select(index =>
            {
                var start = minRate + (step * index);
                var end = index == binCount - 1 ? maxRate : start + step;
                var count = rates.Count(x => x >= start && (index == binCount - 1 ? x <= end : x < end));
                return new HistogramBinModel(
                    $"{start:F3}",
                    start,
                    end,
                    count,
                    count.ToString("N0", CultureInfo.InvariantCulture));
            })
            .ToList();

        return bins;
    }

    private IReadOnlyList<ZoneSegmentModel> BuildZoneCompositionRows(ConsolePipelineInsight insight)
    {
        var rows = new List<ZoneSegmentModel>();
        if (insight.ForbiddenZoneCount.HasValue)
        {
            rows.Add(new ZoneSegmentModel("금지", insight.ForbiddenZoneCount.Value, insight.ForbiddenZoneCount.Value.ToString("N0", CultureInfo.InvariantCulture)));
        }

        if (insight.BoundaryZoneCount.HasValue)
        {
            rows.Add(new ZoneSegmentModel("경계", insight.BoundaryZoneCount.Value, insight.BoundaryZoneCount.Value.ToString("N0", CultureInfo.InvariantCulture)));
        }

        if (insight.CandidateBandCount.HasValue)
        {
            rows.Add(new ZoneSegmentModel("후보", insight.CandidateBandCount.Value, insight.CandidateBandCount.Value.ToString("N0", CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private string DetermineTacticalMarketState(ConsolePipelineInsight insight, TacticalCompanyRowModel? topBlocker)
    {
        if (!insight.IsAvailable)
        {
            return "콘솔 분석 대기";
        }

        if ((insight.ForbiddenZoneCount ?? 0) >= 8 || (insight.BoundaryZoneCount ?? 0) >= 450)
        {
            return "압박 구간 우세";
        }

        if (topBlocker is not null && (topBlocker.ThreatLabel.Contains("우측", StringComparison.Ordinal) || topBlocker.ThreatLabel.Contains("직접", StringComparison.Ordinal)))
        {
            return "우측 방해 주의";
        }

        if ((insight.CandidateBandCount ?? 0) >= 3)
        {
            return "후보 밴드 확보";
        }

        return "관찰 필요";
    }

    private string BuildTacticalHeroSummary(
        ConsolePipelineInsight insight,
        double? donggukCenter,
        TacticalCompanyRowModel? topBlocker,
        int participantCount,
        int donggukCount)
    {
        var centerText = donggukCenter.HasValue
            ? donggukCenter.Value.ToString("F5", CultureInfo.InvariantCulture) + "%"
            : "데이터 부족";
        var blockerText = topBlocker is null
            ? "근접 방해업체 없음"
            : $"{topBlocker.CompanyName} ({topBlocker.ThreatLabel}, Δ {topBlocker.RelativeRateText}%)";
        var candidateText = insight.CandidateBandCount.HasValue
            ? $"후보 {insight.CandidateBandCount.Value:N0}개"
            : "후보 집계 없음";
        return $"동국 중심 {centerText} · 동국 표본 {donggukCount:N0}건 / 전체 {participantCount:N0}건 · {candidateText} · {blockerText}";
    }

    private static string BuildThreatLabel(double? delta, int overlapCount, int sampleCount)
    {
        if (!delta.HasValue)
        {
            return sampleCount >= 3 ? "표본 감시" : "표본 부족";
        }

        var distance = Math.Abs(delta.Value);
        if (distance <= 0.010 && overlapCount >= 2)
        {
            return "직접 방해";
        }

        if (delta.Value >= 0.0 && distance <= 0.030)
        {
            return "우측 방해";
        }

        if (delta.Value < 0.0 && distance <= 0.030)
        {
            return "좌측 충돌";
        }

        if (distance <= 0.060)
        {
            return "근접 감시";
        }

        return sampleCount >= 5 ? "외곽 감시" : "표본 감시";
    }

    private static double? CalculateMedian(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var ordered = values.OrderBy(x => x).ToList();
        var middle = ordered.Count / 2;
        if (ordered.Count % 2 == 1)
        {
            return ordered[middle];
        }

        return (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static bool IsDonggukParticipant(ParticipantRecord record)
    {
        var normalized = string.IsNullOrWhiteSpace(record.NormalizedCompanyName)
            ? CompanyNamePresentation.Canonicalize(record.CompanyName)
            : CompanyNamePresentation.Canonicalize(record.NormalizedCompanyName);
        return ContainsCompanyToken(normalized, DonggukCompanyName);
    }

    private static double ParseNumericText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        var digits = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
    }

    private static double? ParseSignedDoubleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static List<CollectorNoticeRowModel> ResolveVisibleBidNoticeRows(
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalRows,
        IReadOnlyCollection<CollectorNoticeRowModel> incrementalAwardRows,
        IReadOnlyList<NoticeRowModel> snapshotRows)
    {
        return CollectorNoticeRules.CreateVisibleBidRows(incrementalRows, incrementalAwardRows, snapshotRows, DateTime.Now);
    }

    private static string BuildBidNoticeSummary(IReadOnlyCollection<CollectorNoticeRowModel> rows, int visibleCount)
    {
        if (rows.Count == 0)
        {
            return visibleCount > 0
                ? $"오늘 이후 입찰공고 없음 · 팝업 공고 {visibleCount:N0}건 표시"
                : "오늘 이후 입찰공고 0건";
        }

        var nearest = rows
            .OrderBy(x => x.NoticeDate)
            .First();
        return visibleCount < rows.Count
            ? $"오늘 이후 입찰공고 표시 {visibleCount:N0}건 · 원본 {rows.Count:N0}건 · 가장 빠름 {nearest.NoticeDate:MM-dd HH:mm}"
            : $"오늘 이후 입찰공고 {rows.Count:N0}건 · 가장 빠름 {nearest.NoticeDate:MM-dd HH:mm}";
    }

    private static string BuildDonggukAwardNoticeSummary(IReadOnlyCollection<CollectorNoticeRowModel> rows)
    {
        if (rows.Count == 0)
        {
            return "동국 연동 낙찰공고 0건";
        }

        var latest = rows
            .OrderByDescending(x => x.NoticeDate)
            .First();
        return $"동국 연동 낙찰공고 {rows.Count:N0}건 · 최신 {latest.NoticeDate:MM-dd HH:mm}";
    }

    private void RefreshFirstPageBidRecommendation()
    {
        var targetBid = SelectedBidNoticeItem
            ?? VisibleBidNoticeItems.OrderBy(x => x.NoticeDate).FirstOrDefault();

        if (targetBid is null)
        {
            NextBidRecommendationText = "다음 입찰공고 없음";
            BidRecommendationReasonText = "표시할 입찰공고가 없습니다.";
            return;
        }

        var targetLabel = SelectedBidNoticeItem is null ? "다음 입찰공고" : $"선택 공고 {targetBid.NoticeNumber}";
        var regionLabel = NormalizeBidRegionExplanation(targetBid.RegionText);

        if (RecommendationBandMin.HasValue && RecommendationBandMax.HasValue)
        {
            var bandMin = Math.Min(RecommendationBandMin.Value, RecommendationBandMax.Value);
            var bandMax = Math.Max(RecommendationBandMin.Value, RecommendationBandMax.Value);
            NextBidRecommendationText = $"{targetLabel} 추천자리 {bandMin:F5}% ~ {bandMax:F5}% · {targetBid.NoticeDate:MM-dd HH:mm}";
            BidRecommendationReasonText = $"{ConsolePipelineRecommendationReasonText} · 지역 {regionLabel}";
            return;
        }

        NextBidRecommendationText = $"{targetLabel} 추천자리 없음 · {targetBid.NoticeDate:MM-dd HH:mm}";
        BidRecommendationReasonText = $"근거 부족 또는 stale 분석 · 지역 {regionLabel}";
    }

    private static string NormalizeBidRegionExplanation(string? regionText)
    {
        if (string.IsNullOrWhiteSpace(regionText))
        {
            return "미상(전국 아님)";
        }

        var normalized = regionText.Trim();
        return string.Equals(normalized, "미상", StringComparison.OrdinalIgnoreCase)
            ? "미상(전국 아님)"
            : normalized;
    }

    public void UpdateHoveredRate(double? hoveredRate)
    {
        if (!hoveredRate.HasValue)
        {
            InteractivePointerRateText = "차트 위에서 마우스를 움직이면 현재 사정률이 표시됩니다.";
            InteractivePointerHintText = "후보 밴드 내부/외부 여부와 거리를 실시간으로 확인할 수 있습니다.";
            RefreshPointerSignalRow();
            return;
        }

        var rate = hoveredRate.Value;
        InteractivePointerRateText = $"현재 커서 {rate:F3}%";

        var tacticalInsight = GetAlignedTacticalInsight();
        if (!tacticalInsight.IsAvailable)
        {
            InteractivePointerHintText = "콘솔 분석 결과가 아직 없어 후보 밴드 비교를 할 수 없습니다.";
            RefreshPointerSignalRow();
            return;
        }

        var bandMin = tacticalInsight.RecommendationBandMin;
        var bandMax = tacticalInsight.RecommendationBandMax;
        if (!bandMin.HasValue || !bandMax.HasValue)
        {
            InteractivePointerHintText = "구조화된 추천 밴드 정보가 없습니다.";
            RefreshPointerSignalRow();
            return;
        }

        if (rate >= bandMin.Value && rate <= bandMax.Value)
        {
            InteractivePointerHintText = $"후보 밴드 내부 · {bandMin.Value:F3}%~{bandMax.Value:F3}%";
            RefreshPointerSignalRow();
            return;
        }

        if (rate < bandMin.Value)
        {
            InteractivePointerHintText = $"후보 밴드보다 {(bandMin.Value - rate):F3}% 낮습니다.";
            RefreshPointerSignalRow();
            return;
        }

        InteractivePointerHintText = $"후보 밴드보다 {(rate - bandMax.Value):F3}% 높습니다.";
        RefreshPointerSignalRow();
    }

    private ConsolePipelineInsight GetAlignedTacticalInsight()
    {
        if (!IsConsoleInsightAlignedToCurrentSlice(_latestConsolePipelineInsight))
        {
            return ConsolePipelineInsight.Empty(SelectedRegion, "slice_mismatch", "현재 화면 필터와 일치하는 콘솔 분석 결과가 없습니다.");
        }

        return _latestConsolePipelineInsight;
    }

    private bool IsConsoleInsightAlignedToCurrentSlice(ConsolePipelineInsight insight)
    {
        if (!insight.IsAvailable)
        {
            return false;
        }

        if (!string.Equals(insight.Region, SelectedRegion, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(SelectedDataType, "낙찰", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(SelectedPeriodPreset, "최근 3개월", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(insight.AnalysisPeriod, "3m", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAllCompanyToken(SelectedCompany)
            || !string.IsNullOrWhiteSpace(KeywordText)
            || !string.IsNullOrWhiteSpace(OrderingAgencyText))
        {
            return false;
        }

        var selectedMin = _filterState.ParseAdjustRate(SelectedMinAdjustRate);
        var selectedMax = _filterState.ParseAdjustRate(SelectedMaxAdjustRate);
        if (!selectedMin.HasValue || !selectedMax.HasValue)
        {
            return false;
        }

        return insight.AnalysisMinRate.HasValue
               && insight.AnalysisMaxRate.HasValue
               && Math.Abs(insight.AnalysisMinRate.Value - selectedMin.Value) <= 0.0005
               && Math.Abs(insight.AnalysisMaxRate.Value - selectedMax.Value) <= 0.0005;
    }

    private void RefreshPointerSignalRow()
    {
        if (TacticalSignalRows.Count == 0)
        {
            return;
        }

        var updated = false;
        for (var index = 0; index < TacticalSignalRows.Count; index++)
        {
            if (!string.Equals(TacticalSignalRows[index].Label, "판단 포인터", StringComparison.Ordinal))
            {
                continue;
            }

            TacticalSignalRows[index] = new TacticalSignalRowModel(
                "판단 포인터",
                InteractivePointerRateText,
                InteractivePointerHintText);
            updated = true;
            break;
        }

        if (!updated)
        {
            TacticalSignalRows.Add(new TacticalSignalRowModel(
                "판단 포인터",
                InteractivePointerRateText,
                InteractivePointerHintText));
        }
    }

    private void StopLoading()
    {
        if (!IsLoading || _loadCts is null || _loadCts.IsCancellationRequested)
        {
            return;
        }

        _loadCts.Cancel();
        LoadStatusText = "중단 요청 중...";
        AppendEvent("중단 요청");
    }

    private void AppendEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        LoadEvents.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
        while (LoadEvents.Count > 120)
        {
            LoadEvents.RemoveAt(LoadEvents.Count - 1);
        }
    }

    private void RefreshConsolePipelineInsight(string region)
    {
        try
        {
            var insight = _consolePipelineInsightService.Load(region);
            _latestConsolePipelineInsight = insight;
            ConsolePipelineStatusText = insight.StatusText;
            ConsolePipelineSummaryText = insight.SummaryText;
            ConsolePipelineCrowdingText = insight.CrowdingText;
            ConsolePipelineCollisionText = insight.CollisionText;
            ConsolePipelineShadowText = insight.ShadowText;
            ConsolePipelineRecommendationText = insight.RecommendationText;
            ConsolePipelineRecommendationReasonText = insight.RecommendationReasonText;
            RaisePropertyChanged(nameof(RecommendationBandMin));
            RaisePropertyChanged(nameof(RecommendationBandMax));
            RefreshFirstPageBidRecommendation();
        }
        catch (Exception ex)
        {
            _latestConsolePipelineInsight = ConsolePipelineInsight.Empty(region, "error", $"{region} 콘솔 분석 로드 실패");
            ConsolePipelineStatusText = $"{region} 콘솔 분석 로드 실패";
            ConsolePipelineSummaryText = ex.Message;
            ConsolePipelineCrowdingText = "군집 분석 결과 없음";
            ConsolePipelineCollisionText = "충돌 분석 결과 없음";
            ConsolePipelineShadowText = "전략 분석 결과 없음";
            ConsolePipelineRecommendationText = "추천 밴드 없음";
            ConsolePipelineRecommendationReasonText = "분석 파일을 읽는 중 오류가 발생했습니다.";
            RaisePropertyChanged(nameof(RecommendationBandMin));
            RaisePropertyChanged(nameof(RecommendationBandMax));
            RefreshFirstPageBidRecommendation();
            AppendEvent($"콘솔 분석 로드 실패: {ex.Message}");
        }
    }

    private void PublishDonggukRankAlerts(IReadOnlyList<ParticipantRecord> participants)
    {
        var alerts = _alertPublisher.Publish(participants, AppendEvent);
        if (alerts.Count == 0)
        {
            return;
        }

        RankAlertsDetected?.Invoke(this, alerts);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    private void RaisePropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CompanyCheckItem : INotifyPropertyChanged
{
    private bool _isChecked;
    private bool _isVisible;

    public CompanyCheckItem(string name, string displayName, bool isChecked = false, bool isVisible = true)
    {
        Name = name;
        DisplayName = displayName;
        _isChecked = isChecked;
        _isVisible = isVisible;
    }

    public string Name { get; }
    public string DisplayName { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
