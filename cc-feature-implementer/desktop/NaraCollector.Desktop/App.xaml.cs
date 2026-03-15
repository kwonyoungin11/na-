using System.Windows;

using System.IO;
using System.Text;
using System.Windows.Threading;
using NaraCollector.Desktop.Services;

namespace NaraCollector.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        if (ChartImageExporter.TryParse(e.Args ?? [], out var exportRequest)
            && exportRequest is not null)
        {
            try
            {
                ChartImageExporter.Export(exportRequest);
                Shutdown(0);
                return;
            }
            catch (Exception ex)
            {
                WriteCrashLog("ChartExportException", ex);
                Shutdown(1);
                return;
            }
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"예외가 발생했습니다.\n{e.Exception.Message}\n\n로그: {GetCrashLogPath()}",
            "동국문화유산연구원 낙찰분석프로그램",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog("UnhandledException", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void WriteCrashLog(string category, Exception ex)
    {
        try
        {
            var path = GetCrashLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = new StringBuilder();
            payload.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {category}");
            payload.AppendLine(ex.ToString());
            payload.AppendLine(new string('-', 72));
            File.AppendAllText(path, payload.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 로깅 실패는 무시
        }
    }

    private static string GetCrashLogPath()
    {
        var root = DefaultAppPathResolver.ResolveDefaultAppLogDirectory();
        return Path.Combine(root, "app-crash.log");
    }
}
