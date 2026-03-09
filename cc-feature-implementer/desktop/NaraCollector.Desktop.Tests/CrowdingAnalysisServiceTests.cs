using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CrowdingAnalysisServiceTests
{
    [Fact]
    public void Analyze_UsesNoticeAwareExpectedWins()
    {
        var service = new CrowdingAnalysisService();
        var rows = new[]
        {
            CreateParticipant("A", 1, "가", 99.5001, 99.6001),
            CreateParticipant("A", 2, "나", 99.5011, 99.6001),
            CreateParticipant("B", 2, "다", 99.5002, 99.6002),
            CreateParticipant("B", 1, "라", 99.5007, 99.6002),
            CreateParticipant("B", 3, "마", 99.5012, 99.6002),
            CreateParticipant("B", 4, "바", 99.5021, 99.6002),
        };

        var result = service.Analyze(rows, new CrowdingAnalysisOptions(0.001, 99.5, 100.0));
        var band = Assert.Single(result.BandTable.Where(x => x.BandLabel == "99.500~99.501"));

        Assert.Equal(3, band.BidCount);
        Assert.Equal(2, band.WinnerCount);
        Assert.Equal(1.0, band.ExpectedWins, 6);
        Assert.Equal(2.0 / 3.0, band.ActualWinRate, 6);
        Assert.Equal(1.0 / 3.0, band.ExpectedWinRate, 6);
        Assert.Equal(2.0, band.WinLift, 6);
    }

    [Fact]
    public void Analyze_ExcludesNoticesWithoutSingleWinner()
    {
        var service = new CrowdingAnalysisService();
        var rows = new[]
        {
            CreateParticipant("A", 1, "가", 99.5001, 99.6001),
            CreateParticipant("A", 2, "나", 99.5011, 99.6001),
            CreateParticipant("C", 2, "다", 99.5021, 99.6003),
            CreateParticipant("C", 3, "라", 99.5031, 99.6003),
        };

        var result = service.Analyze(rows, new CrowdingAnalysisOptions(0.001, 99.5, 100.0));

        Assert.Equal(2, result.Metadata.AllNoticeCount);
        Assert.Equal(1, result.Metadata.CompleteNoticeCount);
        Assert.Equal(1, result.Metadata.ExcludedNoticeCount);
        Assert.All(result.BandTable, row => Assert.Equal(1, row.NoticeCount));
    }

    private static ParticipantRecord CreateParticipant(string noticeNumber, int rank, string company, double adjustRate, double noticeAdjustRate)
        => new(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: noticeNumber,
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: company,
            NormalizedCompanyName: company,
            OpenDate: new DateTime(2026, 1, 1),
            BidDate: null,
            BidAmount: null,
            BidRateVsEstimate: null,
            BidRateVsBase: null,
            AdjustRate: adjustRate,
            EstimatePrice: null,
            LowerLimitPrice: null,
            BaseAmount: null,
            PriceAdjustmentRate: noticeAdjustRate,
            LowerRate: null,
            LowerDiff: null,
            BizNo: string.Empty,
            CeoName: string.Empty,
            Remark: string.Empty,
            IsAwarded: rank == 1);
}
