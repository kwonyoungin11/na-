using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

internal sealed class NaraApiScriptRunner
{
    private const int DefaultScriptTimeoutSeconds = 3600;
    private const int MinScriptTimeoutSeconds = 60;

    public async Task<ApiSkillRunResult> RunAsync(
        string scriptPath,
        string startMonth,
        string endMonth,
        string startDateTime,
        string endDateTime,
        string keywords,
        string masterPath,
        string outputDirectory,
        string collectionLogPath,
        string? envPath,
        IProgress<LoadingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var timeout = ResolveScriptTimeout();
        var cppExe = ResolveCppCollectorPath();
        if (!string.IsNullOrWhiteSpace(cppExe) && File.Exists(cppExe))
        {
            var cppArgs = new List<string>
            {
                "--start-month", startMonth,
                "--end-month", endMonth,
                "--env", envPath ?? string.Empty,
                "--out", Path.Combine(outputDirectory, "cpp_award_collect.jsonl"),
            };
            progress?.Report(new LoadingProgress("collect", 22, $"C++ 낙찰 수집: {Path.GetFileName(cppExe)} {startMonth}~{endMonth}"));
            var cppResult = await ExecuteProcessAsync(cppExe, cppArgs, timeout, progress, cancellationToken);
            if (cppResult.Success)
            {
                return cppResult with { SourceLabel = $"C++ 낙찰 {startMonth}~{endMonth} 수집" };
            }

            return cppResult;
        }

        var launchers = ResolvePythonLaunchers();
        ApiSkillRunResult? lastResult = null;

        foreach (var launcher in launchers)
        {
            var args = new List<string>(launcher.PrefixArgs)
            {
                scriptPath,
                "--start-month", startMonth,
                "--end-month", endMonth,
                "--start-datetime", startDateTime,
                "--end-datetime", endDateTime,
                "--keywords", keywords,
                "--master-path", masterPath,
                "--output-dir", outputDirectory,
                "--collection-log", collectionLogPath,
                "--months", CollectorNoticeRules.CollectLookbackMonths.ToString(CultureInfo.InvariantCulture),
                "--min-participants", CollectorNoticeRules.MinParticipatingCompanies.ToString(CultureInfo.InvariantCulture),
            };

            if (!string.IsNullOrWhiteSpace(envPath))
            {
                args.Add("--key-source-env");
                args.Add(envPath);
            }

            progress?.Report(new LoadingProgress("collect", 22, $"파이썬 실행기 시도: {launcher.DisplayName} · timeout={timeout.TotalMinutes:F1}분"));
            var result = await ExecuteProcessAsync(launcher.FileName, args, timeout, progress, cancellationToken);
            if (result.StartError is null)
            {
                return result;
            }

            lastResult = result;
            progress?.Report(new LoadingProgress("collect", 24, $"실행기 실패({launcher.DisplayName}): {result.StartError}"));
        }

        return lastResult ?? new ApiSkillRunResult(
            Success: false,
            RawRowCount: 0,
            Summary: "사용 가능한 파이썬 실행기를 찾지 못했습니다.",
            SourceLabel: "API Skill 실행 실패",
            StartError: "python 실행 불가",
            RawMasterPath: null,
            FinalMasterPath: null);
    }

    private static IReadOnlyList<PythonLauncher> ResolvePythonLaunchers()
    {
        var launchers = new List<PythonLauncher>();
        var fromEnv = Environment.GetEnvironmentVariable("NARA_PYTHON");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            launchers.Add(new PythonLauncher(fromEnv.Trim(), Array.Empty<string>(), fromEnv.Trim()));
        }

        if (OperatingSystem.IsWindows())
        {
            launchers.Add(new PythonLauncher("python", Array.Empty<string>(), "python"));
            launchers.Add(new PythonLauncher("py", new[] { "-3" }, "py -3"));
        }
        else
        {
            launchers.Add(new PythonLauncher("python3", Array.Empty<string>(), "python3"));
            launchers.Add(new PythonLauncher("python", Array.Empty<string>(), "python"));
        }

        return launchers;
    }

    private static string? ResolveCppCollectorPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NARA_CPP_COLLECT");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv.Trim()))
        {
            return fromEnv.Trim();
        }

        var candidates = new[]
        {
            @"A:\c\cc-feature-implementer\desktop\nara_collect_cpp\build\Release\nara_award_collect.exe",
            Path.Combine(AppContext.BaseDirectory, "nara_award_collect.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<ApiSkillRunResult> ExecuteProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        IProgress<LoadingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new ApiSkillRunResult(
                Success: false,
                RawRowCount: 0,
                Summary: $"프로세스 시작 실패: {ex.Message}",
                SourceLabel: "API Skill 실행 실패",
                StartError: ex.Message,
                RawMasterPath: null,
                FinalMasterPath: null);
        }

        var stdoutTask = ReadLinesAsync(process.StandardOutput, progress, "api");
        var stderrTask = ReadLinesAsync(process.StandardError, progress, "api");

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            var timeoutStdout = await stdoutTask;
            var timeoutStderr = await stderrTask;
            var timeoutLogs = timeoutStdout
                .Concat(timeoutStderr)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var timeoutSummary = BuildProcessSummary(
                processExitCode: process.HasExited ? process.ExitCode : null,
                logs: timeoutLogs,
                fallback: $"실행 시간 초과: {timeout.TotalMinutes:F1}분");

            return new ApiSkillRunResult(
                Success: false,
                RawRowCount: ParseRawRowCount(timeoutLogs),
                Summary: timeoutSummary,
                SourceLabel: "API Skill 실행 실패",
                StartError: null,
                RawMasterPath: ExtractOutputPath(timeoutLogs, "raw_master"),
                FinalMasterPath: ExtractOutputPath(timeoutLogs, "final_master"));
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var mergedLogs = stdout.Concat(stderr).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var rawRows = ParseRawRowCount(mergedLogs);
        var summary = BuildProcessSummary(process.ExitCode, mergedLogs, fallback: "로그 없음");

        var sourceLabel = process.ExitCode == 0
            ? "API Skill: run_master_sync_pipeline.py"
            : "API Skill 실행 실패";

        return new ApiSkillRunResult(
            Success: process.ExitCode == 0,
            RawRowCount: rawRows,
            Summary: summary,
            SourceLabel: sourceLabel,
            StartError: null,
            RawMasterPath: ExtractOutputPath(mergedLogs, "raw_master"),
            FinalMasterPath: ExtractOutputPath(mergedLogs, "final_master"));
    }

    private static string BuildProcessSummary(
        int? processExitCode,
        IReadOnlyList<string> logs,
        string fallback)
    {
        var summaryBuilder = new StringBuilder();
        if (processExitCode.HasValue)
        {
            summaryBuilder.Append($"exit={processExitCode.Value}");
        }
        else
        {
            summaryBuilder.Append("exit=unknown");
        }

        if (logs.Count > 0)
        {
            var tail = logs.TakeLast(3);
            summaryBuilder.Append(" · ").Append(string.Join(" | ", tail));
            return summaryBuilder.ToString();
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            summaryBuilder.Append(" · ").Append(fallback);
        }

        return summaryBuilder.ToString();
    }

    private static async Task<List<string>> ReadLinesAsync(
        StreamReader reader,
        IProgress<LoadingProgress>? progress,
        string stage)
    {
        var lines = new List<string>();
        var stopwatch = Stopwatch.StartNew();
        var lastReportedAt = TimeSpan.Zero;
        while (await reader.ReadLineAsync() is { } line)
        {
            var text = line.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            lines.Add(text);
            if (progress is null)
            {
                continue;
            }

            var elapsed = stopwatch.Elapsed;
            var isImportant =
                text.Contains("error", StringComparison.OrdinalIgnoreCase)
                || text.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || text.Contains("[완료]", StringComparison.OrdinalIgnoreCase)
                || text.Contains("completed", StringComparison.OrdinalIgnoreCase);
            if (isImportant || lines.Count <= 3 || elapsed - lastReportedAt >= TimeSpan.FromMilliseconds(450))
            {
                progress.Report(new LoadingProgress(stage, 26, text));
                lastReportedAt = elapsed;
            }
        }

        if (progress is not null && lines.Count > 3)
        {
            progress.Report(new LoadingProgress(stage, 26, $"로그 {lines.Count:N0}줄 수집"));
        }

        return lines;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 중단 시 kill 실패는 무시
        }
    }

    private static int ParseRawRowCount(IReadOnlyList<string> logs)
    {
        var patterns = new[]
        {
            new Regex(@"raw_rows\s*:\s*(?<n>[\d,]+)", RegexOptions.IgnoreCase),
            new Regex(@"final_rows\s*:\s*(?<n>[\d,]+)", RegexOptions.IgnoreCase),
        };

        foreach (var line in logs.AsEnumerable().Reverse())
        {
            foreach (var pattern in patterns)
            {
                var match = pattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var raw = match.Groups["n"].Value.Replace(",", "", StringComparison.Ordinal);
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static string? ExtractOutputPath(IReadOnlyList<string> logs, string token)
    {
        if (logs.Count == 0)
        {
            return null;
        }

        var pattern = new Regex($@"{Regex.Escape(token)}\s*:\s*(?<path>.+)$", RegexOptions.IgnoreCase);
        foreach (var line in logs.AsEnumerable().Reverse())
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var rawPath = match.Groups["path"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var normalized = rawPath.Trim('"', '\'');
            return normalized;
        }

        return null;
    }

    private static TimeSpan ResolveScriptTimeout()
    {
        var value = Environment.GetEnvironmentVariable("NARA_API_SCRIPT_TIMEOUT_SECONDS");
        if (!string.IsNullOrWhiteSpace(value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            var normalized = Math.Max(MinScriptTimeoutSeconds, parsed);
            return TimeSpan.FromSeconds(normalized);
        }

        return TimeSpan.FromSeconds(DefaultScriptTimeoutSeconds);
    }

    private sealed record PythonLauncher(
        string FileName,
        IReadOnlyList<string> PrefixArgs,
        string DisplayName);
}

internal sealed record ApiSkillRunResult(
    bool Success,
    int RawRowCount,
    string Summary,
    string SourceLabel,
    string? StartError,
    string? RawMasterPath,
    string? FinalMasterPath);
