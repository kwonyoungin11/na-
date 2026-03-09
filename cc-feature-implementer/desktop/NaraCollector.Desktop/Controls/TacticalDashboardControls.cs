using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;

namespace NaraCollector.Desktop.Controls;

internal static class TacticalChartPalette
{
    public static readonly Brush Background = BrushFromHex("#FFFFFF");
    public static readonly Brush PanelBackground = BrushFromHex("#F7F9FC");
    public static readonly Brush AxisText = BrushFromHex("#5B6576");
    public static readonly Brush StrongText = BrushFromHex("#111827");
    public static readonly Pen GridPen = PenFromHex("#E5EAF2", 1.0);
    public static readonly Pen AxisPen = PenFromHex("#94A3B8", 1.2);
    public static readonly Pen FocusPen = PenFromHex("#2563EB", 1.6);
    public static readonly Brush FocusBrush = BrushFromHex("#DBEAFE");
    public static readonly Brush RecommendationBrush = BrushFromHex("#FDE68A");
    public static readonly Brush HeatNeutralBrush = BrushFromHex("#E5E7EB");
    public static readonly Brush HeatFocusBrush = BrushFromHex("#93C5FD");
    public static readonly Brush HeatThreatBrush = BrushFromHex("#FCA5A5");
    public static readonly Brush BarBrush = BrushFromHex("#F59E0B");
    public static readonly Brush BarSecondaryBrush = BrushFromHex("#FB7185");
    public static readonly Brush BarTertiaryBrush = BrushFromHex("#38BDF8");
    public static readonly Brush MarkerBrush = BrushFromHex("#1D4ED8");

    public static Brush Blend(Brush low, Brush high, double weight)
    {
        if (low is not SolidColorBrush lowBrush || high is not SolidColorBrush highBrush)
        {
            return high;
        }

        weight = Math.Clamp(weight, 0.0, 1.0);
        var lowColor = lowBrush.Color;
        var highColor = highBrush.Color;
        return new SolidColorBrush(Color.FromArgb(
            0xFF,
            (byte)Math.Round(lowColor.R + ((highColor.R - lowColor.R) * weight)),
            (byte)Math.Round(lowColor.G + ((highColor.G - lowColor.G) * weight)),
            (byte)Math.Round(lowColor.B + ((highColor.B - lowColor.B) * weight))));
    }

    public static Brush BrushFromHex(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    public static Pen PenFromHex(string hex, double thickness)
    {
        var pen = new Pen(BrushFromHex(hex), thickness);
        pen.Freeze();
        return pen;
    }
}

public sealed class CompanyBattlefieldMapControl : FrameworkElement
{
    private const string DonggukCompany = "동국문화재연구원";

    public static readonly DependencyProperty ParticipantsProperty = DependencyProperty.Register(
        nameof(Participants),
        typeof(IEnumerable<ParticipantRecord>),
        typeof(CompanyBattlefieldMapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinAdjustRateProperty = DependencyProperty.Register(
        nameof(MinAdjustRate),
        typeof(string),
        typeof(CompanyBattlefieldMapControl),
        new FrameworkPropertyMetadata("99.50000", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxAdjustRateProperty = DependencyProperty.Register(
        nameof(MaxAdjustRate),
        typeof(string),
        typeof(CompanyBattlefieldMapControl),
        new FrameworkPropertyMetadata("100.00000", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RecommendationBandMinProperty = DependencyProperty.Register(
        nameof(RecommendationBandMin),
        typeof(double?),
        typeof(CompanyBattlefieldMapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RecommendationBandMaxProperty = DependencyProperty.Register(
        nameof(RecommendationBandMax),
        typeof(double?),
        typeof(CompanyBattlefieldMapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ParticipantRecord>? Participants
    {
        get => (IEnumerable<ParticipantRecord>?)GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
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

    public double? RecommendationBandMin
    {
        get => (double?)GetValue(RecommendationBandMinProperty);
        set => SetValue(RecommendationBandMinProperty, value);
    }

    public double? RecommendationBandMax
    {
        get => (double?)GetValue(RecommendationBandMaxProperty);
        set => SetValue(RecommendationBandMaxProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        dc.DrawRoundedRectangle(TacticalChartPalette.Background, null, new Rect(RenderSize), 18, 18);

        var rows = (Participants ?? Array.Empty<ParticipantRecord>())
            .Where(x => x.AdjustRate.HasValue)
            .ToList();
        if (rows.Count == 0)
        {
            DrawEmpty(dc, "전장맵 데이터가 없습니다.");
            return;
        }

        var minRate = ParseRate(MinAdjustRate, 99.5);
        var maxRate = ParseRate(MaxAdjustRate, 100.0);
        if (minRate > maxRate)
        {
            (minRate, maxRate) = (maxRate, minRate);
        }

        rows = rows
            .Where(x => x.AdjustRate!.Value >= minRate && x.AdjustRate!.Value <= maxRate)
            .ToList();
        if (rows.Count == 0)
        {
            DrawEmpty(dc, "선택한 사정률 범위에 데이터가 없습니다.");
            return;
        }

        var groups = rows
            .GroupBy(x => CanonicalName(x))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        if (!groups.TryGetValue(DonggukCompany, out var donggukRows) || donggukRows.Count == 0)
        {
            DrawEmpty(dc, "동국 기준 데이터가 부족합니다.");
            return;
        }

        var donggukMedian = Median(donggukRows.Select(x => x.AdjustRate!.Value));
        var donggukNotices = donggukRows
            .Select(x => x.NoticeNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var companyNames = groups.Keys
            .Where(x => !string.Equals(x, DonggukCompany, StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                var companyRows = groups[name];
                var overlap = companyRows.Select(x => x.NoticeNumber)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(donggukNotices.Contains);
                var median = Median(companyRows.Select(x => x.AdjustRate!.Value));
                var delta = median - donggukMedian;
                var score = (Math.Min(overlap, 20) * 5.0) + Math.Max(0.0, 0.080 - Math.Abs(delta)) * 1000.0 + Math.Min(companyRows.Count, 20);
                return new { Name = name, Score = score, Delta = delta, Overlap = overlap };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => Math.Abs(x.Delta))
            .Take(9)
            .Select(x => x.Name)
            .ToList();

        var orderedCompanies = new List<string> { DonggukCompany };
        orderedCompanies.AddRange(companyNames);

        var labelWidth = 108.0;
        var topMargin = 20.0;
        var rightMargin = 18.0;
        var bottomMargin = 28.0;
        var plotRect = new Rect(labelWidth, topMargin, Math.Max(120, RenderSize.Width - labelWidth - rightMargin), Math.Max(80, RenderSize.Height - topMargin - bottomMargin));
        dc.DrawRoundedRectangle(TacticalChartPalette.PanelBackground, null, plotRect, 16, 16);

        if (RecommendationBandMin.HasValue && RecommendationBandMax.HasValue && RecommendationBandMax.Value > RecommendationBandMin.Value)
        {
            var bandStart = plotRect.Left + ((RecommendationBandMin.Value - minRate) / (maxRate - minRate)) * plotRect.Width;
            var bandEnd = plotRect.Left + ((RecommendationBandMax.Value - minRate) / (maxRate - minRate)) * plotRect.Width;
            dc.DrawRectangle(TacticalChartPalette.RecommendationBrush, null, new Rect(new Point(bandStart, plotRect.Top), new Point(bandEnd, plotRect.Bottom)));
        }

        var donggukX = plotRect.Left + ((donggukMedian - minRate) / (maxRate - minRate)) * plotRect.Width;
        dc.DrawLine(TacticalChartPalette.FocusPen, new Point(donggukX, plotRect.Top), new Point(donggukX, plotRect.Bottom));

        const int binCount = 28;
        var rowHeight = plotRect.Height / orderedCompanies.Count;
        var cellWidth = plotRect.Width / binCount;
        var maxBinCount = 1;
        var binsByCompany = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in orderedCompanies)
        {
            var counts = new int[binCount];
            foreach (var record in groups[company])
            {
                var rate = record.AdjustRate!.Value;
                var index = (int)Math.Floor(((rate - minRate) / (maxRate - minRate + 1e-9)) * binCount);
                index = Math.Clamp(index, 0, binCount - 1);
                counts[index]++;
                maxBinCount = Math.Max(maxBinCount, counts[index]);
            }

            binsByCompany[company] = counts;
        }

        for (var rowIndex = 0; rowIndex < orderedCompanies.Count; rowIndex++)
        {
            var company = orderedCompanies[rowIndex];
            var rowTop = plotRect.Top + (rowIndex * rowHeight);
            var rowRect = new Rect(plotRect.Left, rowTop, plotRect.Width, rowHeight - 2);
            dc.DrawRectangle(rowIndex == 0 ? TacticalChartPalette.FocusBrush : Brushes.Transparent, null, rowRect);

            var text = new FormattedText(
                rowIndex == 0 ? "동국" : CompanyNamePresentation.BuildShortLabel(company),
                CultureInfo.GetCultureInfo("ko-KR"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                rowIndex == 0 ? 12 : 11,
                rowIndex == 0 ? TacticalChartPalette.StrongText : TacticalChartPalette.AxisText,
                1.0);
            dc.DrawText(text, new Point(8, rowTop + ((rowHeight - text.Height) / 2)));

            var counts = binsByCompany[company];
            for (var binIndex = 0; binIndex < binCount; binIndex++)
            {
                var intensity = counts[binIndex] / (double)maxBinCount;
                var brush = rowIndex == 0
                    ? TacticalChartPalette.Blend(TacticalChartPalette.HeatNeutralBrush, TacticalChartPalette.HeatFocusBrush, intensity)
                    : TacticalChartPalette.Blend(TacticalChartPalette.HeatNeutralBrush, TacticalChartPalette.HeatThreatBrush, intensity);
                var cellRect = new Rect(plotRect.Left + (binIndex * cellWidth), rowTop + 1, Math.Max(1, cellWidth - 2), Math.Max(2, rowHeight - 4));
                dc.DrawRectangle(brush, null, cellRect);
            }
        }

        for (var tick = 0; tick <= 5; tick++)
        {
            var x = plotRect.Left + (plotRect.Width * tick / 5.0);
            dc.DrawLine(TacticalChartPalette.GridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
            var value = minRate + ((maxRate - minRate) * tick / 5.0);
            var text = new FormattedText(
                value.ToString("F3", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                TacticalChartPalette.AxisText,
                1.0);
            dc.DrawText(text, new Point(x - (text.Width / 2), plotRect.Bottom + 4));
        }
    }

    private void DrawEmpty(DrawingContext dc, string message)
    {
        var text = new FormattedText(
            message,
            CultureInfo.GetCultureInfo("ko-KR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            14,
            TacticalChartPalette.AxisText,
            1.0);
        dc.DrawText(text, new Point(18, 18));
    }

    private static string CanonicalName(ParticipantRecord record)
    {
        return string.IsNullOrWhiteSpace(record.NormalizedCompanyName)
            ? CompanyNamePresentation.Canonicalize(record.CompanyName)
            : CompanyNamePresentation.Canonicalize(record.NormalizedCompanyName);
    }

    private static double ParseRate(string? text, double fallback)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(x => x).ToList();
        if (ordered.Count == 0)
        {
            return 0.0;
        }

        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }
}

public sealed class MetricBarChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<MetricBarRowModel>),
        typeof(MetricBarChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<MetricBarRowModel>? ItemsSource
    {
        get => (IEnumerable<MetricBarRowModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(TacticalChartPalette.Background, null, new Rect(RenderSize), 18, 18);

        var items = (ItemsSource ?? Array.Empty<MetricBarRowModel>()).Take(6).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var max = Math.Max(1.0, items.Max(x => x.Value));
        var rowHeight = Math.Max(32, (RenderSize.Height - 12) / items.Count);
        var labelWidth = 64.0;
        var valueWidth = 52.0;
        var barWidth = Math.Max(24, RenderSize.Width - labelWidth - valueWidth - 24);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var top = 8 + (index * rowHeight);
            var ratio = item.Value / max;
            var brush = (index % 3) switch
            {
                0 => TacticalChartPalette.BarBrush,
                1 => TacticalChartPalette.BarSecondaryBrush,
                _ => TacticalChartPalette.BarTertiaryBrush,
            };
            var label = new FormattedText(item.Label, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, TacticalChartPalette.AxisText, 1.0);
            dc.DrawText(label, new Point(6, top + 6));
            dc.DrawRoundedRectangle(TacticalChartPalette.HeatNeutralBrush, null, new Rect(labelWidth, top + 8, barWidth, 12), 6, 6);
            dc.DrawRoundedRectangle(brush, null, new Rect(labelWidth, top + 8, Math.Max(4, barWidth * ratio), 12), 6, 6);
            var valueText = new FormattedText(item.ValueText, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, TacticalChartPalette.StrongText, 1.0);
            dc.DrawText(valueText, new Point(labelWidth + barWidth + 8, top + 4));
        }
    }
}

public sealed class TrendLineChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<TrendPointModel>),
        typeof(TrendLineChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<TrendPointModel>? ItemsSource
    {
        get => (IEnumerable<TrendPointModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(TacticalChartPalette.Background, null, new Rect(RenderSize), 18, 18);
        var items = (ItemsSource ?? Array.Empty<TrendPointModel>()).ToList();
        if (items.Count < 2)
        {
            return;
        }

        var plotRect = new Rect(16, 14, Math.Max(80, RenderSize.Width - 32), Math.Max(50, RenderSize.Height - 34));
        var min = items.Min(x => x.Value);
        var max = items.Max(x => x.Value);
        if (Math.Abs(max - min) < 0.00001)
        {
            max += 0.0001;
            min -= 0.0001;
        }

        dc.DrawRoundedRectangle(TacticalChartPalette.PanelBackground, null, plotRect, 14, 14);
        dc.DrawLine(TacticalChartPalette.AxisPen, new Point(plotRect.Left, plotRect.Bottom), new Point(plotRect.Right, plotRect.Bottom));
        dc.DrawLine(TacticalChartPalette.AxisPen, new Point(plotRect.Left, plotRect.Top), new Point(plotRect.Left, plotRect.Bottom));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var index = 0; index < items.Count; index++)
            {
                var x = plotRect.Left + (plotRect.Width * index / (items.Count - 1.0));
                var y = plotRect.Bottom - ((items[index].Value - min) / (max - min)) * plotRect.Height;
                if (index == 0)
                {
                    ctx.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    ctx.LineTo(new Point(x, y), true, true);
                }
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, TacticalChartPalette.FocusPen, geometry);

        for (var index = 0; index < items.Count; index++)
        {
            var x = plotRect.Left + (plotRect.Width * index / (items.Count - 1.0));
            var y = plotRect.Bottom - ((items[index].Value - min) / (max - min)) * plotRect.Height;
            dc.DrawEllipse(TacticalChartPalette.MarkerBrush, null, new Point(x, y), 3.5, 3.5);
            var label = new FormattedText(items[index].Label, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, TacticalChartPalette.AxisText, 1.0);
            dc.DrawText(label, new Point(x - (label.Width / 2), plotRect.Bottom + 2));
        }
    }
}

public sealed class HistogramChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<HistogramBinModel>),
        typeof(HistogramChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerValueProperty = DependencyProperty.Register(
        nameof(MarkerValue),
        typeof(double?),
        typeof(HistogramChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightMinProperty = DependencyProperty.Register(
        nameof(HighlightMin),
        typeof(double?),
        typeof(HistogramChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightMaxProperty = DependencyProperty.Register(
        nameof(HighlightMax),
        typeof(double?),
        typeof(HistogramChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<HistogramBinModel>? ItemsSource
    {
        get => (IEnumerable<HistogramBinModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double? MarkerValue
    {
        get => (double?)GetValue(MarkerValueProperty);
        set => SetValue(MarkerValueProperty, value);
    }

    public double? HighlightMin
    {
        get => (double?)GetValue(HighlightMinProperty);
        set => SetValue(HighlightMinProperty, value);
    }

    public double? HighlightMax
    {
        get => (double?)GetValue(HighlightMaxProperty);
        set => SetValue(HighlightMaxProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(TacticalChartPalette.Background, null, new Rect(RenderSize), 18, 18);
        var bins = (ItemsSource ?? Array.Empty<HistogramBinModel>()).ToList();
        if (bins.Count == 0)
        {
            return;
        }

        var plotRect = new Rect(16, 14, Math.Max(100, RenderSize.Width - 32), Math.Max(60, RenderSize.Height - 32));
        dc.DrawRoundedRectangle(TacticalChartPalette.PanelBackground, null, plotRect, 14, 14);
        var max = Math.Max(1.0, bins.Max(x => x.Value));
        var width = plotRect.Width / bins.Count;
        var minX = bins.First().Start;
        var maxX = bins.Last().End;

        if (HighlightMin.HasValue && HighlightMax.HasValue && HighlightMax.Value > HighlightMin.Value)
        {
            var start = plotRect.Left + ((HighlightMin.Value - minX) / (maxX - minX)) * plotRect.Width;
            var end = plotRect.Left + ((HighlightMax.Value - minX) / (maxX - minX)) * plotRect.Width;
            dc.DrawRectangle(TacticalChartPalette.RecommendationBrush, null, new Rect(new Point(start, plotRect.Top), new Point(end, plotRect.Bottom)));
        }

        for (var index = 0; index < bins.Count; index++)
        {
            var barHeight = (bins[index].Value / max) * (plotRect.Height - 16);
            var rect = new Rect(plotRect.Left + (index * width) + 1, plotRect.Bottom - barHeight - 8, Math.Max(3, width - 2), barHeight);
            dc.DrawRectangle(TacticalChartPalette.BarBrush, null, rect);
        }

        if (MarkerValue.HasValue)
        {
            var x = plotRect.Left + ((MarkerValue.Value - minX) / (maxX - minX)) * plotRect.Width;
            dc.DrawLine(TacticalChartPalette.FocusPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
        }
    }
}

public sealed class ZoneCompositionChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<ZoneSegmentModel>),
        typeof(ZoneCompositionChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ZoneSegmentModel>? ItemsSource
    {
        get => (IEnumerable<ZoneSegmentModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(TacticalChartPalette.Background, null, new Rect(RenderSize), 18, 18);
        var items = (ItemsSource ?? Array.Empty<ZoneSegmentModel>()).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var total = Math.Max(1.0, items.Sum(x => x.Value));
        var barRect = new Rect(16, 24, Math.Max(80, RenderSize.Width - 32), 24);
        var brushes = new[] { TacticalChartPalette.BarSecondaryBrush, TacticalChartPalette.BarBrush, TacticalChartPalette.BarTertiaryBrush };
        double currentX = barRect.Left;
        for (var index = 0; index < items.Count; index++)
        {
            var width = barRect.Width * (items[index].Value / total);
            dc.DrawRoundedRectangle(brushes[index % brushes.Length], null, new Rect(currentX, barRect.Top, width, barRect.Height), 8, 8);
            currentX += width;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var text = new FormattedText(
                $"{item.Label} {item.ValueText}",
                CultureInfo.GetCultureInfo("ko-KR"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                TacticalChartPalette.AxisText,
                1.0);
            dc.DrawText(text, new Point(16, 60 + (index * 16)));
        }
    }
}
