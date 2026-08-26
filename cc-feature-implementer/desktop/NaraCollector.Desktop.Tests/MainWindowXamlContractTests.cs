using Xunit;

namespace NaraCollector.Desktop.Tests;

public class MainWindowXamlContractTests
{
    [Fact]
    public void MainWindow_KeepsCommandNamesAndShowsCollectProgress()
    {
        var xaml = File.ReadAllText(LocateDesktopFile("MainWindow.xaml"));

        Assert.Contains("Command=\"{Binding CollectDataCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding StopCollectCommand}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding VisibleAwardNoticeItems}\"", xaml);
        Assert.Contains("x:Name=\"NoticeTrendChart\"", xaml);
        Assert.Contains("Click=\"OpenChartWindowClick\"", xaml);
        Assert.Contains("x:Name=\"AutoCollectButton\"", xaml);
        Assert.Contains("SelectedValue=\"{Binding SelectedRegion, Mode=TwoWay}\"", xaml);
        Assert.Contains("Content=\"업데이트\"", xaml);
        Assert.Contains("Content=\"중지\"", xaml);
        Assert.Contains("Content=\"차트 화면\"", xaml);
        Assert.Contains("동국 참여 낙찰공고", xaml);

        Assert.Contains("Value=\"{Binding LoadProgress, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("Value=\"{Binding LoadProgress}\"", xaml);
        Assert.Contains("Text=\"{Binding LoadStatusText}\"", xaml);
        Assert.Contains("IsLoading", xaml);
        Assert.Contains("BooleanToVisibilityConverter", xaml);
    }

    [Fact]
    public void DesignTokens_DatePickerTextBox_UsesLightBackground()
    {
        var xaml = File.ReadAllText(LocateDesktopFile(Path.Combine("Theme", "DesignTokens.xaml")));

        Assert.DoesNotContain("#101B2B", xaml);
        Assert.Contains("TargetType=\"DatePickerTextBox\"", xaml);
        Assert.Contains("CollectOverlayBorderStyle", xaml);
        Assert.Contains("AwardNoticeDataGridStyle", xaml);
    }

    private static string LocateDesktopFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "NaraCollector.Desktop", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"NaraCollector.Desktop/{relativePath} not found from {AppContext.BaseDirectory}");
    }
}
