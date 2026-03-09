using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CompetitionStrategyProfileTests
{
    [Fact]
    public void LoadFromPaths_ReadsAnalysisAndApiRuleProfiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var analysisPath = Path.Combine(tempRoot, "analysis_profile.json");
        var apiRulePath = Path.Combine(tempRoot, "rule_profile.json");

        try
        {
            File.WriteAllText(
                analysisPath,
                """
                {
                  "cli_defaults": { "my_company": "테스트연구원" },
                  "period_days": { "1y": 400, "6m": 200, "3m": 120 },
                  "desktop": {
                    "fixed_windows": {
                      "all": { "min_bids": 9, "concentration_threshold": 0.61 },
                      "1y": { "min_bids": 8, "concentration_threshold": 0.62 },
                      "6m": { "min_bids": 7, "concentration_threshold": 0.63 },
                      "3m": { "min_bids": 6, "concentration_threshold": 0.64 }
                    },
                    "minimum_support_notices": 4,
                    "minimum_award_notices": 2,
                    "nearby_radius_rate": 0.02,
                    "band_threshold_min": 21.0,
                    "band_threshold_ratio": 0.75,
                    "max_band_expand_steps": 3,
                    "evidence_weights": {
                      "shadow": 0.5,
                      "gap": 0.3,
                      "competition": 0.2
                    },
                    "fallback_weights": {
                      "gap": 0.6,
                      "competition": 0.25,
                      "support": 0.15
                    }
                  }
                }
                """);
            File.WriteAllText(
                apiRulePath,
                """
                {
                  "retired_company_tokens": ["퇴역A", "퇴역B"]
                }
                """);

            var profile = CompetitionStrategyProfileProvider.LoadFromPaths(analysisPath, apiRulePath);

            Assert.Equal("테스트연구원", profile.MyCompanyKeyword);
            Assert.Equal(400, profile.Period1YDays);
            Assert.Equal(200, profile.Period6MDays);
            Assert.Equal(120, profile.Period3MDays);
            Assert.Equal(9, profile.FixedAll.MinBids);
            Assert.Equal(0.61, profile.FixedAll.ConcentrationThreshold, 3);
            Assert.Equal(4, profile.MinimumSupportNotices);
            Assert.Equal(2, profile.MinimumAwardNotices);
            Assert.Equal(0.02, profile.NearbyRadiusRate, 3);
            Assert.Equal(21.0, profile.BandThresholdMin, 3);
            Assert.Equal(0.75, profile.BandThresholdRatio, 3);
            Assert.Equal(3, profile.MaxBandExpandSteps);
            Assert.Equal(new[] { "퇴역A", "퇴역B" }, profile.ExcludedCompanyKeywords);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
