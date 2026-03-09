using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class ConsolePipelineInsightServiceTests
{
    [Fact]
    public void Load_ReadsCompletedPipelineAndTopRecommendation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"console-pipeline-{Guid.NewGuid():N}");
        var shadowDir = Path.Combine(root, "shadow_전국");
        var crowdingDir = Path.Combine(root, "crowding_전국");
        Directory.CreateDirectory(shadowDir);
        Directory.CreateDirectory(crowdingDir);

        try
        {
            File.WriteAllText(
                Path.Combine(shadowDir, "pipeline_status.json"),
                $$"""
                {
                  "snapshot_signature": "{{CSharpAnalysisPipelineRunner.CurrentSnapshotSignature}}",
                  "generated_at_utc": "2026-03-09T00:00:00Z",
                  "status": "completed",
                  "region": "전국",
                  "rows": 1200,
                  "notices": 88,
                  "rate_window": { "rows": 345, "min": 99.5, "max": 100.0 },
                  "outputs": { "shadow": "{{shadowDir.Replace("\\", "\\\\")}}" },
                  "crowding_key_findings": {
                    "crowded_band_lift": 1.5,
                    "sparse_band_lift": 2.7
                  },
                  "collision_key_findings": {
                    "wide_left_space_lift": 3.2,
                    "high_left_advantage_lift": 4.1,
                    "best_matrix_cell": {
                      "우측앵커거리구간": "가까움",
                      "좌측여유구간": "넓음"
                    }
                  },
                  "shadow_summary": {
                    "period": "1y",
                    "fixed_bidders": 4,
                    "recommendations": 2
                  },
                  "conditional_advantage_summary": {
                    "forbidden_zone_count": 6,
                    "boundary_zone_count": 4,
                    "candidate_band_count": 2,
                    "top_candidate": {
                      "band": [99.739, 99.746],
                      "anchor": "화랑문화재연구원",
                      "left_space_width": 0.006,
                      "uncertainty": "medium"
                    }
                  }
                }
                """
            );

            File.WriteAllText(
                Path.Combine(shadowDir, "shadow_strategy.json"),
                """
                {
                  "recommendations": [
                    {
                      "target_company": "화랑문화재연구원",
                      "recommended_rate": 99.742,
                      "band_min": 99.739,
                      "band_max": 99.746,
                      "reason": "좌측여유 양호"
                    }
                  ]
                }
                """
            );

            var resolver = new StubPathResolver(root);
            var service = new ConsolePipelineInsightService(resolver);

            var insight = service.Load("전국");

            Assert.True(insight.IsAvailable);
            Assert.Equal("전국", insight.Region);
            Assert.Equal("1y", insight.AnalysisPeriod);
            Assert.Contains("입력 1,200행", insight.SummaryText);
            Assert.Contains("붐빔 리프트 1.5배", insight.CrowdingText);
            Assert.Contains("좌측여유 넓음 3.2배", insight.CollisionText);
            Assert.Contains("고정업체 4개", insight.ShadowText);
            Assert.Contains("금지 6", insight.ShadowText);
            Assert.Contains("화랑문화재연구원", insight.RecommendationText);
            Assert.Contains("99.739%~99.746%", insight.RecommendationText);
            Assert.Contains("좌측여유 0.006", insight.RecommendationText);
            Assert.Equal("좌측선점 후보 · 불확실성 중간", insight.RecommendationReasonText);
            Assert.True(insight.RecommendationBandMin.HasValue);
            Assert.True(insight.RecommendationBandMax.HasValue);
            Assert.Equal(6, insight.ForbiddenZoneCount);
            Assert.Equal(4, insight.BoundaryZoneCount);
            Assert.Equal(2, insight.CandidateBandCount);
            Assert.Equal("화랑문화재연구원", insight.PrimaryAnchorCompany);
            Assert.Equal("99.739%~99.746%", insight.RecommendationBandText);
            Assert.Equal("0.006", insight.RecommendationLeftSpaceText);
            Assert.Equal("중간", insight.RecommendationUncertainty);
            Assert.Equal(99.5, insight.AnalysisMinRate);
            Assert.Equal(100.0, insight.AnalysisMaxRate);
            Assert.InRange(insight.RecommendationBandMin!.Value, 99.7385, 99.7395);
            Assert.InRange(insight.RecommendationBandMax!.Value, 99.7455, 99.7465);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReturnsUnavailableWhenSnapshotSignatureIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"console-pipeline-stale-{Guid.NewGuid():N}");
        var shadowDir = Path.Combine(root, "shadow_전국");
        Directory.CreateDirectory(shadowDir);

        try
        {
            File.WriteAllText(
                Path.Combine(shadowDir, "pipeline_status.json"),
                """
                {
                  "status": "completed",
                  "region": "전국",
                  "rows": 1200,
                  "notices": 88,
                  "rate_window": { "rows": 345 }
                }
                """
            );

            var resolver = new StubPathResolver(root);
            var service = new ConsolePipelineInsightService(resolver);

            var insight = service.Load("전국");

            Assert.False(insight.IsAvailable);
            Assert.Equal("stale", insight.Status);
            Assert.Contains("오래되었습니다", insight.StatusText);
            Assert.Contains("다시 분석", insight.RecommendationReasonText);
            Assert.Equal("추천 밴드 없음", insight.RecommendationText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReturnsUnavailableWhenMasterFileIsNewerThanSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"console-pipeline-master-stale-{Guid.NewGuid():N}");
        var shadowDir = Path.Combine(root, "shadow_전국");
        Directory.CreateDirectory(shadowDir);
        var masterPath = Path.Combine(root, "master.xlsx");

        try
        {
            File.WriteAllText(masterPath, "test");
            File.SetLastWriteTimeUtc(masterPath, new DateTime(2026, 3, 9, 1, 0, 0, DateTimeKind.Utc));

            File.WriteAllText(
                Path.Combine(shadowDir, "pipeline_status.json"),
                $$"""
                {
                  "snapshot_signature": "{{CSharpAnalysisPipelineRunner.CurrentSnapshotSignature}}",
                  "generated_at_utc": "2026-03-09T00:00:00Z",
                  "source_file_last_write_time_utc": "2026-03-09T00:00:00Z",
                  "status": "completed",
                  "region": "전국",
                  "rows": 1200,
                  "notices": 88,
                  "rate_window": { "rows": 345 }
                }
                """
            );

            var resolver = new StubPathResolver(root, masterPath);
            var service = new ConsolePipelineInsightService(resolver);

            var insight = service.Load("전국");

            Assert.False(insight.IsAvailable);
            Assert.Equal("stale", insight.Status);
            Assert.Contains("원본 엑셀", insight.SummaryText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class StubPathResolver(string analysisRoot, string? masterPath = null, string? analysisProfilePath = null) : IAppPathResolver
    {
        public string ResolveMasterPath(string? providedPath) => providedPath ?? masterPath ?? string.Empty;
        public string ResolveOutputDirectory(string? providedPath) => providedPath ?? string.Empty;
        public string? ResolveEnvPath() => null;
        public string ResolveSkillScriptPath() => string.Empty;
        public string ResolveAnalysisProfilePath() => analysisProfilePath ?? string.Empty;
        public string ResolveApiRuleProfilePath() => string.Empty;
        public string ResolveConsoleAnalysisRootPath() => analysisRoot;
    }
}
