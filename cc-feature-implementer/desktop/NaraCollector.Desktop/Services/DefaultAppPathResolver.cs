using System.IO;

namespace NaraCollector.Desktop.Services;

public sealed class DefaultAppPathResolver : IAppPathResolver
{
    public const string WorkspaceRootEnvironmentVariable = "NARA_WORKSPACE_ROOT";
    public const string AppRootEnvironmentVariable = "NARA_APP_ROOT";

    private const string WorkspaceLinuxRoot = "/mnt/a/c/nara-workspace";
    private const string WorkspaceWindowsRoot = @"A:\c\nara-workspace";
    private const string SkillScriptLinuxPath = "/mnt/a/c/.agents/skills/API/scripts/run_master_sync_pipeline.py";
    private const string SkillScriptWindowsPath = @"A:\c\.agents\skills\API\scripts\run_master_sync_pipeline.py";
    private const string ProjectEnvLinuxPath = "/mnt/a/c/.env";
    private const string ProjectEnvWindowsPath = @"A:\c\.env";
    private const string AnalysisProfileLinuxPath = "/mnt/a/c/.agents/skills/shadow-strategy/analysis_profile.json";
    private const string AnalysisProfileWindowsPath = @"A:\c\.agents\skills\shadow-strategy\analysis_profile.json";
    private const string ApiRuleProfileLinuxPath = "/mnt/a/c/.agents/skills/API/rule_profile.json";
    private const string ApiRuleProfileWindowsPath = @"A:\c\.agents\skills\API\rule_profile.json";

    public static string ResolveDefaultWorkspaceRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        foreach (var candidate in GetWorkspaceRootCandidates())
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    public static string ResolveDefaultAppRootDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable(AppRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        return Path.Combine(ResolveDefaultWorkspaceRoot(), "app");
    }

    public static string ResolveDefaultAppLogDirectory()
        => Path.Combine(ResolveDefaultAppRootDirectory(), "logs");

    public string ResolveMasterPath(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            return providedPath.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable("NARA_MASTER_XLSX");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        var workspaceFromEnv = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(workspaceFromEnv))
        {
            return Path.Combine(workspaceFromEnv.Trim(), "master", "전체_통합_데이터.xlsx");
        }

        var runtimeCandidates = GetWorkspaceRootCandidates()
            .Select(root => Path.Combine(root, "master", "전체_통합_데이터.xlsx"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in runtimeCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return runtimeCandidates[0];
    }

    public string ResolveOutputDirectory(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            return providedPath.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable("NARA_API_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        var workspaceFromEnv = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(workspaceFromEnv))
        {
            return Path.Combine(workspaceFromEnv.Trim(), "output");
        }

        foreach (var candidate in GetWorkspaceRootCandidates()
                     .Select(root => Path.Combine(root, "output")))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(ResolveDefaultWorkspaceRoot(), "output");
    }

    public string? ResolveEnvPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NARA_API_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        var candidates = GetWorkspaceRootCandidates()
            .Select(root => Path.Combine(root, ".env"))
            .Concat(OperatingSystem.IsWindows()
                ? new[] { ProjectEnvWindowsPath, ProjectEnvLinuxPath }
                : new[] { ProjectEnvLinuxPath, ProjectEnvWindowsPath })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.FirstOrDefault(File.Exists);
    }

    public string ResolveSkillScriptPath()
    {
        var fromEnvScript = Environment.GetEnvironmentVariable("NARA_API_MASTER_SYNC_SCRIPT");
        if (!string.IsNullOrWhiteSpace(fromEnvScript) && File.Exists(fromEnvScript))
        {
            return fromEnvScript.Trim();
        }

        var fromEnvRoot = Environment.GetEnvironmentVariable("NARA_API_SKILL_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnvRoot))
        {
            var candidate = Path.Combine(fromEnvRoot.Trim(), "scripts", "run_master_sync_pipeline.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var root in EnumerateDirectoryAncestors(AppContext.BaseDirectory, 8))
        {
            var candidate = Path.Combine(root, ".agents", "skills", "API", "scripts", "run_master_sync_pipeline.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (File.Exists(SkillScriptLinuxPath))
        {
            return SkillScriptLinuxPath;
        }

        if (File.Exists(SkillScriptWindowsPath))
        {
            return SkillScriptWindowsPath;
        }

        return OperatingSystem.IsWindows() ? SkillScriptWindowsPath : SkillScriptLinuxPath;
    }

    public string ResolveAnalysisProfilePath()
    {
        return ResolveOptionalPath(
            environmentVariable: "NARA_ANALYSIS_PROFILE_JSON",
            linkedFileName: "analysis_profile.json",
            linuxFallback: AnalysisProfileLinuxPath,
            windowsFallback: AnalysisProfileWindowsPath);
    }

    public string ResolveApiRuleProfilePath()
    {
        return ResolveOptionalPath(
            environmentVariable: "NARA_API_RULE_PROFILE_JSON",
            linkedFileName: "rule_profile.json",
            linuxFallback: ApiRuleProfileLinuxPath,
            windowsFallback: ApiRuleProfileWindowsPath);
    }

    public string ResolveConsoleAnalysisRootPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        var workspaceFromEnv = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(workspaceFromEnv))
        {
            return Path.Combine(workspaceFromEnv.Trim(), "analysis");
        }

        var candidates = GetWorkspaceRootCandidates()
            .Select(root => Path.Combine(root, "analysis"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static IEnumerable<string> GetWorkspaceRootCandidates()
    {
        var fromEnv = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv.Trim();
        }

        foreach (var ancestor in EnumerateDirectoryAncestors(AppContext.BaseDirectory, 8))
        {
            if (LooksLikeWorkspaceRoot(ancestor))
            {
                yield return ancestor;
            }
        }

        if (Directory.Exists(WorkspaceWindowsRoot))
        {
            yield return WorkspaceWindowsRoot;
        }

        if (Directory.Exists(WorkspaceLinuxRoot))
        {
            yield return WorkspaceLinuxRoot;
        }
    }

    private static bool LooksLikeWorkspaceRoot(string root)
    {
        return Directory.Exists(Path.Combine(root, ".agents"))
            && (Directory.Exists(Path.Combine(root, "cc-feature-implementer"))
                || File.Exists(Path.Combine(root, "AGENTS.md")));
    }

    private static string ResolveOptionalPath(
        string environmentVariable,
        string linkedFileName,
        string linuxFallback,
        string windowsFallback)
    {
        var fromEnv = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv.Trim();
        }

        var localCandidate = Path.Combine(AppContext.BaseDirectory, linkedFileName);
        if (File.Exists(localCandidate))
        {
            return localCandidate;
        }

        foreach (var root in EnumerateDirectoryAncestors(AppContext.BaseDirectory, 8))
        {
            var candidate = linkedFileName switch
            {
                "analysis_profile.json" => Path.Combine(root, ".agents", "skills", "shadow-strategy", linkedFileName),
                "rule_profile.json" => Path.Combine(root, ".agents", "skills", "API", linkedFileName),
                _ => Path.Combine(root, linkedFileName),
            };

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (File.Exists(linuxFallback))
        {
            return linuxFallback;
        }

        if (File.Exists(windowsFallback))
        {
            return windowsFallback;
        }

        return OperatingSystem.IsWindows() ? windowsFallback : linuxFallback;
    }

    private static IEnumerable<string> EnumerateDirectoryAncestors(string startPath, int maxDepth)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        var depth = 0;
        while (current is not null && depth <= maxDepth)
        {
            yield return current.FullName;
            current = current.Parent;
            depth++;
        }
    }
}
