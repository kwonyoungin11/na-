using NaraCollector.Desktop.Services;

var arguments = ParseArguments(args);
var resolver = new DefaultAppPathResolver();
var runner = new CSharpAnalysisPipelineRunner(resolver);

var options = new CSharpPipelineOptions(
    SourceFile: Get(arguments, "--source-file") ?? resolver.ResolveMasterPath(null),
    OutputRoot: Get(arguments, "--output-root") ?? resolver.ResolveConsoleAnalysisRootPath(),
    Region: Get(arguments, "--region") ?? "전국",
    Period: Get(arguments, "--period") ?? "3m",
    MinRate: ParseDouble(Get(arguments, "--min-rate"), 99.5),
    MaxRate: ParseDouble(Get(arguments, "--max-rate"), 100.0));

var statusPath = runner.Execute(options);
Console.WriteLine(statusPath);

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        var current = args[index];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[current] = args[index + 1];
            index++;
            continue;
        }

        result[current] = "true";
    }

    return result;
}

static string? Get(IReadOnlyDictionary<string, string> args, string key)
    => args.TryGetValue(key, out var value) ? value : null;

static double ParseDouble(string? value, double fallback)
    => double.TryParse(value, out var parsed) ? parsed : fallback;
