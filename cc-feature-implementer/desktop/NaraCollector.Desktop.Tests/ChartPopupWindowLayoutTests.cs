using System.Xml.Linq;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public sealed class ChartPopupWindowLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void FilterDock_DoesNotOverlapChart_WithNegativeMarginOrFixed148Row()
    {
        var popup = LoadDesktopXaml("ChartPopupWindow.xaml");
        var text = popup.ToString();

        Assert.DoesNotContain("0,-72", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"148\"", text, StringComparison.Ordinal);

        var negativeMargins = popup.Descendants()
            .Select(e => (string?)e.Attribute("Margin"))
            .Where(m => !string.IsNullOrWhiteSpace(m) && m!.Split(',').Any(part => part.Trim().StartsWith('-')))
            .ToList();
        Assert.Empty(negativeMargins);
    }

    [Fact]
    public void ScatterMap_KeepsBindingsEventsAndResetOverlay()
    {
        var popup = LoadDesktopXaml("ChartPopupWindow.xaml");
        var scatter = popup.Descendants().FirstOrDefault(e => (string?)e.Attribute(Xaml + "Name") == "ScatterMap");
        Assert.NotNull(scatter);
        Assert.Equal("CurrentParticipants", BindingPath(scatter!, "Participants"));
        Assert.Equal("SelectedCompany", BindingPath(scatter!, "SelectedCompany"));
        Assert.Equal("StartDate", BindingPath(scatter!, "StartDate"));
        Assert.Equal("EndDate", BindingPath(scatter!, "EndDate"));
        Assert.Equal("SelectedMinAdjustRate", BindingPath(scatter!, "MinAdjustRate"));
        Assert.Equal("SelectedMaxAdjustRate", BindingPath(scatter!, "MaxAdjustRate"));
        Assert.Equal("RecommendationBandMin", BindingPath(scatter!, "PinnedRecommendationBandMin"));
        Assert.Equal("RecommendationBandMax", BindingPath(scatter!, "PinnedRecommendationBandMax"));
        Assert.Equal("ConsolePipelineRecommendationText", BindingPath(scatter!, "PinnedRecommendationText"));
        Assert.Equal("SelectedChartPrecision", BindingPath(scatter!, "Precision"));
        Assert.Equal("Scatter", (string?)scatter!.Attribute("Mode"));
        Assert.Equal("ChartCompanyPicked", (string?)scatter.Attribute("CompanyPicked"));
        Assert.Equal("ChartHoveredRateChanged", (string?)scatter.Attribute("HoveredRateChanged"));
        Assert.Equal("ChartRateRangeSelected", (string?)scatter.Attribute("RateRangeSelected"));

        var reset = popup.Descendants(Presentation + "Button")
            .FirstOrDefault(e => (string?)e.Attribute("Click") == "ResetMapClick");
        Assert.NotNull(reset);
        Assert.Equal("맵 초기화", (string?)reset!.Attribute("Content"));
        Assert.Equal("{StaticResource MapResetButtonStyle}", (string?)reset.Attribute("Style"));
        Assert.Equal("40", (string?)reset.Attribute("Height"));

        var toolbar = reset.Ancestors(Presentation + "Border")
            .FirstOrDefault(e => (string?)e.Attribute("Style") == "{StaticResource MapToolbarBorderStyle}");
        Assert.NotNull(toolbar);
        Assert.Equal("Right", (string?)toolbar!.Attribute("HorizontalAlignment"));
        Assert.Equal("Top", (string?)toolbar.Attribute("VerticalAlignment"));
    }

    [Fact]
    public void Popup_KeepsTabsAndBottomFilterDock()
    {
        var popup = LoadDesktopXaml("ChartPopupWindow.xaml");
        var headers = popup.Descendants(Presentation + "TabItem")
            .Select(e => (string?)e.Attribute("Header") ?? string.Empty)
            .ToArray();
        Assert.Equal(new[] { "좌측선점" }, headers);
        Assert.DoesNotContain(headers, h => h.Contains("과열", StringComparison.Ordinal));
        Assert.DoesNotContain(headers, h => h.Contains("패턴", StringComparison.Ordinal));
        Assert.DoesNotContain(headers, h => h.Contains("실행 근거", StringComparison.Ordinal));

        var labels = popup.Descendants(Presentation + "TextBlock")
            .Select(e => (string?)e.Attribute("Text"))
            .Where(t => t is "기간" or "정밀도" or "지역" or "최소" or "최대")
            .ToArray();
        Assert.Contains("기간", labels);
        Assert.Contains("정밀도", labels);
        Assert.Contains("지역", labels);
        Assert.Contains("최소", labels);
        Assert.Contains("최대", labels);

        var resetAll = popup.Descendants(Presentation + "Button")
            .FirstOrDefault(e => (string?)e.Attribute("Click") == "ResetCompanySelectionClicked");
        Assert.NotNull(resetAll);
        Assert.Equal("전체", (string?)resetAll!.Attribute("Content"));

        Assert.Contains("CompanyCheckItems", popup.ToString(), StringComparison.Ordinal);

        var rowHeights = popup.Descendants(Presentation + "RowDefinition")
            .Select(e => (string?)e.Attribute("Height"))
            .ToList();
        Assert.Contains("*", rowHeights);
        Assert.Contains("Auto", rowHeights);
    }

    [Fact]
    public void Popup_PlacesLeftOccupyTabBesideScatterMap()
    {
        var popup = LoadDesktopXaml("ChartPopupWindow.xaml");
        var scatter = popup.Descendants().FirstOrDefault(e => (string?)e.Attribute(Xaml + "Name") == "ScatterMap");
        var tabs = popup.Descendants(Presentation + "TabControl")
            .FirstOrDefault(e => (string?)e.Attribute(Xaml + "Name") == "AnalysisSideTabs");
        var board = popup.Descendants().FirstOrDefault(e => (string?)e.Attribute(Xaml + "Name") == "LeftOccupyBoard");

        Assert.NotNull(scatter);
        Assert.NotNull(tabs);
        Assert.NotNull(board);
        Assert.Equal("{StaticResource AnalysisTabControlStyle}", (string?)tabs!.Attribute("Style"));
        Assert.Equal("LeftOccupyZones", BindingPath(board!, "ItemsSource"));

        var scatterColumn = scatter!.Ancestors(Presentation + "Border")
            .Select(e => (string?)e.Attribute("Grid.Column"))
            .FirstOrDefault(c => c is "0" or null);
        Assert.True(scatterColumn is "0" or null);
        Assert.Equal("2", (string?)tabs.Attribute("Grid.Column"));
        Assert.Null(scatter.Ancestors(Presentation + "TabItem").FirstOrDefault());

        var text = popup.ToString();
        Assert.Null(popup.Descendants().FirstOrDefault(e => (string?)e.Attribute(Xaml + "Name") == "CrowdingMap"));
        Assert.DoesNotContain("CrowdingHeatMapControl", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LeftOccupyBoardControl", text, StringComparison.Ordinal);
        Assert.DoesNotContain("과열·공백", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignTokens_DefineMapResetAndToolbarStyles()
    {
        var tokens = LoadDesktopXaml(Path.Combine("Theme", "DesignTokens.xaml"));
        var styles = tokens.Descendants(Presentation + "Style").ToList();

        var reset = styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "MapResetButtonStyle");
        Assert.NotNull(reset);
        Assert.Equal("Button", (string?)reset!.Attribute("TargetType"));
        var resetHeight = reset.Elements(Presentation + "Setter")
            .FirstOrDefault(s => (string?)s.Attribute("Property") == "Height");
        Assert.Equal("40", (string?)resetHeight?.Attribute("Value"));
        Assert.Contains("DropShadowEffect", reset.ToString(), StringComparison.Ordinal);

        var toolbar = styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "MapToolbarBorderStyle");
        Assert.NotNull(toolbar);
        Assert.Equal("Border", (string?)toolbar!.Attribute("TargetType"));
        Assert.Contains("DropShadowEffect", toolbar.ToString(), StringComparison.Ordinal);

        Assert.NotNull(styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "CardBorderStyle"));
        Assert.NotNull(styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "PrimaryButtonStyle"));
        Assert.NotNull(styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "AnalysisTabControlStyle"));
        Assert.NotNull(styles.FirstOrDefault(e => (string?)e.Attribute(Xaml + "Key") == "AnalysisTabItemStyle"));
    }

    private static string BindingPath(XElement element, string attribute)
    {
        var raw = (string?)element.Attribute(attribute);
        Assert.False(string.IsNullOrWhiteSpace(raw), $"{attribute} binding missing");
        const string prefix = "{Binding ";
        Assert.StartsWith(prefix, raw, StringComparison.Ordinal);
        return raw![prefix.Length..].TrimEnd('}');
    }

    private static XDocument LoadDesktopXaml(string relativePath)
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "Ui", relativePath);
        if (File.Exists(fromOutput))
        {
            return XDocument.Load(fromOutput);
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "NaraCollector.Desktop", relativePath);
            if (File.Exists(candidate))
            {
                return XDocument.Load(candidate);
            }
        }

        throw new FileNotFoundException($"Could not locate {relativePath}");
    }
}
