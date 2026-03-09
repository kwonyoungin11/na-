using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CompetitionAnalysisProfileTests
{
    [Fact]
    public void LoadFromPaths_ReadsTopLevelAnalysisProfileValues()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"competition-analysis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var analysisPath = Path.Combine(tempRoot, "analysis_profile.json");
        var apiRulePath = Path.Combine(tempRoot, "rule_profile.json");

        try
        {
            File.WriteAllText(
                analysisPath,
                """
                {
                  "cli_defaults": { "my_company": "테스트문화재연구원" },
                  "rate_range": { "min": 99.6, "max": 100.1, "zone_size": 0.002 },
                  "period_days": { "1m": 31, "1y": 400 },
                  "fixed_bidder": {
                    "min_bids": 7,
                    "concentration_threshold": 44.5,
                    "window_days": { "recent": 28, "short": 88, "medium": 188 }
                  },
                  "shadow": {
                    "base_offset": 0.0012,
                    "minimum_priority_score": 41.0,
                    "dedupe_precision": 4
                  },
                  "same_day_collapse": {
                    "rate_tolerance": 0.004,
                    "min_cluster_size": 3
                  },
                  "decision_rules": {
                    "forbidden_density_score": 71.5,
                    "boundary_clear_min_distance": 0.003
                  }
                }
                """);
            File.WriteAllText(
                apiRulePath,
                """
                {
                  "retired_company_tokens": ["효림", "한국문화재연구원"]
                }
                """);

            var profile = CompetitionAnalysisProfileProvider.LoadFromPaths(analysisPath, apiRulePath);

            Assert.Equal("테스트문화재연구원", profile.MyCompanyKeyword);
            Assert.Equal(99.6, profile.RateMin, 3);
            Assert.Equal(100.1, profile.RateMax, 3);
            Assert.Equal(0.002, profile.ZoneSize, 3);
            Assert.Equal(31, profile.PeriodDays["1m"]);
            Assert.Equal(400, profile.PeriodDays["1y"]);
            Assert.Equal(7, profile.FixedMinBids);
            Assert.Equal(44.5, profile.FixedConcentrationThreshold, 3);
            Assert.Equal(28, profile.FixedWindowDays["recent"]);
            Assert.Equal(88, profile.FixedWindowDays["short"]);
            Assert.Equal(188, profile.FixedWindowDays["medium"]);
            Assert.Equal(0.0012, profile.ShadowBaseOffset, 4);
            Assert.Equal(41.0, profile.ShadowMinimumPriorityScore, 3);
            Assert.Equal(4, profile.ShadowDedupePrecision);
            Assert.Equal(0.004, profile.SameDayClusterRateTolerance, 4);
            Assert.Equal(3, profile.SameDayClusterMinClusterSize);
            Assert.Equal(71.5, profile.DecisionForbiddenDensityScore, 3);
            Assert.Equal(0.003, profile.DecisionBoundaryClearMinDistance, 3);
            Assert.Equal(new[] { "효림", "한국문화재연구원" }, profile.ExcludedCompanyKeywords);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
