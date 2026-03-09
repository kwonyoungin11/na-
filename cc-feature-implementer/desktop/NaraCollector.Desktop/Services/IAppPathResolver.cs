namespace NaraCollector.Desktop.Services;

public interface IAppPathResolver
{
    string ResolveMasterPath(string? providedPath);

    string ResolveOutputDirectory(string? providedPath);

    string? ResolveEnvPath();

    string ResolveSkillScriptPath();

    string ResolveAnalysisProfilePath();

    string ResolveApiRuleProfilePath();

    string ResolveConsoleAnalysisRootPath();
}
