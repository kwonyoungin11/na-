using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NaraCollector.Desktop.Models;

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
    private const string DonggukCompanyKeyword = "동국문화재연구원";
    private const double DragActivationThreshold = 4.0;
    private static readonly string[] ExcludedCompanyKeywords = ["한국문화재연구원", "효림"];
    private static readonly Color DonggukFillColor = Color.FromRgb(0xD3, 0x2F, 0x2F);
    private static readonly object RenderErrorLogSync = new();
    private static readonly Pen GridPen = CreatePen("#D9DEE7", 1.0);
    private static readonly Pen MajorGridPen = CreatePen("#BFC7D4", 1.0);
    private static readonly Pen AxisPen = CreatePen("#5E6A7D", 1.2);
    private static readonly Pen SelectionPen = CreatePen("#FFB84A", 1.4);
    private static readonly Pen BlockBorderPen = CreatePen("#21252B", 0.8);
    private static readonly Pen RankHighlightPen = CreatePen("#B91C1C", 2.2);
    private static readonly Pen RankHighlightOuterPen = CreatePen("#7F1D1D", 3.2);
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
    private static readonly Pen DistributionCenterPen = CreatePen("#F59E0B", 1.6);
    private static readonly Brush BackgroundBrush = CreateBrush("#F6F8FB");
    private static readonly Brush PlotBackgroundBrush = CreateBrush("#FFFFFF");
    private static readonly Brush RecommendationBandBrush = CreateBrush("#2AFDE68A");
    private static readonly Brush RecommendationArrowBrush = CreateBrush("#F97316");
    private static readonly Brush RecommendationCalloutBrush = CreateBrush("#FFF7ED");
    private static readonly Brush RecommendationCalloutHoverBrush = CreateBrush("#FFEDD5");
    private static readonly Brush RecommendationPrimaryTextBrush = CreateBrush("#9A3412");
    private static readonly Brush RecommendationTargetFillBrush = CreateBrush("#FFF3E0");
    private static readonly Brush DistributionMajorBarBrush = CreateBrush("#6EA8FE");
    private static readonly Brush DistributionMinorBarBrush = CreateBrush("#9CC4FF");
    private static readonly Brush TextBrush = CreateBrush("#111111");
    private static readonly Brush SubtleTextBrush = CreateBrush("#5A6473");
    private static readonly Brush RankHighlightFillBrush = CreateBrush("#55FCA5A5");
    private static readonly Brush RankBadgeBrush = CreateBrush("#C62828");
    private static readonly Brush RankBadgeTextBrush = Brushes.White;

    private readonly Dictionary<(int RowIndex, int ColumnIndex), List<ParticipantRecord>> _cellLookup = [];
    private readonly Dictionary<string, Color> _companyColorCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SolidColorBrush> _companyBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RowInfo> _rows = [];
    private readonly List<ParticipantRecord> _filteredPoints = [];

    private Rect _plotRect = Rect.Empty;
    private bool _indexDirty = true;
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
    private RecommendationSnapshot _lastRecommendation = RecommendationSnapshot.Empty;
    private Rect _recommendationHotRect = Rect.Empty;
    private bool _isRecommendationHovered;

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

    public NativeBidChartControl()
    {
        Focusable = true;
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
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NaraCollector",
                "logs",
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
        control.InvalidateVisual();
    }

    private void RebuildIndexIfNeeded()
    {
        if (!_indexDirty)
        {
            return;
        }

        _indexDirty = false;
        _animateDiff = Mode == NativeBidChartMode.Scatter;
        _precisionStep = ParsePrecision(Precision);
        _minRate = ParseRate(MinAdjustRate, 99.0);
        _maxRate = ParseRate(MaxAdjustRate, 101.0);
        if (!IsFinite(_minRate))
        {
            _minRate = 99.0;
        }
        if (!IsFinite(_maxRate))
        {
            _maxRate = 101.0;
        }
        if (_minRate > _maxRate)
        {
            (_minRate, _maxRate) = (_maxRate, _minRate);
        }

        _minRate = Math.Clamp(_minRate, 98.0, 102.0);
        _maxRate = Math.Clamp(_maxRate, 98.0, 102.0);
        if (Math.Abs(_maxRate - _minRate) < 0.00001)
        {
            _maxRate = Math.Min(102.0, _minRate + 0.1);
        }

        _cellLookup.Clear();
        _rows.Clear();
        _filteredPoints.Clear();
        _columnCount = (int)Math.Round((_maxRate - _minRate) / _precisionStep, MidpointRounding.AwayFromZero) + 1;
        _columnCount = Math.Max(1, _columnCount);
        _labelEvery = Math.Max(1, (int)Math.Round(0.1 / _precisionStep, MidpointRounding.AwayFromZero));

        var selectedTokens = SplitSelectedCompanyTokens(SelectedCompany);
        var filterAll = selectedTokens.Count == 0 || selectedTokens.Any(IsAllCompanyToken);
        var normalizedSelectedTokens = selectedTokens
            .Where(x => !IsAllCompanyToken(x))
            .Select(NormalizeCompanyToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var source = (Participants ?? Array.Empty<ParticipantRecord>())
            .Where(x => x.AdjustRate.HasValue)
            .Where(x => x.AdjustRate!.Value >= _minRate && x.AdjustRate.Value <= _maxRate)
            .Where(x => !IsExcludedCompany(x.NormalizedCompanyName, x.CompanyName))
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

        _filteredPoints.AddRange(source);
        _lastRecommendation = ComputeRecommendationSnapshot();
        _recommendationHotRect = Rect.Empty;
        _isRecommendationHovered = false;

        if (source.Count == 0 || Mode != NativeBidChartMode.Scatter)
        {
            if (_animationFrameHooked)
            {
                CompositionTarget.Rendering -= HandleAnimationFrame;
                _animationFrameHooked = false;
            }
            return;
        }

        var groupedRows = source
            .GroupBy(
                x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new RowInfo(
                g.Key,
                g.Max(x => x.OpenDate),
                g.OrderBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                    .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(x => x.OpenDate)
            .ToList();

        _rows.AddRange(groupedRows);
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            foreach (var participant in _rows[rowIndex].Points)
            {
                if (!participant.AdjustRate.HasValue)
                {
                    continue;
                }

                var columnIndex = ToColumnIndex(participant.AdjustRate.Value);
                var key = (rowIndex, columnIndex);
                if (!_cellLookup.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    _cellLookup[key] = bucket;
                }
                bucket.Add(participant);
            }
        }

        _animationStartedAtUtc = DateTime.UtcNow;
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

        dc.DrawRectangle(PlotBackgroundBrush, AxisPen, _plotRect);

        if (_rows.Count == 0 || _columnCount <= 0)
        {
            DrawMessage(dc, area, "표시할 차트 데이터가 없습니다.");
            return;
        }

        var (cellWidth, cellHeight) = GetScatterCellSize();
        var worldWidth = _columnCount * cellWidth;
        var worldHeight = _rows.Count * cellHeight;

        var invScale = 1.0 / Math.Max(0.0001, _scale);
        var visibleWorld = new Rect(
            (-_offset.X) * invScale,
            (-_offset.Y) * invScale,
            _plotRect.Width * invScale,
            _plotRect.Height * invScale);

        var colStart = Math.Clamp((int)Math.Floor(visibleWorld.Left / cellWidth) - 1, 0, Math.Max(0, _columnCount - 1));
        var colEnd = Math.Clamp((int)Math.Ceiling(visibleWorld.Right / cellWidth) + 1, 0, Math.Max(0, _columnCount - 1));
        var rowStart = Math.Clamp((int)Math.Floor(visibleWorld.Top / cellHeight) - 1, 0, Math.Max(0, _rows.Count - 1));
        var rowEnd = Math.Clamp((int)Math.Ceiling(visibleWorld.Bottom / cellHeight) + 1, 0, Math.Max(0, _rows.Count - 1));
        var visibleRows = Math.Max(1, rowEnd - rowStart + 1);
        var visibleCols = Math.Max(1, colEnd - colStart + 1);
        var visibleCellEstimate = visibleRows * visibleCols;
        var nameCharLimit = ResolveNameCharLimit(cellWidth, cellHeight, visibleCellEstimate);
        var drawRowLabels = !HighPerformanceMode
            || (_scale >= 1.0 && visibleRows <= 140 && visibleCellEstimate <= 18000);

        var recommendation = _lastRecommendation with
        {
            RecommendedRate = Math.Clamp(_lastRecommendation.RecommendedRate, _minRate, _maxRate),
            BandMin = Math.Clamp(_lastRecommendation.BandMin, _minRate, _maxRate),
            BandMax = Math.Clamp(_lastRecommendation.BandMax, _minRate, _maxRate),
        };
        var bandStartColumn = ToColumnIndex(Math.Min(recommendation.BandMin, recommendation.BandMax));
        var bandEndColumn = ToColumnIndex(Math.Max(recommendation.BandMin, recommendation.BandMax));

        dc.PushClip(new RectangleGeometry(_plotRect));
        dc.PushTransform(new TranslateTransform(_plotRect.Left + _offset.X, _plotRect.Top + _offset.Y));
        dc.PushTransform(new ScaleTransform(_scale, _scale));

        var recommendationBand = new Rect(
            bandStartColumn * cellWidth,
            0,
            Math.Max(1, ((bandEndColumn - bandStartColumn) + 1) * cellWidth),
            worldHeight);
        dc.DrawRectangle(RecommendationBandBrush, RecommendationBandPen, recommendationBand);

        for (var col = colStart; col <= colEnd; col++)
        {
            var x = col * cellWidth;
            var pen = col % _labelEvery == 0 ? MajorGridPen : GridPen;
            dc.DrawLine(pen, new Point(x, 0), new Point(x, worldHeight));
        }

        for (var row = rowStart; row <= rowEnd; row++)
        {
            var y = row * cellHeight;
            dc.DrawLine(GridPen, new Point(0, y), new Point(worldWidth, y));
        }

        var progress = _animateDiff
            ? Math.Clamp((DateTime.UtcNow - _animationStartedAtUtc).TotalMilliseconds / 320.0, 0.0, 1.0)
            : 1.0;

        var hoverCompany = _hoverCompany;
        for (var row = rowStart; row <= rowEnd; row++)
        {
            for (var col = colStart; col <= colEnd; col++)
            {
                if (!_cellLookup.TryGetValue((row, col), out var bucket))
                {
                    continue;
                }

                for (var i = 0; i < bucket.Count; i++)
                {
                    var point = bucket[i];
                    var rect = BuildPointRect(row, col, i, cellWidth, cellHeight, progress);

                    var blockBrush = ResolveCompanyBrush(point.NormalizedCompanyName, point.CompanyName);
                    dc.DrawRectangle(blockBrush, BlockBorderPen, rect);

                    if (point.Rank == 1)
                    {
                        dc.DrawRectangle(RankHighlightFillBrush, null, rect);
                        dc.DrawRectangle(null, RankHighlightOuterPen, rect);
                        dc.DrawRectangle(null, RankHighlightPen, rect);
                        var badgeRect = new Rect(
                            rect.Right - Math.Max(6, rect.Width * 0.45),
                            rect.Top + 1,
                            Math.Max(6, rect.Width * 0.42),
                            Math.Max(6, rect.Height * 0.38));
                        dc.DrawRoundedRectangle(
                            RankBadgeBrush,
                            null,
                            badgeRect,
                            2,
                            2);
                        DrawText(
                            dc,
                            "1",
                            new Point(badgeRect.Left + 1.2, badgeRect.Top - 0.6),
                            Math.Max(5.5, badgeRect.Height - 1.5),
                            FontWeights.Bold,
                            RankBadgeTextBrush,
                            badgeRect.Width - 2);
                    }

                    if (!string.IsNullOrWhiteSpace(hoverCompany)
                        && (ContainsToken(point.CompanyName, hoverCompany) || ContainsToken(point.NormalizedCompanyName, hoverCompany)))
                    {
                        dc.DrawRectangle(null, HoverHighlightPen, rect);
                    }

                    var shortName = BuildShortName(point.NormalizedCompanyName, point.CompanyName, nameCharLimit);
                    DrawShortText(dc, shortName, rect);
                }
            }
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();

        DrawScatterAxes(dc, cellWidth, cellHeight, colStart, colEnd, rowStart, rowEnd, drawRowLabels);
        DrawRecommendationArrow(dc, recommendation, cellWidth);
        DrawRecommendedLabel(dc, recommendation);

        if (progress < 1.0 || _rows.Count > 0)
        {
            if (!_animationFrameHooked)
            {
                CompositionTarget.Rendering += HandleAnimationFrame;
                _animationFrameHooked = true;
            }
        }
        else
        {
            if (_animationFrameHooked)
            {
                CompositionTarget.Rendering -= HandleAnimationFrame;
                _animationFrameHooked = false;
            }
        }
    }

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
            .GroupBy(x => string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName : x.NormalizedCompanyName)
            .OrderByDescending(g => g.Count())
            .Take(IsAllCompanySelected() ? 8 : 1)
            .ToList();

        if (groupedByCompany.Count == 0)
        {
            DrawMessage(dc, area, "선형 추세를 표시할 데이터가 없습니다.");
            return;
        }

        var yMin = _minRate;
        var yMax = _maxRate;
        var ySpan = Math.Max(0.1, yMax - yMin);
        DrawTrendAxes(dc, yMin, yMax);

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
                    var px = _plotRect.Left + (points[i].X / (double)maxX) * _plotRect.Width;
                    var py = _plotRect.Bottom - ((points[i].Y - yMin) / ySpan) * _plotRect.Height;
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
        }

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
            .Select(x => x.AdjustRate!.Value)
            .Where(x => x >= _minRate && x <= _maxRate)
            .ToList();
        if (rates.Count == 0)
        {
            DrawMessage(dc, area, "분포를 표시할 데이터가 없습니다.");
            return;
        }

        var bins = new int[_columnCount];
        foreach (var rate in rates)
        {
            var index = ToColumnIndex(rate);
            bins[index]++;
        }

        var maxCount = Math.Max(1, bins.Max());
        var barWidth = _plotRect.Width / _columnCount;
        DrawDistributionAxes(dc, maxCount, barWidth);
        for (var i = 0; i < _columnCount; i++)
        {
            var h = (bins[i] / (double)maxCount) * _plotRect.Height;
            var x = _plotRect.Left + (i * barWidth);
            var y = _plotRect.Bottom - h;
            var brush = i % _labelEvery == 0 ? DistributionMajorBarBrush : DistributionMinorBarBrush;
            dc.DrawRectangle(brush, null, new Rect(x + 1, y, Math.Max(1, barWidth - 2), h));
        }

        var centerIndex = ToColumnIndex(100.0);
        var centerX = _plotRect.Left + centerIndex * barWidth + (barWidth / 2.0);
        dc.DrawLine(DistributionCenterPen, new Point(centerX, _plotRect.Top), new Point(centerX, _plotRect.Bottom));

        DrawText(
            dc,
            $"사정률 분포 ({rates.Count:N0}건, 중심 100.000)",
            new Point(_plotRect.Left + 6, _plotRect.Top + 6),
            12,
            FontWeights.SemiBold,
            TextBrush);
    }

    private void DrawTrendAxes(DrawingContext dc, double yMin, double yMax)
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
            var x = _plotRect.Left + (ratio * _plotRect.Width);
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

    private void DrawDistributionAxes(DrawingContext dc, int maxCount, double barWidth)
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
            var x = _plotRect.Left + (i * barWidth) + (barWidth / 2.0);
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
        var header = $"추천 {FormatRate(recommendation.RecommendedRate)}";
        var detail =
            $"{FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}% · 종합점수 {recommendation.EvidenceScore:0.#} · 가림막 {recommendation.ShadowScore:0.#} · 안전마진 {recommendation.MarginScore:0.#} · 경쟁밀도 {recommendation.CompetitionScore:0.#} · 낙찰점수 {recommendation.WinScore:0.#}";
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
        var seconds = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var bob = Math.Sin(seconds * 3.2) * 3.2;
        var pulse = 0.5 + (0.5 * Math.Sin(seconds * 4.4));

        var tipY = Math.Clamp(_plotRect.Top + 26 + bob, _plotRect.Top + 18, _plotRect.Top + 34);
        var shaftTopY = Math.Clamp(_plotRect.Top + 4 + bob, _plotRect.Top + 1, _plotRect.Top + 10);

        dc.DrawLine(
            pulse > 0.55 ? RecommendationGuideStrongPen : RecommendationGuidePen,
            new Point(clampedX, _plotRect.Top + 1),
            new Point(clampedX, _plotRect.Bottom - 1));

        var ringY = Math.Clamp(_plotRect.Top + 18, _plotRect.Top + 14, _plotRect.Top + 24);
        var ringRadius = 8.0 + (pulse * 4.0);
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

        var label = $"공백기반 추천 {FormatRate(recommendation.RecommendedRate)}";
        var rangeText = $"{FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}% · 근거공고 {recommendation.SupportingNoticeCount:N0}건 / 낙찰근거 {recommendation.AwardNoticeCount:N0}건 · 클릭 적용";

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

    private void DrawShortText(DrawingContext dc, string text, Rect rect)
    {
        var displayText = string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim();
        if (displayText.Length > 2)
        {
            displayText = displayText[..2];
        }

        if (string.IsNullOrWhiteSpace(displayText)
            || !IsFinite(rect.Width)
            || !IsFinite(rect.Height)
            || !IsFinite(rect.Left)
            || !IsFinite(rect.Top)
            || rect.Width < 4
            || rect.Height < 4)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var fontSize = Math.Clamp(
            Math.Min(
                rect.Height * 0.94,
                rect.Width / Math.Max(1.0, displayText.Length * 0.74)),
            4.0,
            14.0);
        var typeface = new Typeface(
            new FontFamily("Malgun Gothic"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);
        var maxTextWidth = Math.Max(2.0, rect.Width - 0.2);
        var maxTextHeight = Math.Max(2.0, rect.Height - 0.2);

        static FormattedText CreateLabel(
            string label,
            Typeface face,
            double size,
            Brush brush,
            double pixelsPerDip)
        {
            return new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                face,
                size,
                brush,
                pixelsPerDip);
        }

        var formatted = CreateLabel(displayText, typeface, fontSize, Brushes.White, dpi);
        while ((formatted.Width > maxTextWidth || formatted.Height > maxTextHeight) && fontSize > 3.8)
        {
            fontSize -= 0.4;
            formatted = CreateLabel(displayText, typeface, fontSize, Brushes.White, dpi);
        }

        var shadow = CreateLabel(displayText, typeface, fontSize, Brushes.Black, dpi);

        var x = rect.Left + Math.Max(0, (rect.Width - formatted.Width) / 2.0);
        var y = rect.Top + Math.Max(0, (rect.Height - formatted.Height) / 2.0);
        if (!IsFinite(x) || !IsFinite(y))
        {
            return;
        }

        dc.PushClip(new RectangleGeometry(rect));
        dc.DrawText(shadow, new Point(x + 0.55, y + 0.55));
        dc.DrawText(formatted, new Point(x, y));
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
        var formatted = new FormattedText(
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

        if (!_cellLookup.TryGetValue((row, col), out var bucket) || bucket.Count == 0)
        {
            if (_hoverCompany is not null)
            {
                _hoverCompany = null;
                ToolTip = null;
                InvalidateVisual();
            }
            return;
        }

        var target = bucket[0];
        var nextHoverCompany = string.IsNullOrWhiteSpace(target.CompanyName)
            ? target.NormalizedCompanyName
            : target.CompanyName;

        var tooltipText =
            $"{target.CompanyName}\n순위: {target.Rank}\n공고: {_rows[row].NoticeNumber}\n개찰: {target.OpenDate:yyyy-MM-dd HH:mm:ss}\n사정률: {target.AdjustRate:F5}%";
        ToolTip = tooltipText;

        if (!string.Equals(_hoverCompany, nextHoverCompany, StringComparison.OrdinalIgnoreCase))
        {
            _hoverCompany = nextHoverCompany;
            InvalidateVisual();
        }
    }

    private bool UpdateRecommendationHover(Point position)
    {
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

    private bool TryApplyRecommendedRange(Point position)
    {
        if (Mode != NativeBidChartMode.Scatter
            || _recommendationHotRect == Rect.Empty
            || !_recommendationHotRect.Contains(position))
        {
            return false;
        }

        var min = Math.Clamp(Math.Min(_lastRecommendation.BandMin, _lastRecommendation.BandMax), _minRate, _maxRate);
        var max = Math.Clamp(Math.Max(_lastRecommendation.BandMin, _lastRecommendation.BandMax), _minRate, _maxRate);
        RateRangeSelected?.Invoke(this, new RateRangeSelectedEventArgs(min, max));
        ToolTip = $"{BuildRecommendationTooltip(_lastRecommendation)}\n추천 구간을 필터로 적용했습니다.";
        return true;
    }

    private string BuildRecommendationTooltip(RecommendationSnapshot recommendation)
    {
        return
            $"고정업체 왼쪽/빈공간 기반 분석\n추천값: {FormatRate(recommendation.RecommendedRate)}%\n권장구간: {FormatRate(recommendation.BandMin)}%~{FormatRate(recommendation.BandMax)}%\n종합점수: {recommendation.EvidenceScore:0.#}\n- 가림막 점수: {recommendation.ShadowScore:0.#}\n- 안전마진 점수: {recommendation.MarginScore:0.#}\n- 경쟁밀도 점수: {recommendation.CompetitionScore:0.#}\n- 역사적 낙찰 점수: {recommendation.WinScore:0.#}\n근거 공고: {recommendation.SupportingNoticeCount:N0}건 · 낙찰 근거공고: {recommendation.AwardNoticeCount:N0}건 · 경쟁 표본: {recommendation.CompetitorSampleCount:N0}건 · 구간 표본: {recommendation.BandCompetitorCount:N0}건";
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
            for (var i = 0; i < bucket.Count; i++)
            {
                var rect = BuildPointRect(row, col, i, cellWidth, cellHeight, progress);
                if (!rect.Contains(worldPoint))
                {
                    continue;
                }

                var hitName = !string.IsNullOrWhiteSpace(bucket[i].CompanyName)
                    ? bucket[i].CompanyName
                    : bucket[i].NormalizedCompanyName;
                if (string.IsNullOrWhiteSpace(hitName))
                {
                    return;
                }

                CompanyPicked?.Invoke(this, new CompanyPickedEventArgs(hitName.Trim()));
                return;
            }

            var name = !string.IsNullOrWhiteSpace(bucket[0].CompanyName)
                ? bucket[0].CompanyName
                : bucket[0].NormalizedCompanyName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                CompanyPicked?.Invoke(this, new CompanyPickedEventArgs(name.Trim()));
            }
        }
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
        var competitors = _filteredPoints
            .Where(x => x.AdjustRate.HasValue)
            .Where(x => !IsDonggukCompany(x.NormalizedCompanyName) && !IsDonggukCompany(x.CompanyName))
            .Where(x => !IsExcludedCompany(x.NormalizedCompanyName, x.CompanyName))
            .Select(x => new RecommendationPoint(
                Rate: Math.Clamp(x.AdjustRate!.Value, _minRate, _maxRate),
                OpenDate: x.OpenDate,
                Company: string.IsNullOrWhiteSpace(x.NormalizedCompanyName) ? x.CompanyName ?? string.Empty : x.NormalizedCompanyName,
                Notice: NormalizeCompanyToken(string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber),
                Rank: x.Rank))
            .Where(x => !string.IsNullOrWhiteSpace(x.Company) && !string.IsNullOrWhiteSpace(x.Notice))
            .ToList();

        if (competitors.Count == 0 || _columnCount <= 0)
        {
            var fallbackCenter = Math.Clamp(100.0, _minRate, _maxRate);
            var spread = Math.Max(_precisionStep * 2, 0.002);
            return new RecommendationSnapshot(
                RecommendedRate: fallbackCenter,
                BandMin: Math.Clamp(fallbackCenter - spread, _minRate, _maxRate),
                BandMax: Math.Clamp(fallbackCenter + spread, _minRate, _maxRate),
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
                EvidenceScore: 0);
        }

        var latest = competitors.Max(x => x.OpenDate);
        var oneYearCutoff = latest.AddDays(-365);
        var sixMonthCutoff = latest.AddDays(-180);
        var threeMonthCutoff = latest.AddDays(-90);

        var rowsAll = competitors;
        var rows1y = competitors.Where(x => x.OpenDate >= oneYearCutoff).ToList();
        var rows6m = competitors.Where(x => x.OpenDate >= sixMonthCutoff).ToList();
        var rows3m = competitors.Where(x => x.OpenDate >= threeMonthCutoff).ToList();
        var rowsFinal = competitors.Where(x => x.OpenDate >= threeMonthCutoff).ToList();

        var fixedAll = BuildFixedCompanyMap(rowsAll, minBids: 6, minConcentration: 0.52);
        var fixed1y = BuildFixedCompanyMap(rows1y, minBids: 5, minConcentration: 0.53);
        var fixed6m = BuildFixedCompanyMap(rows6m, minBids: 4, minConcentration: 0.54);
        var fixed3m = BuildFixedCompanyMap(rows3m, minBids: 3, minConcentration: 0.55);

        var finalDensity = BuildDensity(rowsFinal);
        var candidateScores = Enumerable.Repeat(0.0, _columnCount).ToArray();
        var candidateSupportNotices = new int[_columnCount];
        var candidateAwardNotices = new int[_columnCount];
        var candidateShadowScores = new double[_columnCount];
        var candidateMarginScores = new double[_columnCount];
        var candidateCompetitionScores = new double[_columnCount];
        var candidateWinScores = new double[_columnCount];
        var candidateGapScores = new double[_columnCount];
        var candidateCrowdScores = new double[_columnCount];
        const int MinSupportNotices = 3;
        const int MinAwardNotices = 1;
        var nearbyRadius = Math.Max(1, (int)Math.Round(0.01 / Math.Max(_precisionStep, 0.0001), MidpointRounding.AwayFromZero));

        var companyNames = fixed3m.Keys
            .Concat(fixed6m.Keys)
            .Concat(fixed1y.Keys)
            .Concat(fixedAll.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var company in companyNames)
        {
            if (!TryResolveStableFixedColumn(
                    company,
                    fixedAll,
                    fixed1y,
                    fixed6m,
                    fixed3m,
                    out var fixedColumn,
                    out var fixedStrengthScore,
                    out var fixedBidCount))
            {
                continue;
            }

            var targetColumn = fixedColumn - 1;
            if (targetColumn < 1 || targetColumn >= _columnCount)
            {
                continue;
            }

            var fixedDensity = finalDensity[Math.Clamp(fixedColumn, 0, _columnCount - 1)];
            var targetRate = ToRateFromColumn(targetColumn);
            var nearestLeftColumn = FindNearestLeftOccupiedColumn(finalDensity, targetColumn);
            var leftGap = nearestLeftColumn >= 0
                ? Math.Max(0.0, targetRate - ToRateFromColumn(nearestLeftColumn))
                : Math.Max(0.0, targetRate - _minRate);

            // 1) 가림막 점수: (position + 0.001)에 고정업체가 있을 때 집중도 × 투찰횟수
            var hasFixedAbove = fixedColumn == targetColumn + 1 && fixedDensity > 0;
            var shadowScore = hasFixedAbove
                ? Math.Max(0.0, fixedStrengthScore * Math.Max(1, fixedBidCount))
                : 0.0;

            // 2) 안전마진 점수: 바로 왼쪽 최근접 업체와의 간격 × 1000
            var marginScore = Math.Max(0.0, leftGap * 1000.0);

            // 3) 경쟁밀도 점수: ±0.01% 범위 내 업체수 기반
            var nearbyBidders = rowsFinal
                .Where(x => Math.Abs(ToColumnIndex(x.Rate) - targetColumn) <= nearbyRadius)
                .Select(x => x.Company)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var competitionScore = Math.Max(0.0, 100.0 - (nearbyBidders * 5.0));

            // 4) 역사적 낙찰 점수: 해당 위치 1위 횟수 × 10
            var winCountAtPosition = rowsFinal
                .Where(x => x.Rank == 1 && ToColumnIndex(x.Rate) == targetColumn)
                .Select(x => x.Notice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var winScore = Math.Max(0.0, winCountAtPosition * 10.0);

            var supportNoticeCount = rowsFinal
                .Where(x =>
                {
                    var col = ToColumnIndex(x.Rate);
                    return col >= targetColumn - 1 && col <= targetColumn + 1;
                })
                .Select(x => x.Notice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var awardNoticeCount = rowsFinal
                .Where(x => x.Rank == 1)
                .Where(x =>
                {
                    var col = ToColumnIndex(x.Rate);
                    return col >= targetColumn - 1 && col <= targetColumn + 1;
                })
                .Select(x => x.Notice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // 빈 구간 추천 방지: 최소 근거 공고 + 최소 낙찰 근거를 반드시 만족해야 함
            if (supportNoticeCount < MinSupportNotices || awardNoticeCount < MinAwardNotices)
            {
                continue;
            }

            var shadowRankScore = Math.Clamp(shadowScore / 20.0, 0.0, 100.0);
            var marginRankScore = Math.Clamp(marginScore, 0.0, 100.0);
            var competitionRankScore = Math.Clamp(competitionScore, 0.0, 100.0);
            var winRankScore = Math.Clamp(winScore, 0.0, 100.0);

            var evidenceScore =
                (shadowRankScore * 0.40)
                + (marginRankScore * 0.25)
                + (competitionRankScore * 0.15)
                + (winRankScore * 0.20);

            if (evidenceScore <= candidateScores[targetColumn])
            {
                continue;
            }

            candidateScores[targetColumn] = evidenceScore;
            candidateSupportNotices[targetColumn] = supportNoticeCount;
            candidateAwardNotices[targetColumn] = awardNoticeCount;
            candidateShadowScores[targetColumn] = shadowScore;
            candidateMarginScores[targetColumn] = marginScore;
            candidateCompetitionScores[targetColumn] = competitionScore;
            candidateWinScores[targetColumn] = winScore;
            candidateGapScores[targetColumn] = Math.Clamp(marginScore, 0.0, 100.0);
            candidateCrowdScores[targetColumn] = Math.Clamp(competitionScore, 0.0, 100.0);
        }

        var bestColumn = -1;
        var bestEvidenceScore = double.MinValue;
        for (var i = 0; i < candidateScores.Length; i++)
        {
            if (candidateScores[i] <= bestEvidenceScore)
            {
                continue;
            }

            bestEvidenceScore = candidateScores[i];
            bestColumn = i;
        }

        if (bestColumn < 0 || bestEvidenceScore <= 0)
        {
            // 고정업체 조건이 약한 경우에도 "빈 구간 추천"은 금지하고
            // 근거공고+낙찰근거가 있는 자리만 대체 추천
            var finalNoticeCount = Math.Max(1, rowsFinal.Select(x => x.Notice).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            for (var col = 0; col < _columnCount; col++)
            {
                var support = rowsFinal
                    .Where(x => Math.Abs(ToColumnIndex(x.Rate) - col) <= 1)
                    .Select(x => x.Notice)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var award = rowsFinal
                    .Where(x => x.Rank == 1)
                    .Where(x => Math.Abs(ToColumnIndex(x.Rate) - col) <= 1)
                    .Select(x => x.Notice)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                if (support < MinSupportNotices || award < MinAwardNotices)
                {
                    continue;
                }

                var supportScore = Math.Clamp((support / (double)finalNoticeCount) * 100.0, 0.0, 100.0);
                var targetRate = ToRateFromColumn(col);
                var nearestLeftColumn = FindNearestLeftOccupiedColumn(finalDensity, col);
                var leftGap = nearestLeftColumn >= 0
                    ? Math.Max(0.0, targetRate - ToRateFromColumn(nearestLeftColumn))
                    : Math.Max(0.0, targetRate - _minRate);
                var marginScore = Math.Max(0.0, leftGap * 1000.0);
                var nearbyBidders = rowsFinal
                    .Where(x => Math.Abs(ToColumnIndex(x.Rate) - col) <= nearbyRadius)
                    .Select(x => x.Company)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var competitionScore = Math.Max(0.0, 100.0 - (nearbyBidders * 5.0));
                var winScore = Math.Max(0.0, award * 10.0);
                var score =
                    (Math.Clamp(marginScore, 0.0, 100.0) * 0.45)
                    + (Math.Clamp(competitionScore, 0.0, 100.0) * 0.30)
                    + (Math.Clamp(winScore, 0.0, 100.0) * 0.15)
                    + (supportScore * 0.10);
                if (score <= bestEvidenceScore)
                {
                    continue;
                }

                bestEvidenceScore = score;
                bestColumn = col;
                candidateSupportNotices[col] = support;
                candidateAwardNotices[col] = award;
                candidateShadowScores[col] = 0;
                candidateMarginScores[col] = marginScore;
                candidateCompetitionScores[col] = competitionScore;
                candidateWinScores[col] = winScore;
                candidateGapScores[col] = Math.Clamp(marginScore, 0.0, 100.0);
                candidateCrowdScores[col] = Math.Clamp(competitionScore, 0.0, 100.0);
                candidateScores[col] = score;
            }
        }

        if (bestColumn < 0)
        {
            bestColumn = ToColumnIndex(Math.Clamp(100.0, _minRate, _maxRate));
            bestEvidenceScore = 0.0;
        }

        var threshold = Math.Max(18.0, bestEvidenceScore * 0.82);
        var left = bestColumn;
        var right = bestColumn;
        while (left > 0 && candidateScores[left - 1] >= threshold && (bestColumn - left) < 4)
        {
            left--;
        }

        while (right < _columnCount - 1 && candidateScores[right + 1] >= threshold && (right - bestColumn) < 4)
        {
            right++;
        }

        if (left == right)
        {
            left = Math.Max(0, bestColumn - 1);
            right = Math.Min(_columnCount - 1, bestColumn + 1);
        }

        var bandMin = ToRateFromColumn(left);
        var bandMax = ToRateFromColumn(right);
        var bandCount = rowsFinal.Count(x =>
        {
            var col = ToColumnIndex(x.Rate);
            return col >= left && col <= right;
        });

        return new RecommendationSnapshot(
            RecommendedRate: ToRateFromColumn(bestColumn),
            BandMin: Math.Clamp(bandMin, _minRate, _maxRate),
            BandMax: Math.Clamp(bandMax, _minRate, _maxRate),
            CompetitorSampleCount: competitors.Count,
            BandCompetitorCount: bandCount,
            SupportingNoticeCount: candidateSupportNotices[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            AwardNoticeCount: candidateAwardNotices[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            ShadowScore: candidateShadowScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            MarginScore: candidateMarginScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            CompetitionScore: candidateCompetitionScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            WinScore: candidateWinScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            GapOpportunityScore: candidateGapScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            CrowdAvoidanceScore: candidateCrowdScores[Math.Clamp(bestColumn, 0, _columnCount - 1)],
            EvidenceScore: Math.Clamp(bestEvidenceScore, 0.0, 100.0));
    }

    private static double GetDensitySafe(double[] density, int idx)
    {
        if (idx < 0 || idx >= density.Length)
        {
            return 0.0;
        }

        return density[idx];
    }

    private static int FindNearestLeftOccupiedColumn(double[] density, int targetColumn)
    {
        for (var col = targetColumn - 1; col >= 0; col--)
        {
            if (density[col] > 0)
            {
                return col;
            }
        }

        return -1;
    }

    private Dictionary<string, FixedCompanyStat> BuildFixedCompanyMap(
        IReadOnlyList<RecommendationPoint> rows,
        int minBids,
        double minConcentration)
    {
        var result = new Dictionary<string, FixedCompanyStat>(StringComparer.OrdinalIgnoreCase);
        if (rows.Count == 0)
        {
            return result;
        }

        foreach (var companyGroup in rows.GroupBy(x => x.Company, StringComparer.OrdinalIgnoreCase))
        {
            var total = companyGroup.Count();
            if (total < minBids)
            {
                continue;
            }

            var byColumn = companyGroup
                .GroupBy(x => ToColumnIndex(x.Rate))
                .Select(g => new { Column = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            if (byColumn.Count == 0)
            {
                continue;
            }

            var dominant = byColumn[0];
            var concentration = dominant.Count / (double)total;
            if (concentration < minConcentration)
            {
                continue;
            }

            result[companyGroup.Key] = new FixedCompanyStat(
                DominantColumn: dominant.Column,
                ConcentrationScore: concentration * 100.0,
                TotalBids: total);
        }

        return result;
    }

    private static bool TryResolveStableFixedColumn(
        string company,
        IReadOnlyDictionary<string, FixedCompanyStat> fixedAll,
        IReadOnlyDictionary<string, FixedCompanyStat> fixed1y,
        IReadOnlyDictionary<string, FixedCompanyStat> fixed6m,
        IReadOnlyDictionary<string, FixedCompanyStat> fixed3m,
        out int fixedColumn,
        out double fixedStrengthScore,
        out int fixedBidCount)
    {
        fixedColumn = -1;
        fixedStrengthScore = 0.0;
        fixedBidCount = 0;

        if (!fixed3m.TryGetValue(company, out var stat3m))
        {
            return false;
        }

        if (!fixed6m.TryGetValue(company, out var stat6m))
        {
            return false;
        }

        if (Math.Abs(stat3m.DominantColumn - stat6m.DominantColumn) > 2)
        {
            return false;
        }

        var cols = new List<int> { stat3m.DominantColumn, stat6m.DominantColumn };
        var weights = new List<double> { 0.40, 0.30 };
        var strengthParts = new List<double> { stat3m.ConcentrationScore * 0.40, stat6m.ConcentrationScore * 0.30 };

        if (fixed1y.TryGetValue(company, out var stat1y))
        {
            if (Math.Abs(stat1y.DominantColumn - stat6m.DominantColumn) > 3)
            {
                return false;
            }

            cols.Add(stat1y.DominantColumn);
            weights.Add(0.20);
            strengthParts.Add(stat1y.ConcentrationScore * 0.20);
        }

        if (fixedAll.TryGetValue(company, out var statAll))
        {
            cols.Add(statAll.DominantColumn);
            weights.Add(0.10);
            strengthParts.Add(statAll.ConcentrationScore * 0.10);
        }

        var weightedCol = 0.0;
        var weightSum = 0.0;
        for (var i = 0; i < cols.Count; i++)
        {
            weightedCol += cols[i] * weights[i];
            weightSum += weights[i];
        }

        fixedColumn = (int)Math.Round(weightedCol / Math.Max(0.0001, weightSum), MidpointRounding.AwayFromZero);
        fixedStrengthScore = Math.Clamp(strengthParts.Sum(), 0.0, 100.0);
        fixedBidCount = Math.Max(1, stat3m.TotalBids);
        return true;
    }

    private double[] BuildDensity(IReadOnlyList<RecommendationPoint> rows)
    {
        var density = new double[_columnCount];
        foreach (var point in rows)
        {
            density[ToColumnIndex(point.Rate)] += 1.0;
        }

        return density;
    }

    private string FormatRate(double rate)
    {
        var format = _precisionStep <= 0.0015 ? "F3" : "F2";
        return rate.ToString(format, CultureInfo.InvariantCulture);
    }

    private int ToColumnIndex(double rate)
    {
        var raw = (rate - _minRate) / _precisionStep;
        var col = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(col, 0, Math.Max(0, _columnCount - 1));
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
        if (string.IsNullOrWhiteSpace(source))
        {
            return "--";
        }

        var compact = source.Trim();
        if (IsDonggukCompany(compact))
        {
            return "동국";
        }

        compact = compact
            .Replace("(재)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(주)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(사)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("재단법인", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("사단법인", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("주식회사", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (compact.Length == 0)
        {
            return "--";
        }

        var hangul = new List<char>(compact.Length);
        foreach (var ch in compact)
        {
            if (ch >= '\uAC00' && ch <= '\uD7A3')
            {
                hangul.Add(ch);
            }
        }

        if (hangul.Count >= 2)
        {
            return new string([hangul[0], hangul[1]]);
        }

        if (hangul.Count == 1)
        {
            var first = hangul[0];
            var second = compact.FirstOrDefault(ch => ch != first && !char.IsWhiteSpace(ch));
            return second == default ? $"{first}" : $"{first}{second}";
        }

        if (compact.Length >= 2)
        {
            return compact[..2];
        }

        return compact;
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
        return normalized.Contains(DonggukCompanyKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedCompany(string? normalizedCompanyName, string? companyName)
    {
        var normalized = NormalizeCompanyToken(normalizedCompanyName);
        var raw = NormalizeCompanyToken(companyName);
        foreach (var keyword in ExcludedCompanyKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || raw.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        var shouldAnimatePulse = Mode == NativeBidChartMode.Scatter && _rows.Count > 0;
        if (progress >= 1.0 && !shouldAnimatePulse)
        {
            CompositionTarget.Rendering -= HandleAnimationFrame;
            _animationFrameHooked = false;
            return;
        }

        if (shouldAnimatePulse)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastFrameInvalidatedAtUtc) < TimeSpan.FromMilliseconds(66))
            {
                return;
            }

            _lastFrameInvalidatedAtUtc = now;
        }

        InvalidateVisual();
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
        double EvidenceScore)
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
            EvidenceScore: 0);
    }

    private sealed record RecommendationPoint(
        double Rate,
        DateTime OpenDate,
        string Company,
        string Notice,
        int Rank);

    private sealed record FixedCompanyStat(
        int DominantColumn,
        double ConcentrationScore,
        int TotalBids);

    private sealed record RowInfo(
        string NoticeNumber,
        DateTime OpenDate,
        IReadOnlyList<ParticipantRecord> Points);
}
