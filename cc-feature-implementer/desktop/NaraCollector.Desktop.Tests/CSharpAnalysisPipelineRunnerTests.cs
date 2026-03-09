using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CSharpAnalysisPipelineRunnerTests
{
    [Fact]
    public void PrepareAnalysisRows_CollapsesSameCompanySameDayClustersBeforeAnalysis()
    {
        var rows = new[]
        {
            Build("N1", "가람문화재연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.6310),
            Build("N2", "가람문화재연구원", new DateTime(2026, 3, 1, 11, 0, 0), 99.6320),
            Build("N3", "가람문화재연구원", new DateTime(2026, 3, 1, 12, 0, 0), 99.6500),
            Build("N4", "다른연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.7000),
        };

        var prepared = CSharpAnalysisPipelineRunner.PrepareAnalysisRows(rows, 99.62, 99.71);

        Assert.Equal(3, prepared.Count);
        Assert.Equal("N1", prepared[0].NoticeNumber);
        Assert.Equal(99.6315, prepared[0].AdjustRate!.Value, 4);
        Assert.Equal("N3", prepared[1].NoticeNumber);
        Assert.Equal("N4", prepared[2].NoticeNumber);
    }

    [Fact]
    public void PrepareAnalysisRows_FiltersToRateWindowBeforeCollapsing()
    {
        var rows = new[]
        {
            Build("N0", "누리문화재연구원", new DateTime(2026, 3, 1, 9, 0, 0), 99.4990),
            Build("N1", "누리문화재연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.5005),
            Build("N2", "누리문화재연구원", new DateTime(2026, 3, 1, 11, 0, 0), 99.5015),
        };

        var prepared = CSharpAnalysisPipelineRunner.PrepareAnalysisRows(rows, 99.5, 99.6);

        var collapsed = Assert.Single(prepared);
        Assert.Equal("N1", collapsed.NoticeNumber);
        Assert.Equal(99.5010, collapsed.AdjustRate!.Value, 4);
    }

    [Theory]
    [InlineData("대구/경북", "대구", true)]
    [InlineData("대구/경북", "경북", true)]
    [InlineData("경북", "대구", false)]
    [InlineData("전국", "전국", true)]
    public void RegionMatches_IncludesCompositeRegionalTokens(string rowRegion, string selectedRegion, bool expected)
    {
        var result = CSharpAnalysisPipelineRunner.RegionMatches(rowRegion, selectedRegion);

        Assert.Equal(expected, result);
    }

    private static ParticipantRecord Build(string noticeNumber, string companyName, DateTime openDate, double adjustRate)
    {
        return new ParticipantRecord(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: noticeNumber,
            OrderingAgency: "기관",
            Region: "전국",
            Rank: 1,
            CompanyName: companyName,
            NormalizedCompanyName: companyName,
            OpenDate: openDate,
            BidDate: openDate,
            BidAmount: null,
            BidRateVsEstimate: null,
            BidRateVsBase: null,
            AdjustRate: adjustRate,
            EstimatePrice: null,
            LowerLimitPrice: null,
            BaseAmount: null,
            PriceAdjustmentRate: null,
            LowerRate: null,
            LowerDiff: null,
            BizNo: string.Empty,
            CeoName: string.Empty,
            Remark: string.Empty,
            IsAwarded: false);
    }
}
