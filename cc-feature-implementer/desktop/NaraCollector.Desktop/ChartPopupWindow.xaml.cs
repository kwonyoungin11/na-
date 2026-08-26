using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using NaraCollector.Desktop.Controls;
using NaraCollector.Desktop.ViewModels;

namespace NaraCollector.Desktop;

public partial class ChartPopupWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _allowClose;

    public ChartPopupWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel.CreateChartPopupViewModel();
        DataContext = _viewModel;
        Closing += ChartPopupWindowClosing;
    }

    public void RefreshFrom(MainWindowViewModel source)
    {
        _viewModel.CopyPopupFilterStateFrom(source);
    }

    private void ResetMapClick(object sender, RoutedEventArgs e)
    {
        ScatterMap.ResetMapToCenter();
    }

    public void CloseFromOwner()
    {
        _allowClose = true;
        Close();
    }

    private void ChartPopupWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        // 사용자가 X를 눌렀을 때 종료하지 않고 숨김 처리하여
        // 다음 "그래프 창 열기" 시 즉시 복귀하도록 한다.
        e.Cancel = true;
        Hide();
    }

    private void GlobalFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 필터 선택 반영은 ViewModel 바인딩/자동 적용 로직에서 처리한다.
    }

    private void ChartCompanyPicked(object sender, CompanyPickedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.CompanyName))
        {
            return;
        }

        _viewModel.ApplyCompanyPickFromChart(e.CompanyName);
    }

    private void ChartRateRangeSelected(object sender, RateRangeSelectedEventArgs e)
    {
        var min = Math.Clamp(Math.Min(e.MinRate, e.MaxRate), 98.0, 102.0);
        var max = Math.Clamp(Math.Max(e.MinRate, e.MaxRate), 98.0, 102.0);
        _viewModel.SelectedMinAdjustRate = min.ToString("F5");
        _viewModel.SelectedMaxAdjustRate = max.ToString("F5");
    }

    private void ChartHoveredRateChanged(object sender, HoveredRateChangedEventArgs e)
    {
        _viewModel.UpdateHoveredRate(e.Rate);
    }

    private void ResetCompanySelectionClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetCompanySelectionToAll();
    }
}
