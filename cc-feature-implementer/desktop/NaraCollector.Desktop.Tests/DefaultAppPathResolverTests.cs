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
}
