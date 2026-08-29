using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class DefaultAppPathResolverTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void ResolveMasterPath_WithProvidedPath_ReturnsProvidedValue()
    {
        var resolver = new DefaultAppPathResolver();
        var provided = "/tmp/custom-master.xlsx";

        var resolved = resolver.ResolveMasterPath(provided);

        Assert.Equal(provided, resolved);
    }

    [Fact]
    public void ResolveOutputDirectory_WithProvidedPath_ReturnsProvidedValue()
    {
        var resolver = new DefaultAppPathResolver();
        var provided = "/tmp/custom-output";

        var resolved = resolver.ResolveOutputDirectory(provided);

        Assert.Equal(provided, resolved);
    }

    [Fact]
    public void ResolveMasterPath_DefaultsToWorkspaceRootWhenWorkspaceEnvIsSet()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousMaster = Environment.GetEnvironmentVariable("NARA_MASTER_XLSX");
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"nara-workspace-{Guid.NewGuid():N}");
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, tempWorkspace);
                Environment.SetEnvironmentVariable("NARA_MASTER_XLSX", null);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveMasterPath(null);

                Assert.Equal(Path.Combine(tempWorkspace, "master", "전체_통합_데이터.xlsx"), resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable("NARA_MASTER_XLSX", previousMaster);
            }
        }
    }

    [Fact]
    public void ResolveOutputDirectory_DefaultsToWorkspaceRootWhenWorkspaceEnvIsSet()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousOutput = Environment.GetEnvironmentVariable("NARA_API_OUTPUT_DIR");
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"nara-workspace-{Guid.NewGuid():N}");
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, tempWorkspace);
                Environment.SetEnvironmentVariable("NARA_API_OUTPUT_DIR", null);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveOutputDirectory(null);

                Assert.Equal(Path.Combine(tempWorkspace, "output"), resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable("NARA_API_OUTPUT_DIR", previousOutput);
            }
        }
    }

    [Fact]
    public void ResolveSkillScriptPath_PrioritizesEnvironmentScriptWhenFileExists()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("NARA_API_MASTER_SYNC_SCRIPT");
            var tempScript = Path.Combine(Path.GetTempPath(), $"resolver-test-{Guid.NewGuid():N}.py");
            try
            {
                File.WriteAllText(tempScript, "print('ok')");
                Environment.SetEnvironmentVariable("NARA_API_MASTER_SYNC_SCRIPT", tempScript);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveSkillScriptPath();

                Assert.Equal(tempScript, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NARA_API_MASTER_SYNC_SCRIPT", previous);
                if (File.Exists(tempScript))
                {
                    File.Delete(tempScript);
                }
            }
        }
    }

    [Fact]
    public void ResolveEnvPath_PrioritizesEnvironmentSetting()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("NARA_API_ENV_FILE");
            var tempEnv = Path.Combine(Path.GetTempPath(), $"resolver-test-{Guid.NewGuid():N}.env");
            try
            {
                File.WriteAllText(tempEnv, "NARA_SERVICE_KEY=demo");
                Environment.SetEnvironmentVariable("NARA_API_ENV_FILE", tempEnv);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveEnvPath();

                Assert.Equal(tempEnv, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NARA_API_ENV_FILE", previous);
                if (File.Exists(tempEnv))
                {
                    File.Delete(tempEnv);
                }
            }
        }
    }

    [Fact]
    public void ResolveEnvPath_UsesWorkspaceEnvFileWhenWorkspaceRootIsSet()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousEnv = Environment.GetEnvironmentVariable("NARA_API_ENV_FILE");
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"nara-workspace-{Guid.NewGuid():N}");
            var tempEnv = Path.Combine(tempWorkspace, ".env");
            try
            {
                Directory.CreateDirectory(tempWorkspace);
                File.WriteAllText(tempEnv, "SERVICE_KEY=demo");
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, tempWorkspace);
                Environment.SetEnvironmentVariable("NARA_API_ENV_FILE", null);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveEnvPath();

                Assert.Equal(tempEnv, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable("NARA_API_ENV_FILE", previousEnv);
                if (Directory.Exists(tempWorkspace))
                {
                    Directory.Delete(tempWorkspace, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void ResolveAnalysisProfilePath_PrioritizesEnvironmentSetting()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("NARA_ANALYSIS_PROFILE_JSON");
            var tempFile = Path.Combine(Path.GetTempPath(), $"analysis-profile-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(tempFile, "{\"profile_name\":\"test\"}");
                Environment.SetEnvironmentVariable("NARA_ANALYSIS_PROFILE_JSON", tempFile);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveAnalysisProfilePath();

                Assert.Equal(tempFile, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NARA_ANALYSIS_PROFILE_JSON", previous);
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }

    [Fact]
    public void ResolveApiRuleProfilePath_PrioritizesEnvironmentSetting()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("NARA_API_RULE_PROFILE_JSON");
            var tempFile = Path.Combine(Path.GetTempPath(), $"api-rule-profile-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(tempFile, "{\"profile_name\":\"api-test\"}");
                Environment.SetEnvironmentVariable("NARA_API_RULE_PROFILE_JSON", tempFile);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveApiRuleProfilePath();

                Assert.Equal(tempFile, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NARA_API_RULE_PROFILE_JSON", previous);
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }

    [Fact]
    public void ResolveConsoleAnalysisRootPath_PrioritizesEnvironmentSetting()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR");
            var tempDir = Path.Combine(Path.GetTempPath(), $"console-analysis-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempDir);
                Environment.SetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR", tempDir);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveConsoleAnalysisRootPath();

                Assert.Equal(tempDir, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR", previous);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void ResolveConsoleAnalysisRootPath_DefaultsToWorkspaceAnalysisWhenWorkspaceEnvIsSet()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousAnalysis = Environment.GetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR");
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"nara-workspace-{Guid.NewGuid():N}");
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, tempWorkspace);
                Environment.SetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR", null);

                var resolver = new DefaultAppPathResolver();
                var resolved = resolver.ResolveConsoleAnalysisRootPath();

                Assert.Equal(Path.Combine(tempWorkspace, "analysis"), resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable("NARA_CONSOLE_ANALYSIS_DIR", previousAnalysis);
            }
        }
    }

    [Fact]
    public void ResolveDefaultWorkspaceRoot_WhenEnvUnset_DoesNotUseMissingADrive()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousAppRoot = Environment.GetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, null);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, null);

                var root = DefaultAppPathResolver.ResolveDefaultWorkspaceRoot();
                var drive = Path.GetPathRoot(Path.GetFullPath(root));

                Assert.False(string.IsNullOrWhiteSpace(drive));
                Assert.True(Directory.Exists(drive), $"workspace root drive missing: {root}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, previousAppRoot);
            }
        }
    }

    [Fact]
    public void ResolveDefaultAppLogDirectory_WhenEnvUnset_ParentDriveExists()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousAppRoot = Environment.GetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, null);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, null);

                var logs = DefaultAppPathResolver.ResolveDefaultAppLogDirectory();
                var drive = Path.GetPathRoot(Path.GetFullPath(logs));

                Assert.False(string.IsNullOrWhiteSpace(drive));
                Assert.True(Directory.Exists(drive), $"log directory drive missing: {logs}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, previousAppRoot);
            }
        }
    }

    [Fact]
    public void ResolveDefaultAppRootDirectory_PrioritizesAppRootEnvironmentSetting()
    {
        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable);
            var tempAppRoot = Path.Combine(Path.GetTempPath(), $"nara-app-{Guid.NewGuid():N}");
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, tempAppRoot);

                var resolved = DefaultAppPathResolver.ResolveDefaultAppRootDirectory();

                Assert.Equal(tempAppRoot, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, previous);
            }
        }
    }

    [Fact]
    public void ResolveDefaultAppRootDirectory_UsesWorkspaceRootWhenPresent()
    {
        lock (EnvLock)
        {
            var previousWorkspace = Environment.GetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable);
            var previousAppRoot = Environment.GetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable);
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"nara-workspace-{Guid.NewGuid():N}");
            try
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, tempWorkspace);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, null);

                var resolved = DefaultAppPathResolver.ResolveDefaultAppRootDirectory();

                Assert.Equal(Path.Combine(tempWorkspace, "app"), resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.WorkspaceRootEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable(DefaultAppPathResolver.AppRootEnvironmentVariable, previousAppRoot);
            }
        }
    }
}
