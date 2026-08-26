using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;

namespace NaraCollector.Desktop.Controls;

public enum NativeBidChartMode
{
    Scatter,
    Trend,
    Distribution,
}

public sealed class CompanyPickedEventArgs : EventArgs
{
    public CompanyPickedEventArgs(string companyName)
    {
        CompanyName = companyName;
    }

    public string CompanyName { get; }
}

public sealed class RateRangeSelectedEventArgs : EventArgs
{
    public RateRangeSelectedEventArgs(double minRate, double maxRate)
    {
        MinRate = minRate;
        MaxRate = maxRate;
    }

    public double MinRate { get; }
    public double MaxRate { get; }
}

public sealed class HoveredRateChangedEventArgs : EventArgs
{
    public HoveredRateChangedEventArgs(double? rate)
    {
        Rate = rate;
    }

    public double? Rate { get; }
}

public sealed class NativeBidChartControl : FrameworkElement
{
    private enum ChartInteractionState
    {
        None,
        Panning,
        Selecting,
    }

    private const string AllCompanyOption = "전체경쟁업체";
    private const string LegacyAllCompanyOption = "전체업체";
    private const double DragActivationThreshold = 4.0;
    private static readonly Color DonggukFillColor = Color.FromRgb(0xD3, 0x2F, 0x2F);
    private static readonly Lazy<CompetitionAnalysisProfile> AnalysisProfile = new(() => CompetitionAnalysisProfileProvider.Load());
    private static readonly Lazy<CompetitionRecommendationEngine> RecommendationEngine = new(() => new CompetitionRecommendationEngine(AnalysisProfile.Value));
    private static readonly Lazy<IReadOnlyDictionary<string, Color>> FixedCompanyColors = new(LoadFixedCompanyColors);
    private static readonly object RenderErrorLogSync = new();
    private static readonly Pen GridPen = CreatePen("#D9DEE7", 1.0);
    private static readonly Pen MajorGridPen = CreatePen("#BFC7D4", 1.0);
    private static readonly Pen AxisPen = CreatePen("#5E6A7D", 1.2);
    private static readonly Pen SelectionPen = CreatePen("#FFB84A", 1.4);
    private static readonly Pen BlockBorderPen = CreatePen("#21252B", 0.8);
    private static readonly Pen RankHighlightPen = CreatePen("#FACC15", 2.3);
    private static readonly Pen RankHighlightOuterPen = CreatePen("#CA8A04", 3.2);
    private static readonly Pen HoverHighlightPen = CreatePen("#F59E0B", 1.8);
    private static readonly Pen RecommendationBandPen = CreatePen("#FDE68A", 0.6);
    private static readonly Pen RecommendationArrowPen = CreatePen("#EA580C", 2.0);
    private static readonly Pen RecommendationArrowGlowPen = CreatePen("#FDBA74", 4.0);
    private static readonly Pen RecommendationGuidePen = CreateDashedPen("#FB923C", 1.4, 4.0, 2.6);
    private static readonly Pen RecommendationGuideStrongPen = CreateDashedPen("#EA580C", 2.0, 5.0, 2.2);
    private static readonly Pen RecommendationCalloutPen = CreatePen("#FDBA74", 1.2);
    private static readonly Pen RecommendationCalloutHoverPen = CreatePen("#EA580C", 1.8);
    private static readonly Pen RecommendationTargetRingPen = CreatePen("#C2410C", 2.0);
    private static readonly Pen RecommendationTargetGlowPen = CreatePen("#FDBA74", 4.2);
    private static readonly Pen PointerGuidePen = CreateDashedPen("#2563EB", 1.2, 4.0, 3.0);
    private static readonly Pen DistributionCenterPen = CreatePen("#F59E0B", 1.6);
    private static readonly Pen DistributionStackBorderPen = CreatePen("#D8E2F2", 0.8);
    private static readonly Brush BackgroundBrush = CreateBrush("#F6F8FB");
    private static readonly Brush PlotBackgroundBrush = CreateBrush("#FFFFFF");
    private static readonly Brush RecommendationBandBrush = CreateBrush("#2AFDE68A");
    private static readonly Brush RecommendationArrowBrush = CreateBrush("#F97316");
    private static readonly Brush RecommendationCalloutBrush = CreateBrush("#FFF7ED");
    private static readonly Brush RecommendationCalloutHoverBrush = CreateBrush("#FFEDD5");
    private static readonly Brush RecommendationPrimaryTextBrush = CreateBrush("#9A3412");
    private static readonly Brush RecommendationTargetFillBrush = CreateBrush("#FFF3E0");
    private static readonly Brush PointerLabelBrush = CreateBrush("#EFF6FF");
    private static readonly Brush PointerLabelTextBrush = CreateBrush("#1D4ED8");
    private static readonly Brush DistributionMajorBarBrush = CreateBrush("#6EA8FE");
    private static readonly Brush DistributionMinorBarBrush = CreateBrush("#9CC4FF");
    private static readonly Brush TextBrush = CreateBrush("#111111");
    private static readonly Brush SubtleTextBrush = CreateBrush("#5A6473");
    private static readonly Brush RankBadgeBrush = CreateBrush("#C62828");
    private static readonly Brush RankBadgeTextBrush = Brushes.White;
    private static readonly Pen ChipLabelOutlinePen = CreateRoundPen("#59000000", 0.4);
    private static readonly Pen TrendMarkerPen = CreatePen("#FFFFFF", 1.1);

    public static readonly DependencyProperty PinnedRecommendationBandMinProperty = DependencyProperty.Register(
        nameof(PinnedRecommendationBandMin),
        typeof(double?),
        typeof(NativeBidChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PinnedRecommendationBandMaxProperty = DependencyProperty.Register(
        nameof(PinnedRecommendationBandMax),
        typeof(double?),
        typeof(NativeBidChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PinnedRecommendationTextProperty = DependencyProperty.Register(
        nameof(PinnedRecommendationText),
        typeof(string),
        typeof(NativeBidChartControl),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface ScatterLabelTypeface = new(
        new FontFamily("Malgun Gothic"),
        FontStyles.Normal,
        FontWeights.Bold,
        FontStretches.Normal);

    private readonly Dictionary<(int RowIndex, int ColumnIndex), List<ParticipantRecord>> _cellLookup = [];
    private readonly Dictionary<string, Color> _companyColorCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SolidColorBrush> _companyBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Text, int SizeBucket, int DpiBucket), FormattedText> _shortLabelCache = [];
    private DrawingGroup? _chipLayerCache;
    private int _chipLayerCacheVersion = -1;
    private double _chipLayerCellSize;
    private ImageSource? _viewLayer;
    private ScatterViewCacheKey _viewLayerKey;
    private double? _lastRaisedHoverRate;
    private readonly Dictionary<(string Text, int SizeBucket, int Weight, int MaxWidth, int DpiBucket, int Color), FormattedText> _axisLabelCache = [];
    private readonly List<RowInfo> _rows = [];
    private readonly List<ParticipantRecord> _filteredPoints = [];
    private readonly List<ParticipantRecord> _recommendationPoints = [];

    private Rect _plotRect = Rect.Empty;
    private bool _indexDirty = true;
    private bool _isPreparingData;
    private ChartInteractionState _interactionState = ChartInteractionState.None;
    private bool _animateDiff;
    private bool _animationFrameHooked;
    private bool _isPanActive;
    private Point _panStart;
    private Vector _panOrigin;
    private Point _selectionStart;
    private Point _selectionCurrent;
    private string? _hoverCompany;
    private double _scale = 1.0;
    private Vector _offset = new(12, 12);
    private double _precisionStep = 0.01;
    private double _minRate = 99.0;
    private double _maxRate = 101.0;
    private int _columnCount;
    private int _labelEvery = 10;
    private DateTime _animationStartedAtUtc = DateTime.MinValue;
    private DateTime _lastFrameInvalidatedAtUtc = DateTime.MinValue;
    private DateTime _lastRenderErrorLoggedAtUtc = DateTime.MinValue;
    private string? _lastRenderErrorMessage;
    private readonly DispatcherTimer _pulseTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private RecommendationSnapshot _lastRecommendation = RecommendationSnapshot.Empty;
    private Rect _recommendationHotRect = Rect.Empty;
    private bool _isRecommendationHovered;
    private Point? _hoverPlotPosition;
    private double? _hoverRate;
    private string[] _hoverOverlayLines = [];
    private int _prepareVersion;
    private double _pulsePhase;

    public double? PinnedRecommendationBandMin
    {
        get => (double?)GetValue(PinnedRecommendationBandMinProperty);
        set => SetValue(PinnedRecommendationBandMinProperty, value);
    }

    public double? PinnedRecommendationBandMax
    {
        get => (double?)GetValue(PinnedRecommendationBandMaxProperty);
        set => SetValue(PinnedRecommendationBandMaxProperty, value);
    }

    public string PinnedRecommendationText
    {
        get => (string)GetValue(PinnedRecommendationTextProperty);
        set => SetValue(PinnedRecommendationTextProperty, value);
    }

    private static readonly Color[] DistinctCompanyPalette =
    [
        Color.FromRgb(0x1F, 0x77, 0xB4), // blue
        Color.FromRgb(0x2C, 0xA0, 0x2C), // green
        Color.FromRgb(0x94, 0x67, 0xBD), // purple
        Color.FromRgb(0x17, 0xBE, 0xCF), // cyan
        Color.FromRgb(0xBC, 0xBD, 0x22), // olive
        Color.FromRgb(0xE3, 0x77, 0xC2), // magenta
        Color.FromRgb(0x8C, 0x56, 0x4B), // brown
        Color.FromRgb(0x39, 0x7D, 0xF6), // vivid blue
        Color.FromRgb(0x2A, 0xA8, 0x4A), // vivid green
        Color.FromRgb(0x7C, 0x3A, 0xED), // violet
        Color.FromRgb(0x0E, 0x9F, 0xA8), // teal
        Color.FromRgb(0x9A, 0x34, 0xB8), // deep magenta
        Color.FromRgb(0x0F, 0x76, 0x67), // deep teal
        Color.FromRgb(0x46, 0x2A, 0xBA), // indigo
        Color.FromRgb(0x1D, 0x4E, 0x89), // navy
        Color.FromRgb(0x10, 0x8C, 0x57), // emerald
        Color.FromRgb(0x6D, 0x28, 0xD9), // purple 2
        Color.FromRgb(0x0E, 0x74, 0x90), // cyan deep
        Color.FromRgb(0x65, 0xA3, 0x0D), // lime deep
        Color.FromRgb(0xB4, 0x53, 0x09), // amber deep
        Color.FromRgb(0x7E, 0x22, 0xCE), // fuchsia deep
        Color.FromRgb(0x25, 0x63, 0xEB), // cobalt
        Color.FromRgb(0x05, 0x95, 0x8B), // sea green
        Color.FromRgb(0x43, 0x3D, 0x8B), // slate indigo
        Color.FromRgb(0x0D, 0x94, 0x88), // turquoise deep
        Color.FromRgb(0x54, 0x75, 0x0A), // olive deep
        Color.FromRgb(0x2B, 0x6C, 0xB0), // steel blue
        Color.FromRgb(0x4D, 0x7C, 0x0F), // moss
        Color.FromRgb(0x0F, 0x76, 0xA8), // ocean
        Color.FromRgb(0x5B, 0x21, 0xB6), // purple deep
        Color.FromRgb(0x15, 0x89, 0x3F), // forest
        Color.FromRgb(0x3B, 0x82, 0xF6), // azure
    ];

    public event EventHandler<CompanyPickedEventArgs>? CompanyPicked;
    public event EventHandler<RateRangeSelectedEventArgs>? RateRangeSelected;
    public event EventHandler<HoveredRateChangedEventArgs>? HoveredRateChanged;

    public NativeBidChartControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetCachingHint(this, CachingHint.Cache);
        _pulseTimer.Tick += HandlePulseTick;
        Loaded += (_, _) =>
        {
            UpdatePulseTimer();
            if (IsVisible && _indexDirty)
            {
                QueueDataPreparation();
            }
        };
        Unloaded += (_, _) => _pulseTimer.Stop();
        IsVisibleChanged += (_, _) =>
        {
            UpdatePulseTimer();
            if (IsVisible && _indexDirty)
            {
                QueueDataPreparation();
                InvalidateVisual();
            }
        };
    }

    public static readonly DependencyProperty ParticipantsProperty =
        DependencyProperty.Register(
            nameof(Participants),
            typeof(IEnumerable<ParticipantRecord>),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty SelectedCompanyProperty =
        DependencyProperty.Register(
            nameof(SelectedCompany),
            typeof(string),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                AllCompanyOption,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty MinAdjustRateProperty =
        DependencyProperty.Register(
            nameof(MinAdjustRate),
            typeof(string),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                "99.00000",
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty MaxAdjustRateProperty =
        DependencyProperty.Register(
            nameof(MaxAdjustRate),
            typeof(string),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                "101.00000",
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty StartDateProperty =
        DependencyProperty.Register(
            nameof(StartDate),
            typeof(DateTime),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                DateTime.MinValue,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty EndDateProperty =
        DependencyProperty.Register(
            nameof(EndDate),
            typeof(DateTime),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                DateTime.MaxValue,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty PrecisionProperty =
        DependencyProperty.Register(
            nameof(Precision),
            typeof(string),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                "0.01",
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(NativeBidChartMode),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                NativeBidChartMode.Scatter,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataPropertyChanged));

    public static readonly DependencyProperty HighPerformanceModeProperty =
        DependencyProperty.Register(
            nameof(HighPerformanceMode),
            typeof(bool),
            typeof(NativeBidChartControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ParticipantRecord>? Participants
    {
        get => (IEnumerable<ParticipantRecord>?)GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }

    public string SelectedCompany
    {
        get => (string)GetValue(SelectedCompanyProperty);
        set => SetValue(SelectedCompanyProperty, value);
    }

    public string MinAdjustRate
    {
        get => (string)GetValue(MinAdjustRateProperty);
        set => SetValue(MinAdjustRateProperty, value);
    }

    public string MaxAdjustRate
    {
        get => (string)GetValue(MaxAdjustRateProperty);
        set => SetValue(MaxAdjustRateProperty, value);
    }

    public DateTime StartDate
    {
        get => (DateTime)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime EndDate
    {
        get => (DateTime)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public string Precision
    {
        get => (string)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    public NativeBidChartMode Mode
    {
        get => (NativeBidChartMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public bool HighPerformanceMode
    {
        get => (bool)GetValue(HighPerformanceModeProperty);
        set => SetValue(HighPerformanceModeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        try
        {
            base.OnRender(drawingContext);
            var width = IsFinite(ActualWidth) ? ActualWidth : 0.0;
            var height = IsFinite(ActualHeight) ? ActualHeight : 0.0;
            var area = new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));
            drawingContext.DrawRectangle(BackgroundBrush, null, area);
            if (area.Width < 100 || area.Height < 100)
            {
                return;
            }

            RebuildIndexIfNeeded();
            NormalizeViewState();

            switch (Mode)
            {
                case NativeBidChartMode.Scatter:
                    DrawScatter(drawingContext, area);
                    break;
                case NativeBidChartMode.Trend:
                    DrawTrend(drawingContext, area);
                    break;
                case NativeBidChartMode.Distribution:
                    DrawDistribution(drawingContext, area);
                    break;
            }

            if (_interactionState == ChartInteractionState.Selecting && _plotRect != Rect.Empty)
            {
                var selection = new Rect(_selectionStart, _selectionCurrent);
                drawingContext.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(54, 245, 158, 11)),
                    SelectionPen,
                    selection);
            }

            if (_isPreparingData && _rows.Count == 0 && _filteredPoints.Count == 0)
            {
                DrawMessage(drawingContext, area, "차트 데이터를 준비 중입니다.");
            }
        }
        catch (Exception ex)
        {
            if (_animationFrameHooked)
            {
                CompositionTarget.Rendering -= HandleAnimationFrame;
                _animationFrameHooked = false;
            }

            try
            {
                var safeWidth = IsFinite(ActualWidth) && ActualWidth > 0 ? ActualWidth : 640.0;
                var safeHeight = IsFinite(ActualHeight) && ActualHeight > 0 ? ActualHeight : 360.0;
                var area = new Rect(0, 0, safeWidth, safeHeight);
                drawingContext.DrawRectangle(BackgroundBrush, null, area);
                DrawMessage(drawingContext, area, "그래프 렌더링 오류가 발생했습니다. 로그를 확인하세요.");
            }
            catch
            {
                // 오류 메시지 렌더링 실패는 무시하고 로그만 남김
            }

            LogRenderException(ex);
        }
    }

    private void LogRenderException(Exception ex)
    {
        var now = DateTime.UtcNow;
        var message = ex.Message ?? string.Empty;
        if (string.Equals(_lastRenderErrorMessage, message, StringComparison.Ordinal)
            && (now - _lastRenderErrorLoggedAtUtc) < TimeSpan.FromSeconds(10))
        {
            return;
        }

        _lastRenderErrorMessage = message;
        _lastRenderErrorLoggedAtUtc = now;

        try
        {
            var path = Path.Combine(
                DefaultAppPathResolver.ResolveDefaultAppLogDirectory(),
                "chart-render.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            lock (RenderErrorLogSync)
            {
                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n{new string('-', 72)}\n",
                    System.Text.Encoding.UTF8);
            }
        }
        catch
        {
            // 렌더 오류 로깅 실패는 무시
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_plotRect == Rect.Empty || !_plotRect.Contains(e.GetPosition(this)))
        {
            return;
        }

        if (!IsFinite(_scale) || _scale <= 0.0001)
        {
            _scale = 1.0;
        }
        if (!IsFinite(_offset.X) || !IsFinite(_offset.Y))
        {
            _offset = new Vector(12, 12);
        }

        var zoomFactor = e.Delta > 0 ? 1.10 : 0.90;
        var oldScale = _scale;
        _scale = Math.Clamp(_scale * zoomFactor, 0.25, 5.0);

        var point = e.GetPosition(this);
        var px = point.X - _plotRect.Left;
        var py = point.Y - _plotRect.Top;
        if (oldScale > 0.0001)
        {
            var ratio = _scale / oldScale;
            _offset.X = px - ((px - _offset.X) * ratio);
            _offset.Y = py - ((py - _offset.Y) * ratio);
        }

        NormalizeViewState();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Mode == NativeBidChartMode.Scatter && TryApplyRecommendedRange(position))
        {
            EndInteraction(invalidateVisual: true);
            e.Handled = true;
            return;
        }

        if (!_plotRect.Contains(position))
        {
            return;
        }

        if (Mode == NativeBidChartMode.Scatter && e.ClickCount >= 2)
        {
            TryEmitCompanyPick(position);
            EndInteraction(invalidateVisual: false);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _interactionState = ChartInteractionState.Selecting;
            _selectionStart = position;
            _selectionCurrent = position;
        }
        else
        {
            _interactionState = ChartInteractionState.Panning;
            _panStart = position;
            _panOrigin = _offset;
            _isPanActive = false;
        }

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);

        if (_interactionState == ChartInteractionState.Panning)
        {
            if (!_isPanActive)
            {
                var movement = (position - _panStart).Length;
                if (movement < DragActivationThreshold)
                {
                    return;
                }

                _isPanActive = true;
            }

            _offset = _panOrigin + (Vector)(position - _panStart);
            NormalizeViewState();
            InvalidateVisual();
            return;
        }

        if (_interactionState == ChartInteractionState.Selecting)
        {
            _selectionCurrent = position;
            InvalidateVisual();
            return;
        }

        UpdateHoveredRate(position);

        if (UpdateRecommendationHover(position))
        {
            if (_hoverCompany is not null)
            {
                _hoverCompany = null;
            }

            InvalidateVisual();
            return;
        }

        UpdateHoverState(position);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_interactionState == ChartInteractionState.Selecting)
        {
            var selection = new Rect(_selectionStart, _selectionCurrent);
            if (selection.Width > 6 && selection.Height > 6)
            {
                EmitRangeSelection(selection);
            }
            EndInteraction(invalidateVisual: true);
        }
        else if (_interactionState == ChartInteractionState.Panning)
        {
            if (!_isPanActive && e.ClickCount >= 2)
            {
                TryEmitCompanyPick(position);
            }

            EndInteraction(invalidateVisual: false);
        }

        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCompany = null;
        _isRecommendationHovered = false;
        ClearHoveredRate();
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        EndInteraction(invalidateVisual: false);
        _scale = 1.0;
        _offset = new Vector(12, 12);
        _hoverCompany = null;
        _isRecommendationHovered = false;
        ClearHoveredRate();
        ToolTip = null;
        CompanyPicked?.Invoke(this, new CompanyPickedEventArgs(AllCompanyOption));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_interactionState != ChartInteractionState.None)
        {
            _interactionState = ChartInteractionState.None;
            _isPanActive = false;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_interactionState != ChartInteractionState.None)
        {
            EndInteraction(invalidateVisual: true);
            e.Handled = true;
        }
    }

    private static void OnDataPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NativeBidChartControl control)
        {
            return;
        }

        control._indexDirty = true;
        control.InvalidateCachedLayers();
        if (!control.IsVisible)
        {
            return;
        }

        control.UpdatePulseTimer();
        control.QueueDataPreparation();
        control.InvalidateVisual();
    }

    private void RebuildIndexIfNeeded()
    {
        if (!_indexDirty)
        {
            return;
        }

        _indexDirty = false;
        QueueDataPreparation();
    }

    private void QueueDataPreparation()
    {
        var participants = (Participants ?? Array.Empty<ParticipantRecord>()).ToList();
        var selectedCompany = SelectedCompany;
        var precision = Precision;
        var minAdjustRate = MinAdjustRate;
        var maxAdjustRate = MaxAdjustRate;
        var startDate = StartDate;
        var endDate = EndDate;
        var mode = Mode;
        var version = Interlocked.Increment(ref _prepareVersion);
        _isPreparingData = true;

        _ = Task.Run(() => BuildPreparedState(participants, selectedCompany, precision, minAdjustRate, maxAdjustRate, startDate, endDate, mode))
            .ContinueWith(
                task =>
                {
                    _ = Dispatcher.InvokeAsync(
                        () =>
                        {
                            if (version != _prepareVersion)
                            {
                                return;
                            }

                            _isPreparingData = false;
                            if (task.IsFaulted)
                            {
                                _cellLookup.Clear();
                                _rows.Clear();
                                _filteredPoints.Clear();
                                _recommendationPoints.Clear();
                                _lastRecommendation = RecommendationSnapshot.Empty;
                                _recommendationHotRect = Rect.Empty;
                                _isRecommendationHovered = false;
                                InvalidateVisual();
                                return;
                            }

                            ApplyPreparedState(task.Result);
                        },
                        DispatcherPriority.Background);
                },
                TaskScheduler.Default);
    }

    private PreparedChartState BuildPreparedState(
        IReadOnlyList<ParticipantRecord> participants,
        string selectedCompany,
        string precision,
        string minAdjustRate,
        string maxAdjustRate,
        DateTime startDate,
        DateTime endDate,
        NativeBidChartMode mode)
    {
        participants = ScatterChartRules.InPeriod(
            CollectorNoticeRules.ApplyChartRules(participants ?? Array.Empty<ParticipantRecord>()),
            startDate == default ? DateTime.MinValue : startDate,
            endDate == default ? DateTime.MaxValue : endDate);
        var precisionStep = ParsePrecision(precision);
        var minRate = ParseRate(minAdjustRate, 99.0);
        var maxRate = ParseRate(maxAdjustRate, 101.0);
        if (!IsFinite(minRate))
        {
            minRate = 99.0;
        }
        if (!IsFinite(maxRate))
        {
            maxRate = 101.0;
        }
        if (minRate > maxRate)
        {
            (minRate, maxRate) = (maxRate, minRate);
        }

        minRate = Math.Clamp(minRate, 98.0, 102.0);
        maxRate = Math.Clamp(maxRate, 98.0, 102.0);
        if (Math.Abs(maxRate - minRate) < 0.00001)
        {
            maxRate = Math.Min(102.0, minRate + 0.1);
        }

        var columnCount = Math.Max(1, (int)Math.Round((maxRate - minRate) / precisionStep, MidpointRounding.AwayFromZero) + 1);
        var labelEvery = Math.Max(1, (int)Math.Round(0.1 / precisionStep, MidpointRounding.AwayFromZero));

        var selectedTokens = SplitSelectedCompanyTokens(selectedCompany);
        var filterAll = selectedTokens.Count == 0 || selectedTokens.Any(IsAllCompanyToken);
        var normalizedSelectedTokens = selectedTokens
            .Where(x => !IsAllCompanyToken(x))
            .Select(NormalizeCompanyToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var marketWindowPoints = participants
            .Where(x => x.AdjustRate.HasValue)
            .Where(x => x.AdjustRate!.Value >= minRate && x.AdjustRate.Value <= maxRate)
            .Where(x => !IsExcludedCompany(x.NormalizedCompanyName, x.CompanyName))
            .ToList();
        var recommendationPoints = ParticipantSameDayClusterReducer
            .Collapse(
                marketWindowPoints,
                AnalysisProfile.Value.SameDayClusterRateTolerance,
                AnalysisProfile.Value.SameDayClusterMinClusterSize)
            .ToList();

        var filteredPoints = marketWindowPoints
            .Where(x =>
            {
                if (filterAll)
                {
                    return true;
                }

                var normalizedRaw = NormalizeCompanyToken(x.CompanyName);
                var normalizedName = NormalizeCompanyToken(x.NormalizedCompanyName);
                foreach (var token in normalizedSelectedTokens)
                {
                    if (normalizedRaw.Contains(token, StringComparison.OrdinalIgnoreCase)
                        || normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            })
            .ToList();
        if (mode != NativeBidChartMode.Scatter)
        {
            filteredPoints = ParticipantSameDayClusterReducer
                .Collapse(
                    filteredPoints,
                    AnalysisProfile.Value.SameDayClusterRateTolerance,
                    AnalysisProfile.Value.SameDayClusterMinClusterSize)
                .ToList();
        }

        var recommendation = BuildRecommendationSnapshot(recommendationPoints, minRate, maxRate);
        var rows = new List<RowInfo>();
        var cellLookup = new Dictionary<(int RowIndex, int ColumnIndex), List<ParticipantRecord>>();

        if (filteredPoints.Count > 0 && mode == NativeBidChartMode.Scatter)
        {
            rows = filteredPoints
                .GroupBy(
                    x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                    StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var scatter = ScatterChartRules.BuildNotice(participants, g.Key, minRate, maxRate);
                    return new RowInfo(
                    g.Key,
                    g.Max(x => x.OpenDate),
                    participants
                        .Where(x => string.Equals(
                            string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                            g.Key,
                            StringComparison.OrdinalIgnoreCase))
                        .Where(x => x.AdjustRate.HasValue)
                        .Where(x => x.IsAwarded || x.Rank == 1)
                        .OrderByDescending(x => x.OpenDate)
                        .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                        .FirstOrDefault(),
                    scatter.Chips.ToList());
                })
                .OrderBy(x => x.OpenDate)
                .ToList();

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                foreach (var participant in rows[rowIndex].Points)
                {
                    if (!participant.AdjustRate.HasValue)
                    {
                        continue;
                    }

                    var columnIndex = ToColumnIndex(participant.AdjustRate.Value, minRate, precisionStep, columnCount);
                    var key = (rowIndex, columnIndex);
                    if (!cellLookup.TryGetValue(key, out var bucket))
                    {
                        bucket = [];
                        cellLookup[key] = bucket;
                    }
                    bucket.Add(participant);
                }
            }

            foreach (var key in cellLookup.Keys.ToList())
            {
                cellLookup[key] = ScatterChartRules.SortOverlappingChips(cellLookup[key]);
            }
        }

        return new PreparedChartState(
            FilteredPoints: filteredPoints,
            RecommendationPoints: recommendationPoints,
            Rows: rows,
            CellLookup: cellLookup,
            Recommendation: recommendation,
            PrecisionStep: precisionStep,
            MinRate: minRate,
            MaxRate: maxRate,
            ColumnCount: columnCount,
            LabelEvery: labelEvery,
            AnimateDiff: false);
    }

    private void ApplyPreparedState(PreparedChartState state)
    {
        _animateDiff = state.AnimateDiff;
        _precisionStep = state.PrecisionStep;
        _minRate = state.MinRate;
        _maxRate = state.MaxRate;
        _columnCount = state.ColumnCount;
        _labelEvery = state.LabelEvery;

        _cellLookup.Clear();
        foreach (var entry in state.CellLookup)
        {
            _cellLookup[entry.Key] = entry.Value;
        }

        _rows.Clear();
        _rows.AddRange(state.Rows);

        _filteredPoints.Clear();
        _filteredPoints.AddRange(state.FilteredPoints);

        _recommendationPoints.Clear();
        _recommendationPoints.AddRange(state.RecommendationPoints);

        _lastRecommendation = state.Recommendation;
        _recommendationHotRect = Rect.Empty;
        _isRecommendationHovered = false;
        InvalidateCachedLayers();

        if (_animateDiff && _rows.Count > 0)
        {
            _animationStartedAtUtc = DateTime.UtcNow;
        }
        else if (_animationFrameHooked)
        {
            CompositionTarget.Rendering -= HandleAnimationFrame;
            _animationFrameHooked = false;
        }

        InvalidateVisual();
    }

    private void InvalidateCachedLayers()
    {
        _chipLayerCache = null;
        _viewLayer = null;
    }

    private void DrawScatter(DrawingContext dc, Rect area)
    {
        var left = 220.0;
        var right = 22.0;
        var top = 16.0;
        var bottom = 46.0;
        _plotRect = new Rect(
            left,
            top,
            Math.Max(120, area.Width - left - right),
            Math.Max(120, area.Height - top - bottom));

        if (_rows.Count == 0 || _columnCount <= 0)
        {
            dc.DrawRectangle(PlotBackgroundBrush, AxisPen, _plotRect);
            DrawMessage(dc, area, "동국 참여 낙찰공고가 없습니다. 기간·지역을 조정하세요.");
            return;
        }

        var layout = MeasureScatterLayout();
        var viewKey = BuildViewCacheKey(area);
        var overlayOnly = _interactionState == ChartInteractionState.None
            && !_isPanActive
            && ScatterRenderCache.IsOverlayOnlyFrame(_viewLayer is not null, _viewLayerKey, viewKey);

        if (overlayOnly)
        {
            dc.DrawImage(_viewLayer, area);
        }
        else if (_isPanActive || _interactionState == ChartInteractionState.Panning)
        {
            DrawScatterStatic(dc, area, layout);
            _viewLayer = null;
        }
        else
        {
            try
            {
                _viewLayer = RasterizeScatterStatic(area, layout);
                _viewLayerKey = viewKey;
                dc.DrawImage(_viewLayer, area);
            }
            catch
            {
                DrawScatterStatic(dc, area, layout);
                _viewLayer = null;
            }
        }

        DrawScatterOverlay(dc, layout);

        if (_animationFrameHooked)
        {
            CompositionTarget.Rendering -= HandleAnimationFrame;
            _animationFrameHooked = false;
        }
    }

    private ScatterLayout MeasureScatterLayout()
    {
        var (cellWidth, cellHeight) = GetScatterCellSize();
        var invScale = 1.0 / Math.Max(0.0001, _scale);
        var visibleWorld = new Rect(
            (-_offset.X) * invScale,
            (-_offset.Y) * invScale,
            _plotRect.Width * invScale,
            _plotRect.Height * invScale);

        var gridColStart = (int)Math.Floor(visibleWorld.Left / cellWidth) - 1;
        var gridColEnd = (int)Math.Ceiling(visibleWorld.Right / cellWidth) + 1;
        var colStart = Math.Clamp(gridColStart, 0, Math.Max(0, _columnCount - 1));
        var colEnd = Math.Clamp(gridColEnd, 0, Math.Max(0, _columnCount - 1));
        var gridRowStart = (int)Math.Floor(visibleWorld.Top / cellHeight) - 1;
        var unclampedGridRowEnd = (int)Math.Ceiling(visibleWorld.Bottom / cellHeight) + 1;
        var worldRowCount = Math.Max(_rows.Count, unclampedGridRowEnd + 1);
        var worldWidth = _columnCount * cellWidth;
        var worldHeight = worldRowCount * cellHeight;
        var rowStart = Math.Clamp(gridRowStart, 0, Math.Max(0, _rows.Count - 1));
        var rowEnd = Math.Clamp(unclampedGridRowEnd, 0, Math.Max(0, _rows.Count - 1));
        var visibleRows = Math.Max(1, rowEnd - rowStart + 1);
        var visibleCols = Math.Max(1, colEnd - colStart + 1);
        var visibleCellEstimate = visibleRows * visibleCols;
        var drawRowLabels = !HighPerformanceMode
            || (_scale >= 1.0 && visibleRows <= 140 && visibleCellEstimate <= 18000);
        var recommendation = ResolveDisplayRecommendation();
        var bandStartColumn = recommendation is null
            ? 0
            : ToColumnIndex(Math.Min(recommendation.BandMin, recommendation.BandMax));
        var bandEndColumn = recommendation is null
            ? 0
            : ToColumnIndex(Math.Max(recommendation.BandMin, recommendation.BandMax));

        return new ScatterLayout(
            cellWidth,
            cellHeight,
            worldWidth,
            worldHeight,
            colStart,
            colEnd,
            rowStart,
            rowEnd,
            gridColStart,
            gridColEnd,
            gridRowStart,
            unclampedGridRowEnd,
            drawRowLabels,
            recommendation,
            bandStartColumn,
            bandEndColumn);
    }

    private ScatterViewCacheKey BuildViewCacheKey(Rect area)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new ScatterViewCacheKey(
            _prepareVersion,
            Math.Max(1, (int)Math.Round(area.Width * dpi)),
            Math.Max(1, (int)Math.Round(area.Height * dpi)),
            (int)Math.Round(_scale * 1000.0),
            (int)Math.Round(_offset.X * 10.0),
            (int)Math.Round(_offset.Y * 10.0),
            (int)Math.Round(_plotRect.Width),
            (int)Math.Round(_plotRect.Height),
            _columnCount,
            _rows.Count);
    }

    private ImageSource RasterizeScatterStatic(Rect area, ScatterLayout layout)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var dpiX = 96.0 * dpi.DpiScaleX;
        var dpiY = 96.0 * dpi.DpiScaleY;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(area.Width * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(area.Height * dpi.DpiScaleY));
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            DrawScatterStatic(ctx, area, layout);
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private void DrawScatterStatic(DrawingContext dc, Rect area, ScatterLayout layout)
    {
        dc.DrawRectangle(BackgroundBrush, null, area);
        dc.DrawRectangle(PlotBackgroundBrush, AxisPen, _plotRect);

        dc.PushClip(new RectangleGeometry(_plotRect));
        dc.PushTransform(new TranslateTransform(_plotRect.Left + _offset.X, _plotRect.Top + _offset.Y));
        dc.PushTransform(new ScaleTransform(_scale, _scale));

        if (layout.Recommendation is not null)
        {
            var recommendationBand = new Rect(
                layout.BandStartColumn * layout.CellWidth,
                0,
                Math.Max(1, ((layout.BandEndColumn - layout.BandStartColumn) + 1) * layout.CellWidth),
                layout.WorldHeight);
            dc.DrawRectangle(RecommendationBandBrush, RecommendationBandPen, recommendationBand);
        }

        var gridTop = layout.GridRowStart * layout.CellHeight;
        var gridBottom = (layout.UnclampedGridRowEnd + 1) * layout.CellHeight;
        var gridLeft = layout.GridColStart * layout.CellWidth;
        var gridRight = (layout.GridColEnd + 1) * layout.CellWidth;
        var gridColStep = HighPerformanceMode && (layout.GridColEnd - layout.GridColStart) > 90 ? 2 : 1;
        var gridRowStep = HighPerformanceMode && (layout.UnclampedGridRowEnd - layout.GridRowStart) > 90 ? 2 : 1;
        for (var col = layout.GridColStart; col <= layout.GridColEnd; col += gridColStep)
        {
            var x = col * layout.CellWidth;
            var pen = col % _labelEvery == 0 ? MajorGridPen : GridPen;
            dc.DrawLine(pen, new Point(x, gridTop), new Point(x, gridBottom));
        }

        for (var row = layout.GridRowStart; row <= layout.UnclampedGridRowEnd; row += gridRowStep)
        {
            var y = row * layout.CellHeight;
            dc.DrawLine(GridPen, new Point(gridLeft, y), new Point(gridRight, y));
        }

        EnsureChipLayerCache(layout.CellWidth, layout.CellHeight);
        if (_chipLayerCache is not null)
        {
            dc.DrawDrawing(_chipLayerCache);
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();

        DrawScatterAxes(
            dc,
            layout.CellWidth,
            layout.CellHeight,
            layout.ColStart,
            layout.ColEnd,
            layout.RowStart,
            layout.RowEnd,
            layout.DrawRowLabels);

        if (layout.Recommendation is not null)
        {
            DrawRecommendationArrow(dc, layout.Recommendation, layout.CellWidth);
            DrawRecommendedLabel(dc, layout.Recommendation);
        }
        else
        {
            _recommendationHotRect = Rect.Empty;
        }
    }

    private void DrawScatterOverlay(DrawingContext dc, ScatterLayout layout)
    {
        dc.PushClip(new RectangleGeometry(_plotRect));
        dc.PushTransform(new TranslateTransform(_plotRect.Left + _offset.X, _plotRect.Top + _offset.Y));
        dc.PushTransform(new ScaleTransform(_scale, _scale));

        var hoverCompany = _hoverCompany;
        for (var row = layout.RowStart; row <= layout.RowEnd; row++)
        {
            var rowInfo = _rows[row];
            if (!string.IsNullOrWhiteSpace(hoverCompany))
            {
                for (var col = layout.ColStart; col <= layout.ColEnd; col++)
                {
                    if (!_cellLookup.TryGetValue((row, col), out var bucket))
                    {
                        continue;
                    }

                    for (var i = 0; i < bucket.Count; i++)
                    {
                        var point = bucket[i];
                        if (!ContainsToken(point.CompanyName, hoverCompany)
                            && !ContainsToken(point.NormalizedCompanyName, hoverCompany))
                        {
                            continue;
                        }

                        var rect = BuildPointRect(row, col, i, layout.CellWidth, layout.CellHeight, 1.0);
                        dc.DrawRectangle(null, HoverHighlightPen, rect);
                    }
                }
            }

            if (rowInfo.AwardedPoint?.AdjustRate is double awardedRate
                && (awardedRate < _minRate || awardedRate > _maxRate))
            {
                var markerRect = awardedRate < _minRate
                    ? new Rect(2, (row * layout.CellHeight) + 2, Math.Max(7, layout.CellWidth * 0.55), Math.Max(7, layout.CellHeight * 0.42))
                    : new Rect(layout.WorldWidth - Math.Max(11, layout.CellWidth * 0.7), (row * layout.CellHeight) + 2, Math.Max(7, layout.CellWidth * 0.55), Math.Max(7, layout.CellHeight * 0.42));
                dc.DrawRoundedRectangle(RankBadgeBrush, RankHighlightOuterPen, markerRect, 2, 2);
            }
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();
        DrawHoveredRateGuide(dc);
    }

    private readonly record struct ScatterLayout(
        double CellWidth,
        double CellHeight,
        double WorldWidth,
        double WorldHeight,
        int ColStart,
        int ColEnd,
        int RowStart,
        int RowEnd,
        int GridColStart,
        int GridColEnd,
        int GridRowStart,
        int UnclampedGridRowEnd,
        bool DrawRowLabels,
        RecommendationSnapshot? Recommendation,
        int BandStartColumn,
        int BandEndColumn);

    private void DrawTrend(DrawingContext dc, Rect area)
    {
        _plotRect = new Rect(52, 20, Math.Max(120, area.Width - 74), Math.Max(120, area.Height - 62));
        dc.DrawRectangle(PlotBackgroundBrush, AxisPen, _plotRect);

        if (_filteredPoints.Count == 0)
        {
            DrawMessage(dc, area, "선형 추세를 표시할 데이터가 없습니다.");
            return;
        }

        var groupedByCompany = _filteredPoints
            .GroupBy(GetCompanyGroupKey)
            .ToList();

        groupedByCompany = IsAllCompanySelected()
            ? groupedByCompany
                .OrderByDescending(g => g.Count())
                .Take(8)
                .ToList()
            : groupedByCompany
                .OrderBy(g => ResolveSelectedCompanyOrder(g.Key))
                .ThenByDescending(g => g.Count())
                .ToList();

        groupedByCompany = groupedByCompany
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToList();

        if (groupedByCompany.Count == 0)
        {
            DrawMessage(dc, area, "선형 추세를 표시할 데이터가 없습니다.");
            return;
        }

        var yMin = _minRate;
        var yMax = _maxRate;
        var ySpan = Math.Max(0.1, yMax - yMin);
        var maxPointCount = groupedByCompany
            .Select(g => g.Count(x => x.AdjustRate.HasValue))
            .DefaultIfEmpty(0)
            .Max();
        var worldWidth = GetTrendWorldWidth(maxPointCount);

        DrawTrendAxes(dc, yMin, yMax, worldWidth);

        dc.PushClip(new RectangleGeometry(_plotRect));
        dc.PushTransform(new TranslateTransform(_plotRect.Left + _offset.X, _plotRect.Top));
        dc.PushTransform(new ScaleTransform(_scale, 1.0));

        var showMarkers = !IsAllCompanySelected();
        foreach (var g in groupedByCompany)
        {
            var points = g
                .Where(x => x.AdjustRate.HasValue)
                .OrderBy(x => x.OpenDate)
                .Select((x, idx) => new
                {
                    X = idx,
                    Y = x.AdjustRate!.Value,
                })
                .ToList();

            if (points.Count < 2)
            {
                continue;
            }

            var maxX = Math.Max(1, points.Count - 1);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var i = 0; i < points.Count; i++)
                {
                    var px = (points[i].X / (double)maxX) * worldWidth;
                    var py = _plotRect.Height - ((points[i].Y - yMin) / ySpan) * _plotRect.Height;
                    if (i == 0)
                    {
                        ctx.BeginFigure(new Point(px, py), false, false);
                    }
                    else
                    {
                        ctx.LineTo(new Point(px, py), true, false);
                    }
                }
            }
            geometry.Freeze();

            var color = ResolveCompanyColor(g.Key, g.Key);
            var pen = new Pen(new SolidColorBrush(color), IsAllCompanySelected() ? 1.8 : 2.6);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);

            if (!showMarkers)
            {
                continue;
            }

            var markerBrush = ResolveCompanyBrush(g.Key, g.Key);
            const double markerRadius = 3.2;
            for (var i = 0; i < points.Count; i++)
            {
                var px = (points[i].X / (double)maxX) * worldWidth;
                var py = _plotRect.Height - ((points[i].Y - yMin) / ySpan) * _plotRect.Height;
                dc.DrawEllipse(markerBrush, TrendMarkerPen, new Point(px, py), markerRadius, markerRadius);
            }
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();

        DrawText(
            dc,
            $"기초대비사정률 추세 ({_minRate:F3} ~ {_maxRate:F3})",
            new Point(_plotRect.Left + 6, _plotRect.Top + 6),
            12,
            FontWeights.SemiBold,
            TextBrush);
    }

    private void DrawDistribution(DrawingContext dc, Rect area)
    {
        _plotRect = new Rect(48, 20, Math.Max(120, area.Width - 70), Math.Max(120, area.Height - 62));
        dc.DrawRectangle(PlotBackgroundBrush, AxisPen, _plotRect);

        var rates = _filteredPoints
            .Where(x => x.AdjustRate.HasValue)
            .ToList();
        var filteredRates = rates
            .Select(x => x.AdjustRate!.Value)
            .Where(x => x >= _minRate && x <= _maxRate)
            .ToList();
        if (filteredRates.Count == 0)
        {
            DrawMessage(dc, area, "분포를 표시할 데이터가 없습니다.");
            return;
        }

        var groupedByCompany = rates
            .GroupBy(GetCompanyGroupKey)
            .ToList();
        groupedByCompany = IsAllCompanySelected()
            ? groupedByCompany
                .OrderByDescending(g => g.Count())
                .Take(8)
                .ToList()
            : groupedByCompany
                .OrderBy(g => ResolveSelectedCompanyOrder(g.Key))
                .ThenByDescending(g => g.Count())
                .ToList();

        var companyBins = groupedByCompany
            .Select(g =>
            {
                var bins = new int[_columnCount];
                foreach (var row in g.Where(x => x.AdjustRate.HasValue))
                {
                    var rate = row.AdjustRate!.Value;
                    if (rate < _minRate || rate > _maxRate)
                    {
                        continue;
                    }

                    var index = ToColumnIndex(rate);
                    bins[index]++;
                }

                return new
                {
                    Company = g.Key,
                    Brush = ResolveCompanyBrush(g.Key, g.Key),
                    Bins = bins,
                };
            })
            .Where(x => x.Bins.Any(count => count > 0))
            .ToList();
        var totalBins = new int[_columnCount];
        foreach (var series in companyBins)
        {
            for (var i = 0; i < _columnCount; i++)
            {
                totalBins[i] += series.Bins[i];
            }
        }

        var maxCount = Math.Max(1, totalBins.Max());
        var worldWidth = GetDistributionWorldWidth();
        var barWidth = worldWidth / _columnCount;

        DrawDistributionAxes(dc, maxCount, barWidth, worldWidth);

        dc.PushClip(new RectangleGeometry(_plotRect));
        dc.PushTransform(new TranslateTransform(_plotRect.Left + _offset.X, _plotRect.Top));
        dc.PushTransform(new ScaleTransform(_scale, 1.0));

        for (var i = 0; i < _columnCount; i++)
        {
            var x = i * barWidth;
            if (IsAllCompanySelected())
            {
                var h = (totalBins[i] / (double)maxCount) * _plotRect.Height;
                var y = _plotRect.Height - h;
                var brush = i % _labelEvery == 0 ? DistributionMajorBarBrush : DistributionMinorBarBrush;
                dc.DrawRectangle(brush, DistributionStackBorderPen, new Rect(x + 1, y, Math.Max(1, barWidth - 2), h));
                continue;
            }

            var accumulatedHeight = 0.0;
            foreach (var series in companyBins)
            {
                var count = series.Bins[i];
                if (count <= 0)
                {
                    continue;
                }

                var segmentHeight = (count / (double)maxCount) * _plotRect.Height;
                var y = _plotRect.Height - accumulatedHeight - segmentHeight;
                dc.DrawRectangle(series.Brush, DistributionStackBorderPen, new Rect(x + 1, y, Math.Max(1, barWidth - 2), segmentHeight));
                accumulatedHeight += segmentHeight;
            }
        }

        var centerIndex = ToColumnIndex(100.0);
        var centerX = centerIndex * barWidth + (barWidth / 2.0);
        dc.DrawLine(DistributionCenterPen, new Point(centerX, 0), new Point(centerX, _plotRect.Height));

        dc.Pop();
        dc.Pop();
        dc.Pop();

        DrawText(
            dc,
            $"사정률 분포 ({filteredRates.Count:N0}건, 중심 100.000)",
            new Point(_plotRect.Left + 6, _plotRect.Top + 6),
            12,
            FontWeights.SemiBold,
            TextBrush);
    }

    private void DrawTrendAxes(DrawingContext dc, double yMin, double yMax, double worldWidth)
    {
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Bottom), new Point(_plotRect.Right, _plotRect.Bottom));
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Top), new Point(_plotRect.Left, _plotRect.Bottom));

        var ySpan = Math.Max(0.1, yMax - yMin);
        const int yTickCount = 4;
        for (var i = 0; i <= yTickCount; i++)
        {
            var ratio = i / (double)yTickCount;
            var y = _plotRect.Bottom - (ratio * _plotRect.Height);
            dc.DrawLine(i == 0 ? AxisPen : GridPen, new Point(_plotRect.Left, y), new Point(_plotRect.Right, y));

            var value = yMin + (ratio * ySpan);
            DrawText(
                dc,
                value.ToString("F3", CultureInfo.InvariantCulture),
                new Point(Math.Max(2, _plotRect.Left - 52), y - 8),
                10,
                FontWeights.Medium,
                SubtleTextBrush,
                56);
        }

        const int xTickCount = 4;
        for (var i = 0; i <= xTickCount; i++)
        {
            var ratio = i / (double)xTickCount;
            var x = ToHorizontalViewportX(ratio * worldWidth);
            if (x < (_plotRect.Left - 32) || x > (_plotRect.Right + 32))
            {
                continue;
            }

            dc.DrawLine(i == 0 ? AxisPen : MajorGridPen, new Point(x, _plotRect.Top), new Point(x, _plotRect.Bottom));
            DrawText(
                dc,
                $"{ratio * 100:0}%",
                new Point(x - 14, _plotRect.Bottom + 6),
                10,
                FontWeights.Medium,
                SubtleTextBrush,
                36);
        }

        DrawText(
            dc,
            "사정률(%)",
            new Point(Math.Max(2, _plotRect.Left - 52), Math.Max(2, _plotRect.Top - 14)),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            56);
        DrawText(
            dc,
            "시계열 진행(%)",
            new Point(Math.Max(_plotRect.Left + 4, _plotRect.Right - 88), _plotRect.Bottom + 22),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            96);
    }

    private void DrawDistributionAxes(DrawingContext dc, int maxCount, double barWidth, double worldWidth)
    {
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Bottom), new Point(_plotRect.Right, _plotRect.Bottom));
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Top), new Point(_plotRect.Left, _plotRect.Bottom));

        const int yTickCount = 4;
        for (var i = 0; i <= yTickCount; i++)
        {
            var ratio = i / (double)yTickCount;
            var y = _plotRect.Bottom - (ratio * _plotRect.Height);
            dc.DrawLine(i == 0 ? AxisPen : GridPen, new Point(_plotRect.Left, y), new Point(_plotRect.Right, y));

            var countLabel = (int)Math.Round(maxCount * ratio, MidpointRounding.AwayFromZero);
            DrawText(
                dc,
                countLabel.ToString("N0", CultureInfo.InvariantCulture),
                new Point(Math.Max(2, _plotRect.Left - 44), y - 8),
                10,
                FontWeights.Medium,
                SubtleTextBrush,
                48);
        }

        var xLabelStep = Math.Max(_labelEvery, Math.Max(1, _columnCount / 8));
        var rateFormat = _precisionStep <= 0.0015 ? "F3" : "F2";
        for (var i = 0; i < _columnCount; i += xLabelStep)
        {
            var x = ToHorizontalViewportX((i * barWidth) + (barWidth / 2.0));
            if (x < (_plotRect.Left - 44) || x > (_plotRect.Right + 44))
            {
                continue;
            }

            dc.DrawLine(MajorGridPen, new Point(x, _plotRect.Bottom), new Point(x, _plotRect.Top));

            var rate = _minRate + (i * _precisionStep);
            DrawText(
                dc,
                rate.ToString(rateFormat, CultureInfo.InvariantCulture),
                new Point(x - 20, _plotRect.Bottom + 6),
                10,
                FontWeights.Medium,
                SubtleTextBrush,
                44);
        }

        DrawText(
            dc,
            "건수",
            new Point(Math.Max(2, _plotRect.Left - 38), Math.Max(2, _plotRect.Top - 14)),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            40);
        DrawText(
            dc,
            "기초대비사정률(%)",
            new Point(Math.Max(_plotRect.Left + 4, _plotRect.Right - 112), _plotRect.Bottom + 22),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            120);
    }

    private void DrawScatterAxes(
        DrawingContext dc,
        double cellWidth,
        double cellHeight,
        int colStart,
        int colEnd,
        int rowStart,
        int rowEnd,
        bool drawRowLabels)
    {
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Bottom), new Point(_plotRect.Right, _plotRect.Bottom));
        dc.DrawLine(AxisPen, new Point(_plotRect.Left, _plotRect.Top), new Point(_plotRect.Left, _plotRect.Bottom));

        var rateFormat = _precisionStep <= 0.0015 ? "F3" : "F2";

        var rowStride = 1;
        if (HighPerformanceMode)
        {
            var visibleRows = Math.Max(1, rowEnd - rowStart + 1);
            if (visibleRows > 180)
            {
                rowStride = 4;
            }
            else if (visibleRows > 120)
            {
                rowStride = 3;
            }
            else if (visibleRows > 70)
            {
                rowStride = 2;
            }
        }

        for (var row = rowStart; row <= rowEnd; row += rowStride)
        {
            if (!drawRowLabels)
            {
                break;
            }

            var y = _plotRect.Top + _offset.Y + ((row + 0.5) * cellHeight * _scale);
            if (y < _plotRect.Top - 8 || y > _plotRect.Bottom + 8)
            {
                continue;
            }

            var rowInfo = _rows[row];
            var label = $"{rowInfo.NoticeNumber} ({rowInfo.OpenDate:yy-MM-dd HH:mm})";
            dc.DrawLine(
                AxisPen,
                new Point(_plotRect.Left - 4, y),
                new Point(_plotRect.Left, y));
            DrawText(
                dc,
                label,
                new Point(6, y - 6),
                10,
                FontWeights.Medium,
                SubtleTextBrush,
                maxWidth: Math.Max(40, IsFinite(_plotRect.Left) ? _plotRect.Left - 12 : 220));
        }

        for (var col = colStart; col <= colEnd; col++)
        {
            if (col % _labelEvery != 0)
            {
                continue;
            }

            var x = _plotRect.Left + _offset.X + ((col + 0.5) * cellWidth * _scale);
            if (x < _plotRect.Left - 10 || x > _plotRect.Right + 10)
            {
                continue;
            }

            dc.DrawLine(
                AxisPen,
                new Point(x, _plotRect.Bottom),
                new Point(x, _plotRect.Bottom + 4));
            var rate = _minRate + (col * _precisionStep);
            DrawText(
                dc,
                rate.ToString(rateFormat, CultureInfo.InvariantCulture),
                new Point(x - 18, _plotRect.Bottom + 6),
                10,
                FontWeights.Medium,
                SubtleTextBrush);
        }

        DrawText(
            dc,
            "기초대비사정률(%)",
            new Point(Math.Max(_plotRect.Left + 4, _plotRect.Right - 116), _plotRect.Bottom + 22),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            124);
        DrawText(
            dc,
            "공고(개찰일시)",
            new Point(6, Math.Max(4, _plotRect.Top - 16)),
            10,
            FontWeights.SemiBold,
            SubtleTextBrush,
            Math.Max(80, _plotRect.Left - 12));
    }

    private void DrawRecommendedLabel(DrawingContext dc, RecommendationSnapshot recommendation)
    {
        var header = BuildDisplayRecommendationHeader(recommendation);
        var detail =
            $"{FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}% · {FormatDecisionCategory(recommendation.DecisionCategory)} · 앵커 {recommendation.AnchorCompany} · 좌측여유 {FormatGap(recommendation.LeftSpaceWidth)}";
        DrawText(
            dc,
            header,
            new Point(Math.Max(8, _plotRect.Right - 190), Math.Max(4, _plotRect.Top - 16)),
            11,
            FontWeights.SemiBold,
            RecommendationPrimaryTextBrush,
            188);
        DrawText(
            dc,
            detail,
            new Point(Math.Max(8, _plotRect.Right - 260), Math.Max(18, _plotRect.Top - 2)),
            9,
            FontWeights.Medium,
            SubtleTextBrush,
            258);
    }

    private void DrawHoveredRateGuide(DrawingContext dc)
    {
        if (!_hoverRate.HasValue || !_hoverPlotPosition.HasValue || _plotRect == Rect.Empty)
        {
            return;
        }

        var pointerX = Math.Clamp(_hoverPlotPosition.Value.X, _plotRect.Left + 2, _plotRect.Right - 2);
        var pointerY = Math.Clamp(_hoverPlotPosition.Value.Y, _plotRect.Top + 2, _plotRect.Bottom - 2);
        dc.DrawLine(PointerGuidePen, new Point(pointerX, _plotRect.Top + 1), new Point(pointerX, _plotRect.Bottom - 1));
        dc.DrawLine(PointerGuidePen, new Point(_plotRect.Left + 1, pointerY), new Point(_plotRect.Right - 1, pointerY));
        dc.DrawLine(RecommendationGuideStrongPen, new Point(pointerX - 8, pointerY), new Point(pointerX + 8, pointerY));
        dc.DrawLine(RecommendationGuideStrongPen, new Point(pointerX, pointerY - 8), new Point(pointerX, pointerY + 8));

        var lines = _hoverOverlayLines.Length > 0
            ? _hoverOverlayLines
            : [$"기초대비사정률 {FormatRate(_hoverRate.Value)}"];
        var longestLine = lines.Max(x => x?.Length ?? 0);
        var labelWidth = Math.Clamp(78 + (longestLine * 6.8), 120, 320);
        var labelHeight = 12 + (lines.Length * 16.0);
        var labelLeft = pointerX + 12;
        if (labelLeft + labelWidth > _plotRect.Right - 4)
        {
            labelLeft = pointerX - labelWidth - 12;
        }

        var labelTop = pointerY - labelHeight - 10;
        if (labelTop < _plotRect.Top + 4)
        {
            labelTop = pointerY + 10;
        }

        labelLeft = Math.Clamp(labelLeft, _plotRect.Left + 4, _plotRect.Right - labelWidth - 4);
        labelTop = Math.Clamp(labelTop, _plotRect.Top + 4, _plotRect.Bottom - labelHeight - 4);
        var labelRect = new Rect(labelLeft, labelTop, labelWidth, labelHeight);
        dc.DrawRoundedRectangle(PointerLabelBrush, RecommendationCalloutPen, labelRect, 6, 6);
        for (var i = 0; i < lines.Length; i++)
        {
            DrawText(
                dc,
                lines[i],
                new Point(labelRect.Left + 8, labelRect.Top + 4 + (i * 15.5)),
                i == 0 ? 9.8 : 9.0,
                i == 0 ? FontWeights.SemiBold : FontWeights.Medium,
                i == 0 ? PointerLabelTextBrush : SubtleTextBrush,
                labelRect.Width - 12);
        }
    }

    private void DrawRecommendationArrow(DrawingContext dc, RecommendationSnapshot recommendation, double cellWidth)
    {
        if (_plotRect == Rect.Empty || _rows.Count == 0 || _columnCount <= 0)
        {
            _recommendationHotRect = Rect.Empty;
            return;
        }

        var recommendedColumn = ToColumnIndex(recommendation.RecommendedRate);
        var targetX = _plotRect.Left + _offset.X + ((recommendedColumn + 0.5) * cellWidth * _scale);
        if (!IsFinite(targetX))
        {
            _recommendationHotRect = Rect.Empty;
            return;
        }

        var clampedX = Math.Clamp(targetX, _plotRect.Left + 10, _plotRect.Right - 10);
        var emphasis = _isRecommendationHovered ? 1.0 : 0.0;
        var tipY = _plotRect.Top + 26;
        var shaftTopY = _plotRect.Top + 4;
        var pulse = (Math.Sin(_pulsePhase) + 1.0) / 2.0;

        dc.DrawLine(
            _isRecommendationHovered ? RecommendationGuideStrongPen : RecommendationGuidePen,
            new Point(clampedX, _plotRect.Top + 1),
            new Point(clampedX, _plotRect.Bottom - 1));

        var ringY = Math.Clamp(_plotRect.Top + 18, _plotRect.Top + 14, _plotRect.Top + 24);
        var ringRadius = 8.0 + (emphasis * 3.0) + (pulse * 2.2);
        var ringCenter = new Point(clampedX, ringY);
        dc.DrawEllipse(
            RecommendationTargetFillBrush,
            RecommendationTargetGlowPen,
            ringCenter,
            ringRadius,
            ringRadius);
        dc.DrawEllipse(
            null,
            RecommendationTargetRingPen,
            ringCenter,
            Math.Max(6.0, ringRadius - 2.2),
            Math.Max(6.0, ringRadius - 2.2));

        dc.DrawLine(
            RecommendationArrowGlowPen,
            new Point(clampedX, shaftTopY),
            new Point(clampedX, tipY - 8));
        dc.DrawLine(
            RecommendationArrowPen,
            new Point(clampedX, shaftTopY),
            new Point(clampedX, tipY - 8));

        var arrowHead = new StreamGeometry();
        using (var ctx = arrowHead.Open())
        {
            ctx.BeginFigure(new Point(clampedX, tipY), true, true);
            ctx.LineTo(new Point(clampedX - 10.5, tipY - 14), true, false);
            ctx.LineTo(new Point(clampedX + 10.5, tipY - 14), true, false);
        }
        arrowHead.Freeze();
        dc.DrawGeometry(RecommendationArrowBrush, null, arrowHead);

        var label = BuildDisplayRecommendationHeader(recommendation);
        var rangeText = $"{FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}% · 기초대비사정율 · 클릭 적용";

        const double labelWidth = 216;
        const double labelHeight = 38;
        var labelLeft = clampedX + 12;
        if (labelLeft + labelWidth > _plotRect.Right - 3)
        {
            labelLeft = clampedX - labelWidth - 12;
        }

        labelLeft = Math.Clamp(labelLeft, _plotRect.Left + 3, _plotRect.Right - labelWidth - 3);
        var labelTop = Math.Clamp(_plotRect.Top + 4, 4, _plotRect.Bottom - labelHeight - 4);
        var labelRect = new Rect(labelLeft, labelTop, labelWidth, labelHeight);

        dc.DrawRoundedRectangle(
            _isRecommendationHovered ? RecommendationCalloutHoverBrush : RecommendationCalloutBrush,
            _isRecommendationHovered ? RecommendationCalloutHoverPen : RecommendationCalloutPen,
            labelRect,
            6,
            6);
        DrawText(
            dc,
            label,
            new Point(labelRect.Left + 8, labelRect.Top + 4),
            10.5,
            FontWeights.SemiBold,
            RecommendationPrimaryTextBrush,
            labelRect.Width - 14);
        DrawText(
            dc,
            rangeText,
            new Point(labelRect.Left + 8, labelRect.Top + 21),
            9,
            FontWeights.Medium,
            SubtleTextBrush,
            labelRect.Width - 14);

        var arrowHotRect = new Rect(clampedX - 20, shaftTopY - 8, 40, 44);
        _recommendationHotRect = Rect.Union(arrowHotRect, labelRect);
    }

    private void EnsureChipLayerCache(double cellWidth, double cellHeight)
    {
        if (_chipLayerCache is not null
            && _chipLayerCacheVersion == _prepareVersion
            && Math.Abs(_chipLayerCellSize - cellWidth) < 0.001)
        {
            return;
        }

        var group = new DrawingGroup();
        using (var ctx = group.Open())
        {
            foreach (var entry in _cellLookup)
            {
                var (row, col) = entry.Key;
                var bucket = entry.Value;
                for (var i = 0; i < bucket.Count; i++)
                {
                    var point = bucket[i];
                    var rect = BuildPointRect(row, col, i, cellWidth, cellHeight, 1.0);
                    ctx.DrawRectangle(
                        ResolveCompanyBrush(point.NormalizedCompanyName, point.CompanyName),
                        BlockBorderPen,
                        rect);
                    DrawShortText(
                        ctx,
                        BuildShortName(point.NormalizedCompanyName, point.CompanyName, 2),
                        rect);
                    if (point.Rank != 1)
                    {
                        continue;
                    }

                    ctx.DrawRectangle(null, RankHighlightPen, rect);
                    var badgeRect = new Rect(
                        rect.Right - Math.Max(6, rect.Width * 0.45),
                        rect.Top + 1,
                        Math.Max(6, rect.Width * 0.42),
                        Math.Max(6, rect.Height * 0.38));
                    ctx.DrawRoundedRectangle(RankBadgeBrush, null, badgeRect, 2, 2);
                }
            }
        }

        if (group.CanFreeze)
        {
            group.Freeze();
        }

        _chipLayerCache = group;
        _chipLayerCacheVersion = _prepareVersion;
        _chipLayerCellSize = cellWidth;
    }

    private void DrawShortText(DrawingContext dc, string text, Rect rect)
    {
        var displayText = CompanyNamePresentation.BuildShortLabel(text);
        if (string.IsNullOrWhiteSpace(displayText))
        {
            displayText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        if (displayText.Length > 2)
        {
            displayText = displayText[..2];
        }

        if (string.IsNullOrWhiteSpace(displayText)
            || !IsFinite(rect.Width)
            || !IsFinite(rect.Height)
            || !IsFinite(rect.Left)
            || !IsFinite(rect.Top)
            || rect.Width < 6
            || rect.Height < 6)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var maxW = Math.Max(2.0, rect.Width - 2.0);
        var maxH = Math.Max(2.0, rect.Height - 2.0);
        var fontSize = Math.Clamp(
            Math.Min(maxW / Math.Max(1.05, displayText.Length), maxH * 0.62),
            4.0,
            11.0);
        var sizeBucket = Math.Max(4, (int)Math.Round(fontSize * 2.0));
        var dpiBucket = Math.Max(1, (int)Math.Round(dpi * 100));
        var key = (displayText, sizeBucket, dpiBucket);
        if (!_shortLabelCache.TryGetValue(key, out var formatted))
        {
            if (_shortLabelCache.Count > 256)
            {
                _shortLabelCache.Clear();
            }

            formatted = new FormattedText(
                displayText,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                ScatterLabelTypeface,
                fontSize,
                Brushes.White,
                dpi)
            {
                MaxTextWidth = maxW,
                MaxTextHeight = maxH,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            _shortLabelCache[key] = formatted;
        }

        var x = rect.Left + Math.Max(0, (rect.Width - formatted.Width) / 2.0);
        var y = rect.Top + Math.Max(0, (rect.Height - formatted.Height) / 2.0);
        if (!IsFinite(x) || !IsFinite(y))
        {
            return;
        }

        dc.PushClip(new RectangleGeometry(rect));
        var origin = new Point(x, y);
        var outline = formatted.BuildGeometry(origin);
        if (outline is not null && !outline.IsEmpty())
        {
            dc.DrawGeometry(Brushes.White, ChipLabelOutlinePen, outline);
        }
        else
        {
            dc.DrawText(formatted, origin);
        }

        dc.Pop();
    }

    private void DrawMessage(DrawingContext dc, Rect area, string message)
    {
        var safeX = IsFinite(area.Width) ? Math.Max(8, area.Width / 2 - 120) : 8;
        var safeY = IsFinite(area.Height) ? Math.Max(8, area.Height / 2 - 8) : 8;
        var safeMaxWidth = IsFinite(area.Width) ? Math.Max(80, area.Width - 16) : 640;
        DrawText(
            dc,
            message,
            new Point(safeX, safeY),
            14,
            FontWeights.SemiBold,
            SubtleTextBrush,
            safeMaxWidth);
    }

    private void DrawText(
        DrawingContext dc,
        string text,
        Point point,
        double size,
        FontWeight weight,
        Brush brush,
        double maxWidth = double.PositiveInfinity)
    {
        if (!IsFinite(size) || size <= 0)
        {
            size = 10;
        }

        if (!IsFinite(point.X) || !IsFinite(point.Y))
        {
            point = new Point(0, 0);
        }

        var resolvedMaxWidth = maxWidth;
        if (double.IsNaN(resolvedMaxWidth)
            || double.IsInfinity(resolvedMaxWidth)
            || resolvedMaxWidth <= 0)
        {
            resolvedMaxWidth = 4096;
        }
        else if (resolvedMaxWidth > 50000)
        {
            resolvedMaxWidth = 50000;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var color = brush is SolidColorBrush solid
            ? (solid.Color.A << 24) | (solid.Color.R << 16) | (solid.Color.G << 8) | solid.Color.B
            : 0;
        var key = (
            text ?? string.Empty,
            Math.Max(1, (int)Math.Round(size * 10.0)),
            weight.ToOpenTypeWeight(),
            Math.Max(1, (int)Math.Min(resolvedMaxWidth, 50000)),
            Math.Max(1, (int)Math.Round(dpi * 100.0)),
            color);
        if (!_axisLabelCache.TryGetValue(key, out var formatted))
        {
            if (_axisLabelCache.Count > 512)
            {
                _axisLabelCache.Clear();
            }

            formatted = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, weight, FontStretches.Normal),
                size,
                brush,
                dpi)
            {
                MaxTextWidth = resolvedMaxWidth,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            _axisLabelCache[key] = formatted;
        }

        dc.DrawText(formatted, point);
    }

    private void UpdateHoverState(Point position)
    {
        if (_plotRect == Rect.Empty || !_plotRect.Contains(position) || Mode != NativeBidChartMode.Scatter)
        {
            if (_hoverCompany is not null)
            {
                _hoverCompany = null;
                ToolTip = null;
                InvalidateVisual();
            }
            return;
        }

        var worldPoint = ToWorldPoint(position);
        var (cellWidth, cellHeight) = GetScatterCellSize();
        var row = (int)Math.Floor(worldPoint.Y / cellHeight);
        var col = (int)Math.Floor(worldPoint.X / cellWidth);
        if (row < 0 || row >= _rows.Count || col < 0 || col >= _columnCount)
        {
            if (_hoverCompany is not null)
            {
                _hoverCompany = null;
                ToolTip = null;
                InvalidateVisual();
            }
            return;
        }

        var hoveredRow = _rows[row];
        var awardedPoint = hoveredRow.AwardedPoint
            ?? hoveredRow.Points.FirstOrDefault(x => x.IsAwarded || x.Rank == 1);
        var currentRate = _hoverRate ?? ToRateFromColumn(col);

        if (!_cellLookup.TryGetValue((row, col), out var bucket) || bucket.Count == 0)
        {
            var rowLines = BuildScatterHoverLines(
                currentRate,
                hoveredRow.NoticeNumber,
                awardedPoint,
                null);
            var rowTooltip = string.Join(Environment.NewLine, rowLines);
            var rowChanged = _hoverCompany is not null
                             || ToolTip is null
                             || !string.Equals(ToolTip.ToString(), rowTooltip, StringComparison.Ordinal);
            _hoverCompany = null;
            ToolTip = rowTooltip;
            _hoverOverlayLines = rowLines;
            if (rowChanged)
            {
                InvalidateVisual();
            }
            return;
        }

        var target = ResolveHoveredParticipant(bucket, row, col, cellWidth, cellHeight);
        var nextHoverCompany = string.IsNullOrWhiteSpace(target.CompanyName)
            ? target.NormalizedCompanyName
            : target.CompanyName;

        var hoverLines = BuildScatterHoverLines(
            currentRate,
            hoveredRow.NoticeNumber,
            awardedPoint,
            target);
        var tooltipText = string.Join(Environment.NewLine, hoverLines);
        ToolTip = tooltipText;
        _hoverOverlayLines = hoverLines;

        if (!string.Equals(_hoverCompany, nextHoverCompany, StringComparison.OrdinalIgnoreCase))
        {
            _hoverCompany = nextHoverCompany;
            InvalidateVisual();
        }
    }

    private bool UpdateRecommendationHover(Point position)
    {
        if (ResolveDisplayRecommendation() is null)
        {
            _isRecommendationHovered = false;
            ToolTip = null;
            return false;
        }

        var hovered = Mode == NativeBidChartMode.Scatter
                      && _recommendationHotRect != Rect.Empty
                      && _recommendationHotRect.Contains(position);
        if (!hovered)
        {
            if (_isRecommendationHovered)
            {
                _isRecommendationHovered = false;
                ToolTip = null;
            }

            return false;
        }

        _isRecommendationHovered = true;
        ToolTip = BuildRecommendationTooltip(_lastRecommendation);
        return true;
    }

    private void UpdateHoveredRate(Point position)
    {
        if (_plotRect == Rect.Empty || !_plotRect.Contains(position))
        {
            ClearHoveredRate();
            return;
        }

        var worldPoint = ToWorldPoint(position);
        if (!IsFinite(worldPoint.X) || !IsFinite(worldPoint.Y))
        {
            ClearHoveredRate();
            return;
        }

        double? rate = Mode switch
        {
            NativeBidChartMode.Scatter => ResolveScatterHoveredRate(worldPoint),
            NativeBidChartMode.Distribution => ResolveDistributionHoveredRate(worldPoint),
            NativeBidChartMode.Trend => ResolveTrendHoveredRate(position),
            _ => null,
        };
        if (!rate.HasValue)
        {
            ClearHoveredRate();
            return;
        }

        var changed = !_hoverRate.HasValue
                      || Math.Abs(_hoverRate.Value - rate.Value) > (_precisionStep / 5.0)
                      || !_hoverPlotPosition.HasValue
                      || Math.Abs(_hoverPlotPosition.Value.X - position.X) > 1.0
                      || Math.Abs(_hoverPlotPosition.Value.Y - position.Y) > 1.0;

        _hoverRate = rate.Value;
        _hoverPlotPosition = position;
        if (Mode != NativeBidChartMode.Scatter)
        {
            _hoverOverlayLines =
            [
                $"기초대비사정률 {FormatRate(rate.Value)}",
            ];
            ToolTip = _hoverOverlayLines[0];
        }

        if (changed)
        {
            if (ScatterRenderCache.ShouldRaiseHoveredRate(_lastRaisedHoverRate, rate.Value, _precisionStep))
            {
                _lastRaisedHoverRate = rate.Value;
                HoveredRateChanged?.Invoke(this, new HoveredRateChangedEventArgs(Math.Round(rate.Value, 5)));
            }

            InvalidateVisual();
        }
    }

    private void ClearHoveredRate()
    {
        if (!_hoverRate.HasValue && !_hoverPlotPosition.HasValue)
        {
            return;
        }

        _hoverRate = null;
        _hoverPlotPosition = null;
        _lastRaisedHoverRate = null;
        _hoverOverlayLines = [];
        HoveredRateChanged?.Invoke(this, new HoveredRateChangedEventArgs(null));
    }

    private string[] BuildScatterHoverLines(
        double currentRate,
        string noticeNumber,
        ParticipantRecord? awardedPoint,
        ParticipantRecord? hoveredPoint)
    {
        var displayRecommendation = ResolveDisplayRecommendation();
        var lines = new List<string>
        {
            $"기초대비사정률 {FormatRate(currentRate)}",
            $"공고 {noticeNumber}",
        };

        if (displayRecommendation is not null)
        {
            lines.Add($"추천 {FormatRate(displayRecommendation.RecommendedRate)}");
        }

        if (awardedPoint?.AdjustRate is double awardRate)
        {
            var awardCompany = string.IsNullOrWhiteSpace(awardedPoint.NormalizedCompanyName)
                ? awardedPoint.CompanyName
                : awardedPoint.NormalizedCompanyName;
            var rangeSuffix = awardRate < _minRate || awardRate > _maxRate
                ? " · 범위밖"
                : string.Empty;
            lines.Add($"1위업체 {FormatRate(awardRate)} · {awardCompany}{rangeSuffix}");
        }
        else
        {
            lines.Add("1위업체 -");
        }

        if (hoveredPoint is not null)
        {
            var companyName = string.IsNullOrWhiteSpace(hoveredPoint.NormalizedCompanyName)
                ? hoveredPoint.CompanyName
                : hoveredPoint.NormalizedCompanyName;
            var bidAmountText = hoveredPoint.BidAmount.HasValue
                ? $"{hoveredPoint.BidAmount.Value:N0}원"
                : "금액없음";
            var bidRate = hoveredPoint.AdjustRate ?? currentRate;
            lines.Add($"{companyName} · {FormatRate(bidRate)} · {bidAmountText}");
        }

        return lines.ToArray();
    }

    private ParticipantRecord ResolveHoveredParticipant(
        IReadOnlyList<ParticipantRecord> bucket,
        int row,
        int col,
        double cellWidth,
        double cellHeight)
    {
        var progress = _animateDiff
            ? Math.Clamp((DateTime.UtcNow - _animationStartedAtUtc).TotalMilliseconds / 320.0, 0.0, 1.0)
            : 1.0;
        var worldPoint = ToWorldPoint(_hoverPlotPosition ?? new Point());
        return HitFrontmostChip(bucket, row, col, cellWidth, cellHeight, progress, worldPoint)
            ?? bucket[^1];
    }

    private double? ResolveScatterHoveredRate(Point worldPoint)
    {
        var (cellWidth, _) = GetScatterCellSize();
        if (!IsFinite(cellWidth) || cellWidth <= 0)
        {
            return null;
        }

        return Math.Clamp(_minRate + ((worldPoint.X / cellWidth) * _precisionStep), _minRate, _maxRate);
    }

    private double? ResolveDistributionHoveredRate(Point worldPoint)
    {
        if (_columnCount <= 0)
        {
            return null;
        }

        var worldWidth = GetDistributionWorldWidth();
        var barWidth = worldWidth / Math.Max(1, _columnCount);
        if (!IsFinite(barWidth) || barWidth <= 0)
        {
            return null;
        }

        return Math.Clamp(_minRate + ((worldPoint.X / barWidth) * _precisionStep), _minRate, _maxRate);
    }

    private double? ResolveTrendHoveredRate(Point position)
    {
        var ySpan = Math.Max(0.1, _maxRate - _minRate);
        var ratio = 1.0 - ((position.Y - _plotRect.Top) / Math.Max(1.0, _plotRect.Height));
        return Math.Clamp(_minRate + (ratio * ySpan), _minRate, _maxRate);
    }

    private bool TryApplyRecommendedRange(Point position)
    {
        var displayRecommendation = ResolveDisplayRecommendation();
        if (Mode != NativeBidChartMode.Scatter
            || displayRecommendation is null
            || _recommendationHotRect == Rect.Empty
            || !_recommendationHotRect.Contains(position))
        {
            return false;
        }

        var min = Math.Clamp(Math.Min(displayRecommendation.BandMin, displayRecommendation.BandMax), _minRate, _maxRate);
        var max = Math.Clamp(Math.Max(displayRecommendation.BandMin, displayRecommendation.BandMax), _minRate, _maxRate);
        RateRangeSelected?.Invoke(this, new RateRangeSelectedEventArgs(min, max));
        ToolTip = $"{BuildRecommendationTooltip(displayRecommendation)}\n추천 구간을 필터로 적용했습니다.";
        return true;
    }

    private string BuildRecommendationTooltip(RecommendationSnapshot recommendation)
    {
        return
            $"조건부우위 분석\n판단: {FormatDecisionCategory(recommendation.DecisionCategory)} · 불확실성 {FormatUncertainty(recommendation.UncertaintyLevel)}\n추천값: {FormatRate(recommendation.RecommendedRate)}%\n권장구간: {FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}%\n충돌앵커: {recommendation.AnchorCompany}\n앵커간격: {FormatGap(recommendation.AnchorGap)} · 좌측여유: {FormatGap(recommendation.LeftSpaceWidth)}\n종합점수: {recommendation.EvidenceScore:0.#}\n- 고정성 점수: {recommendation.ShadowScore:0.#}\n- 공백지속 점수: {recommendation.GapOpportunityScore:0.#}\n- 과열회피 점수: {recommendation.CrowdAvoidanceScore:0.#}\n근거 공고: {recommendation.SupportingNoticeCount:N0}건 · 참고 낙찰공고: {recommendation.AwardNoticeCount:N0}건 · 경쟁 표본: {recommendation.CompetitorSampleCount:N0}건 · 구간 표본: {recommendation.BandCompetitorCount:N0}건\n사유: {recommendation.ReasonSummary}";
    }

    private void TryEmitCompanyPick(Point position)
    {
        if (_plotRect == Rect.Empty || !_plotRect.Contains(position) || Mode != NativeBidChartMode.Scatter)
        {
            return;
        }

        var worldPoint = ToWorldPoint(position);
        var (cellWidth, cellHeight) = GetScatterCellSize();
        var row = (int)Math.Floor(worldPoint.Y / cellHeight);
        var col = (int)Math.Floor(worldPoint.X / cellWidth);
        if (_cellLookup.TryGetValue((row, col), out var bucket) && bucket.Count > 0)
        {
            var progress = _animateDiff
                ? Math.Clamp((DateTime.UtcNow - _animationStartedAtUtc).TotalMilliseconds / 320.0, 0.0, 1.0)
                : 1.0;
            var hit = HitFrontmostChip(bucket, row, col, cellWidth, cellHeight, progress, worldPoint)
                ?? bucket[^1];
            var hitName = !string.IsNullOrWhiteSpace(hit.CompanyName)
                ? hit.CompanyName
                : hit.NormalizedCompanyName;
            if (!string.IsNullOrWhiteSpace(hitName))
            {
                CompanyPicked?.Invoke(this, new CompanyPickedEventArgs(hitName.Trim()));
            }
        }
    }

    private static ParticipantRecord? HitFrontmostChip(
        IReadOnlyList<ParticipantRecord> bucket,
        int row,
        int col,
        double cellWidth,
        double cellHeight,
        double progress,
        Point worldPoint)
    {
        for (var i = bucket.Count - 1; i >= 0; i--)
        {
            var rect = BuildPointRect(row, col, i, cellWidth, cellHeight, progress);
            if (rect.Contains(worldPoint))
            {
                return bucket[i];
            }
        }

        return null;
    }

    private void EmitRangeSelection(Rect selection)
    {
        if (_plotRect == Rect.Empty || Mode != NativeBidChartMode.Scatter)
        {
            return;
        }

        var p1 = ToWorldPoint(selection.TopLeft);
        var p2 = ToWorldPoint(selection.BottomRight);
        var (cellWidth, _) = GetScatterCellSize();
        var c1 = (int)Math.Floor(Math.Min(p1.X, p2.X) / cellWidth);
        var c2 = (int)Math.Ceiling(Math.Max(p1.X, p2.X) / cellWidth);
        c1 = Math.Clamp(c1, 0, _columnCount - 1);
        c2 = Math.Clamp(c2, 0, _columnCount - 1);
        var minRate = _minRate + (c1 * _precisionStep);
        var maxRate = _minRate + (c2 * _precisionStep);
        RateRangeSelected?.Invoke(this, new RateRangeSelectedEventArgs(minRate, maxRate));
    }

    private void EndInteraction(bool invalidateVisual)
    {
        _interactionState = ChartInteractionState.None;
        _isPanActive = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (invalidateVisual)
        {
            InvalidateVisual();
        }
    }

    private Point ToWorldPoint(Point controlPoint)
    {
        var safeScale = (!IsFinite(_scale) || _scale <= 0.0001) ? 1.0 : _scale;
        var safeOffsetX = IsFinite(_offset.X) ? _offset.X : 0.0;
        var safeOffsetY = IsFinite(_offset.Y) ? _offset.Y : 0.0;
        var safePlotLeft = IsFinite(_plotRect.Left) ? _plotRect.Left : 0.0;
        var safePlotTop = IsFinite(_plotRect.Top) ? _plotRect.Top : 0.0;
        var x = (controlPoint.X - safePlotLeft - safeOffsetX) / safeScale;
        var y = (controlPoint.Y - safePlotTop - safeOffsetY) / safeScale;
        return new Point(x, y);
    }

    private static Rect BuildPointRect(
        int row,
        int col,
        int index,
        double cellWidth,
        double cellHeight,
        double animationProgress)
    {
        var side = Math.Max(2, Math.Min(cellWidth, cellHeight) - 2.0);
        var left = col * cellWidth + ((cellWidth - side) / 2.0) + ((index % 2) * 0.8);
        var top = row * cellHeight + ((cellHeight - side) / 2.0) + (((index / 2) % 2) * 0.8);
        var rect = new Rect(
            left,
            top,
            side,
            side);

        if (animationProgress < 1.0)
        {
            rect.Y += (1.0 - animationProgress) * 10.0;
        }

        return rect;
    }

    private double GetTrendWorldWidth(int maxPointCount)
    {
        var pointSpacing = _precisionStep <= 0.0015 ? 18.0 : 10.0;
        return Math.Max(_plotRect.Width, Math.Max(1, maxPointCount - 1) * pointSpacing);
    }

    private double GetDistributionWorldWidth()
    {
        var barWidth = _precisionStep <= 0.0015 ? 4.0 : 1.0;
        return Math.Max(_plotRect.Width, _columnCount * barWidth);
    }

    private double ToHorizontalViewportX(double worldX)
    {
        var safeScale = (!IsFinite(_scale) || _scale <= 0.0001) ? 1.0 : _scale;
        var safeOffsetX = IsFinite(_offset.X) ? _offset.X : 0.0;
        return _plotRect.Left + safeOffsetX + (worldX * safeScale);
    }

    private (double CellWidth, double CellHeight) GetScatterCellSize()
    {
        var size = _precisionStep <= 0.0015 ? 16.0 : 20.0;
        return (size, size);
    }

    private bool IsAllCompanySelected()
    {
        var selectedTokens = SplitSelectedCompanyTokens(SelectedCompany);
        return selectedTokens.Count == 0 || selectedTokens.Any(IsAllCompanyToken);
    }

    private static List<string> SplitSelectedCompanyTokens(string? selectedCompany)
    {
        if (string.IsNullOrWhiteSpace(selectedCompany))
        {
            return [];
        }

        return selectedCompany
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsAllCompanyToken(string token)
    {
        return string.Equals(token, "전체", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, AllCompanyOption, StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, LegacyAllCompanyOption, StringComparison.OrdinalIgnoreCase);
    }

    private RecommendationSnapshot ComputeRecommendationSnapshot()
    {
        return BuildRecommendationSnapshot(_recommendationPoints, _minRate, _maxRate);
    }

    private static RecommendationSnapshot BuildRecommendationSnapshot(
        IReadOnlyList<ParticipantRecord> filteredPoints,
        double minRate,
        double maxRate)
    {
        var result = RecommendationEngine.Value.Analyze(filteredPoints, minRate, maxRate);
        return new RecommendationSnapshot(
            RecommendedRate: result.RecommendedRate,
            BandMin: result.BandMin,
            BandMax: result.BandMax,
            CompetitorSampleCount: result.CompetitorSampleCount,
            BandCompetitorCount: result.BandCompetitorCount,
            SupportingNoticeCount: result.SupportingNoticeCount,
            AwardNoticeCount: result.AwardNoticeCount,
            ShadowScore: result.FixedScore,
            MarginScore: result.AnchorScore,
            CompetitionScore: result.DensityPenalty,
            WinScore: 0,
            GapOpportunityScore: result.GapOpportunityScore,
            CrowdAvoidanceScore: result.CrowdAvoidanceScore,
            EvidenceScore: result.EvidenceScore,
            DecisionCategory: result.DecisionCategory,
            UncertaintyLevel: result.UncertaintyLevel,
            AnchorCompany: result.AnchorCompany,
            AnchorGap: result.AnchorGap,
            LeftSpaceWidth: result.LeftSpaceWidth,
            ReasonSummary: result.ReasonSummary);
    }

    private string FormatRate(double rate)
    {
        var format = _precisionStep <= 0.0015 ? "F3" : "F2";
        return rate.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatDecisionCategory(string? category)
    {
        return (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "candidate" => "좌측선점 후보",
            "forbidden" => "금지구간",
            "boundary" => "경계구간",
            _ => "미분류",
        };
    }

    private static string FormatUncertainty(string? uncertainty)
    {
        return (uncertainty ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "low" => "낮음",
            "medium" => "중간",
            "high" => "높음",
            _ => "미상",
        };
    }

    private static string FormatGap(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
    }

    private int ToColumnIndex(double rate)
    {
        return ToColumnIndex(rate, _minRate, _precisionStep, _columnCount);
    }

    private static int ToColumnIndex(double rate, double minRate, double precisionStep, int columnCount)
    {
        var raw = (rate - minRate) / precisionStep;
        var col = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(col, 0, Math.Max(0, columnCount - 1));
    }

    private double ToRateFromColumn(int columnIndex)
    {
        var clamped = Math.Clamp(columnIndex, 0, Math.Max(0, _columnCount - 1));
        return _minRate + (clamped * _precisionStep);
    }

    private static bool ContainsToken(string? source, string token)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var left = NormalizeCompanyToken(source);
        var right = NormalizeCompanyToken(token);
        return left.Contains(right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCompanyToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
    }

    private int ResolveNameCharLimit(double cellWidth, double cellHeight, int visibleCellEstimate)
    {
        return 2;
    }

    private static string BuildShortName(string? normalizedName, string? rawName, int maxChars)
    {
        var source = string.IsNullOrWhiteSpace(normalizedName) ? rawName : normalizedName;
        if (IsDonggukCompany(source))
        {
            return "동국";
        }

        var label = CompanyNamePresentation.BuildShortLabel(source);
        if (!string.IsNullOrWhiteSpace(label))
        {
            return maxChars > 0 && label.Length > maxChars ? label[..maxChars] : label;
        }

        return "--";
    }

    private static double ParsePrecision(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            if (!IsFinite(parsed))
            {
                return 0.01;
            }
            if (parsed <= 0.0015)
            {
                return 0.001;
            }

            return 0.01;
        }

        return 0.01;
    }

    private static double ParseRate(string? value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            if (IsFinite(parsed))
            {
                return parsed;
            }
        }

        if (double.TryParse(value, out parsed))
        {
            if (IsFinite(parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private void NormalizeViewState()
    {
        if (!IsFinite(_scale) || _scale <= 0)
        {
            _scale = 1.0;
        }

        _scale = Math.Clamp(_scale, 0.25, 5.0);

        if (!IsFinite(_offset.X) || !IsFinite(_offset.Y))
        {
            _offset = new Vector(12, 12);
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private Color ResolveCompanyColor(string? normalizedCompanyName, string? companyName)
    {
        var key = string.IsNullOrWhiteSpace(normalizedCompanyName) ? companyName ?? string.Empty : normalizedCompanyName;
        key = key.Trim();
        if (key.Length == 0)
        {
            return Colors.SlateGray;
        }

        if (IsDonggukCompany(key))
        {
            return DonggukFillColor;
        }

        if (_companyColorCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var normalizedKey = NormalizeCompanyToken(key);
        if (FixedCompanyColors.Value.TryGetValue(normalizedKey, out var fixedColor))
        {
            _companyColorCache[key] = fixedColor;
            return fixedColor;
        }

        unchecked
        {
            var hash = 23;
            foreach (var ch in key)
            {
                hash = (hash * 31) + ch;
            }

            var paletteIndex = Math.Abs(hash % DistinctCompanyPalette.Length);
            var color = DistinctCompanyPalette[paletteIndex];

            // 팔레트 순환 시 밝기 변형으로 추가 대비 확보
            var cycle = Math.Abs(hash / DistinctCompanyPalette.Length) % 3;
            if (cycle == 1)
            {
                color = ScaleRgb(color, 0.85);
            }
            else if (cycle == 2)
            {
                color = ScaleRgb(color, 1.10);
            }

            // 동국(빨강)과 유사한 색은 회피
            if (GetColorDistance(color, DonggukFillColor) < 70)
            {
                color = DistinctCompanyPalette[(paletteIndex + 9) % DistinctCompanyPalette.Length];
            }

            _companyColorCache[key] = color;
            return color;
        }
    }

    private static Color ScaleRgb(Color color, double scale)
    {
        byte ToByte(byte c) => (byte)Math.Clamp((int)Math.Round(c * scale), 0, 255);
        return Color.FromRgb(ToByte(color.R), ToByte(color.G), ToByte(color.B));
    }

    private static double GetColorDistance(Color a, Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    private SolidColorBrush ResolveCompanyBrush(string? normalizedCompanyName, string? companyName)
    {
        var key = string.IsNullOrWhiteSpace(normalizedCompanyName) ? companyName ?? string.Empty : normalizedCompanyName;
        key = key.Trim();
        if (key.Length == 0)
        {
            return Brushes.SlateGray;
        }

        if (_companyBrushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var color = ResolveCompanyColor(normalizedCompanyName, companyName);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _companyBrushCache[key] = brush;
        return brush;
    }

    private static bool IsDonggukCompany(string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return false;
        }

        var normalized = NormalizeCompanyToken(companyName);
        var ownKeyword = NormalizeCompanyToken(AnalysisProfile.Value.MyCompanyKeyword);
        return ownKeyword.Length > 0 && normalized.Contains(ownKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedCompany(string? normalizedCompanyName, string? companyName)
    {
        var normalized = NormalizeCompanyToken(normalizedCompanyName);
        var raw = NormalizeCompanyToken(companyName);
        foreach (var keyword in AnalysisProfile.Value.ExcludedCompanyKeywords)
        {
            var token = NormalizeCompanyToken(keyword);
            if (token.Length == 0)
            {
                continue;
            }

            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase)
                || raw.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetCompanyGroupKey(ParticipantRecord row)
    {
        return string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName;
    }

    private int ResolveSelectedCompanyOrder(string? companyKey)
    {
        if (string.IsNullOrWhiteSpace(companyKey))
        {
            return int.MaxValue;
        }

        var selectedTokens = SplitSelectedCompanyTokens(SelectedCompany)
            .Where(x => !IsAllCompanyToken(x))
            .Select(NormalizeCompanyToken)
            .ToList();
        if (selectedTokens.Count == 0)
        {
            return 0;
        }

        var normalizedKey = NormalizeCompanyToken(companyKey);
        for (var i = 0; i < selectedTokens.Count; i++)
        {
            if (normalizedKey.Contains(selectedTokens[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static IReadOnlyDictionary<string, Color> LoadFixedCompanyColors()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "bidder_colors.json");
            if (!File.Exists(path))
            {
                return new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            }

            var output = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!property.Value.TryGetProperty("color", out var colorElement))
                {
                    continue;
                }

                var hex = colorElement.GetString();
                if (string.IsNullOrWhiteSpace(hex))
                {
                    continue;
                }

                var normalizedName = NormalizeCompanyToken(property.Name);
                if (normalizedName.Length == 0)
                {
                    continue;
                }

                output[normalizedName] = (Color)ColorConverter.ConvertFromString(hex)!;
            }

            return output;
        }
        catch
        {
            return new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Pen CreateTransientPen(Color color, double thickness)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private RecommendationSnapshot? ResolveDisplayRecommendation()
    {
        if (PinnedRecommendationBandMin.HasValue && PinnedRecommendationBandMax.HasValue)
        {
            var bandMin = Math.Min(PinnedRecommendationBandMin.Value, PinnedRecommendationBandMax.Value);
            var bandMax = Math.Max(PinnedRecommendationBandMin.Value, PinnedRecommendationBandMax.Value);
            return _lastRecommendation with
            {
                RecommendedRate = (bandMin + bandMax) / 2.0,
                BandMin = bandMin,
                BandMax = bandMax,
                DecisionCategory = "candidate",
                ReasonSummary = string.IsNullOrWhiteSpace(PinnedRecommendationText)
                    ? _lastRecommendation.ReasonSummary
                    : PinnedRecommendationText.Trim(),
            };
        }

        return HasActionableRecommendation(_lastRecommendation)
            ? _lastRecommendation
            : null;
    }

    private string BuildDisplayRecommendationHeader(RecommendationSnapshot recommendation)
    {
        var pinned = PinnedRecommendationText?.Trim();
        if (!string.IsNullOrWhiteSpace(pinned)
            && !pinned.Contains("추천 밴드 없음", StringComparison.Ordinal))
        {
            return pinned;
        }

        return $"1순위 추천 {FormatRate(recommendation.RecommendedRate)}";
    }

    internal static List<ParticipantRecord> SortCellBucketForDisplay(IEnumerable<ParticipantRecord> bucket)
    {
        return ScatterChartRules.SortOverlappingChips(bucket);
    }

    public void ResetMapToCenter()
    {
        EndInteraction(invalidateVisual: false);
        _hoverCompany = null;
        _isRecommendationHovered = false;
        ClearHoveredRate();
        ToolTip = null;
        _scale = 1.0;
        if (_plotRect.Width > 1 && _rows.Count > 0 && _columnCount > 0)
        {
            var (cellWidth, cellHeight) = GetScatterCellSize();
            var worldWidth = _columnCount * cellWidth;
            var worldHeight = _rows.Count * cellHeight;
            _offset = new Vector(
                (_plotRect.Width - worldWidth) / 2.0,
                (_plotRect.Height - worldHeight) / 2.0);
        }
        else
        {
            _offset = new Vector(12, 12);
        }

        InvalidateVisual();
    }

    private static bool HasActionableRecommendation(RecommendationSnapshot recommendation)
    {
        if (!string.Equals(recommendation.DecisionCategory, "candidate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (recommendation.BandMax <= recommendation.BandMin)
        {
            return false;
        }

        if (recommendation.CompetitorSampleCount <= 0
            && recommendation.SupportingNoticeCount <= 0
            && recommendation.AwardNoticeCount <= 0)
        {
            return false;
        }

        return true;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(((hue / 60.0) % 2) - 1));
        var m = value - c;

        (double r, double g, double b) = hue switch
        {
            >= 0 and < 60 => (c, x, 0.0),
            >= 60 and < 120 => (x, c, 0.0),
            >= 120 and < 180 => (0.0, c, x),
            >= 180 and < 240 => (0.0, x, c),
            >= 240 and < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round((v + m) * 255.0), 0, 255);
        return Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));
    }

    private static Pen CreatePen(string hex, double thickness)
    {
        var brush = CreateBrush(hex);
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateRoundPen(string hex, double thickness)
    {
        var brush = CreateBrush(hex);
        var pen = new Pen(brush, thickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            MiterLimit = 2.0,
        };
        pen.Freeze();
        return pen;
    }

    private static Pen CreateDashedPen(string hex, double thickness, double dash, double gap)
    {
        var brush = CreateBrush(hex);
        var pen = new Pen(brush, thickness)
        {
            DashStyle = new DashStyle(new DoubleCollection { dash, gap }, 0),
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void HandleAnimationFrame(object? sender, EventArgs e)
    {
        var progress = Math.Clamp((DateTime.UtcNow - _animationStartedAtUtc).TotalMilliseconds / 320.0, 0.0, 1.0);
        if (progress >= 1.0)
        {
            CompositionTarget.Rendering -= HandleAnimationFrame;
            _animationFrameHooked = false;
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastFrameInvalidatedAtUtc) < TimeSpan.FromMilliseconds(33))
        {
            return;
        }

        _lastFrameInvalidatedAtUtc = now;
        InvalidateVisual();
    }

    private void HandlePulseTick(object? sender, EventArgs e)
    {
        if (!IsVisible || HighPerformanceMode || Mode != NativeBidChartMode.Scatter)
        {
            _pulseTimer.Stop();
            return;
        }

        _pulsePhase += 0.45;
        if (_pulsePhase > Math.PI * 2)
        {
            _pulsePhase -= Math.PI * 2;
        }

        InvalidateVisual();
    }

    private void UpdatePulseTimer()
    {
        if (IsVisible && Mode == NativeBidChartMode.Scatter && !HighPerformanceMode)
        {
            if (!_pulseTimer.IsEnabled)
            {
                _pulseTimer.Start();
            }

            return;
        }

        _pulseTimer.Stop();
    }

    private sealed record RecommendationSnapshot(
        double RecommendedRate,
        double BandMin,
        double BandMax,
        int CompetitorSampleCount,
        int BandCompetitorCount,
        int SupportingNoticeCount,
        int AwardNoticeCount,
        double ShadowScore,
        double MarginScore,
        double CompetitionScore,
        double WinScore,
        double GapOpportunityScore,
        double CrowdAvoidanceScore,
        double EvidenceScore,
        string DecisionCategory,
        string UncertaintyLevel,
        string AnchorCompany,
        double? AnchorGap,
        double? LeftSpaceWidth,
        string ReasonSummary)
    {
        public static RecommendationSnapshot Empty => new(
            RecommendedRate: 100.0,
            BandMin: 99.99,
            BandMax: 100.01,
            CompetitorSampleCount: 0,
            BandCompetitorCount: 0,
            SupportingNoticeCount: 0,
            AwardNoticeCount: 0,
            ShadowScore: 0,
            MarginScore: 0,
            CompetitionScore: 0,
            WinScore: 0,
            GapOpportunityScore: 0,
            CrowdAvoidanceScore: 0,
            EvidenceScore: 0,
            DecisionCategory: "boundary",
            UncertaintyLevel: "high",
            AnchorCompany: string.Empty,
            AnchorGap: null,
            LeftSpaceWidth: null,
            ReasonSummary: "조건부우위 후보 없음");
    }

    private sealed record PreparedChartState(
        IReadOnlyList<ParticipantRecord> FilteredPoints,
        IReadOnlyList<ParticipantRecord> RecommendationPoints,
        IReadOnlyList<RowInfo> Rows,
        IReadOnlyDictionary<(int RowIndex, int ColumnIndex), List<ParticipantRecord>> CellLookup,
        RecommendationSnapshot Recommendation,
        double PrecisionStep,
        double MinRate,
        double MaxRate,
        int ColumnCount,
        int LabelEvery,
        bool AnimateDiff);

    private sealed record RowInfo(
        string NoticeNumber,
        DateTime OpenDate,
        ParticipantRecord? AwardedPoint,
        IReadOnlyList<ParticipantRecord> Points);
}
