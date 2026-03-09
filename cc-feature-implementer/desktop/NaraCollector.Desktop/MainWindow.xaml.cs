using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NaraCollector.Desktop.Controls;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.ViewModels;

namespace NaraCollector.Desktop;

public partial class MainWindow : Window
{
    private static readonly TimeSpan AutoCollectInterval = TimeSpan.FromMinutes(1);

    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _autoCollectTimer;
    private ChartPopupWindow? _chartPopupWindow;
    private bool _autoCollectEnabled;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        _viewModel.RankAlertsDetected += ViewModelRankAlertsDetected;
        _autoCollectTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AutoCollectInterval,
        };
        _autoCollectTimer.Tick += AutoCollectTimerTick;

        DataContext = _viewModel;
        Loaded += MainWindowLoaded;
        Closed += MainWindowClosed;
    }

    private void MainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 시작 시 자동 로딩은 하지 않는다.
        // 이후 필터 변경 반영은 ViewModel의 필터 동기화 로직에서 처리한다.
        UpdateAutoCollectButtonContent();
        Dispatcher.BeginInvoke(
            () => BringWindowToFront(this),
            DispatcherPriority.ApplicationIdle);
    }

    private void MainWindowClosed(object? sender, EventArgs e)
    {
        _autoCollectTimer.Stop();
        _autoCollectTimer.Tick -= AutoCollectTimerTick;
        _viewModel.RankAlertsDetected -= ViewModelRankAlertsDetected;

        if (_chartPopupWindow is null)
        {
            return;
        }

        try
        {
            _chartPopupWindow.CloseFromOwner();
        }
        catch
        {
            // 창 종료 중 예외는 무시
        }
    }

    private void OpenChartWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOrActivateChartWindow();
    }

    private void OpenChartWindowAreaClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenOrActivateChartWindow();
    }

    private void ToggleAutoCollectClick(object sender, RoutedEventArgs e)
    {
        _autoCollectEnabled = !_autoCollectEnabled;
        if (_autoCollectEnabled)
        {
            _viewModel.StartRealtimeAutoCollectSession(DateTime.Now);
            _autoCollectTimer.Start();
            _viewModel.AppendExternalEvent($"1분 자동수집 시작 ({DateTime.Now:HH:mm:ss})");
            TriggerAutoCollect();
        }
        else
        {
            _autoCollectTimer.Stop();
            _viewModel.StopRealtimeAutoCollectSession();
            _viewModel.AppendExternalEvent($"1분 자동수집 중지 ({DateTime.Now:HH:mm:ss})");
        }

        UpdateAutoCollectButtonContent();
    }

    private void MainFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 필터 선택 반영은 ViewModel 바인딩/자동 적용 로직에서 처리한다.
    }

    private void MainFilterDateChanged(object sender, SelectionChangedEventArgs e)
    {
        // 날짜 변경 반영은 ViewModel 바인딩/자동 적용 로직에서 처리한다.
    }

    private void MainFilterCheckedChanged(object sender, RoutedEventArgs e)
    {
        // 체크 변경 반영은 ViewModel 바인딩/자동 적용 로직에서 처리한다.
    }

    private void MainChartCompanyPicked(object sender, CompanyPickedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.CompanyName))
        {
            return;
        }

        _viewModel.ApplyCompanyPickFromChart(e.CompanyName);
    }

    private void MainChartRateRangeSelected(object sender, RateRangeSelectedEventArgs e)
    {
        var min = Math.Clamp(Math.Min(e.MinRate, e.MaxRate), 98.0, 102.0);
        var max = Math.Clamp(Math.Max(e.MinRate, e.MaxRate), 98.0, 102.0);
        _viewModel.SelectedMinAdjustRate = min.ToString("F5");
        _viewModel.SelectedMaxAdjustRate = max.ToString("F5");
    }

    private void MainChartHoveredRateChanged(object sender, HoveredRateChangedEventArgs e)
    {
        _viewModel.UpdateHoveredRate(e.Rate);
    }

    private void OpenOrActivateChartWindow()
    {
        EnsureChartWindowVisible();
    }

    private void AutoCollectTimerTick(object? sender, EventArgs e)
    {
        TriggerAutoCollect();
    }

    private void TriggerAutoCollect()
    {
        if (!_autoCollectEnabled || _viewModel.IsLoading || !_viewModel.CollectDataCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.CollectDataCommand.Execute(null);
    }

    private void UpdateAutoCollectButtonContent()
    {
        if (AutoCollectButton is null)
        {
            return;
        }

        AutoCollectButton.Content = _autoCollectEnabled
            ? "1분 자동수집 중지"
            : "1분 자동수집 시작";
    }

    private void ViewModelRankAlertsDetected(object? sender, IReadOnlyList<RankAlertModel> alerts)
    {
        if (alerts.Count == 0)
        {
            return;
        }

        var latestAlerts = alerts
            .OrderByDescending(x => x.OpenDate)
            .Take(3)
            .ToList();
        var lines = latestAlerts
            .Select(x =>
                $"• {x.OpenDate:MM-dd HH:mm} [{x.NoticeNumber}] {x.Rank}위 ({x.CompanyName})");
        var message = string.Join(Environment.NewLine, lines);
        if (alerts.Count > latestAlerts.Count)
        {
            message += $"{Environment.NewLine}외 {alerts.Count - latestAlerts.Count:N0}건";
        }

        MessageBox.Show(
            this,
            message,
            "동국문화재연구원 순위 알림",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void EnsureChartWindowVisible()
    {
        if (_chartPopupWindow is null)
        {
            _chartPopupWindow = new ChartPopupWindow(_viewModel)
            {
                Owner = this,
            };
            _chartPopupWindow.Closed += (_, _) => _chartPopupWindow = null;
        }

        if (!_chartPopupWindow.IsVisible)
        {
            try
            {
                _chartPopupWindow.Show();
            }
            catch (InvalidOperationException)
            {
                // 내부 상태가 이미 폐기된 경우 새 창으로 재생성
                _chartPopupWindow = new ChartPopupWindow(_viewModel)
                {
                    Owner = this,
                };
                _chartPopupWindow.Closed += (_, _) => _chartPopupWindow = null;
                _chartPopupWindow.Show();
            }
        }
        else if (_chartPopupWindow.WindowState == WindowState.Minimized)
        {
            _chartPopupWindow.WindowState = WindowState.Normal;
        }

        BringWindowToFront(_chartPopupWindow);
    }

    private static void BringWindowToFront(Window targetWindow)
    {
        if (targetWindow.WindowState == WindowState.Minimized)
        {
            targetWindow.WindowState = WindowState.Normal;
        }

        targetWindow.ShowInTaskbar = true;
        var originalTopmost = targetWindow.Topmost;
        targetWindow.Topmost = true;
        targetWindow.Activate();
        targetWindow.Focus();
        targetWindow.Topmost = originalTopmost;
    }
}
