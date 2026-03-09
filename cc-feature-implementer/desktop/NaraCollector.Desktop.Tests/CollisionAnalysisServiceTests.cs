using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CollisionAnalysisServiceTests
{
    [Fact]
    public void Analyze_TracksAnchorAndLeftSpace()
    {
        var service = new CollisionAnalysisService();
        var rows = new[]
        {
            CreateParticipant("A", -1, "탈락", 99.500, 99.600),
            CreateParticipant("A", 1, "낙찰", 99.510, 99.600),
            CreateParticipant("A", 2, "앵커", 99.515, 99.600),
            CreateParticipant("A", 3, "후행", 99.530, 99.600),
        };

        var result = service.Analyze(rows, new CollisionAnalysisOptions(1, 99.5, 100.0));
        var winner = Assert.Single(result.CollisionFrame.Where(x => x.CompanyName == "낙찰"));

        Assert.Equal("앵커", winner.RightAnchorCompany);
        Assert.Equal(0.005, winner.RightAnchorGap!.Value, 6);
        Assert.Equal(0.010, winner.LeftSpaceGapAll!.Value, 6);
        Assert.Equal(0.005, winner.LeftSpaceAdvantage!.Value, 6);
    }

    [Fact]
    public void Analyze_LeftSpaceAdvantageShowsHigherLiftForHighTier()
    {
        var service = new CollisionAnalysisService();
        var rows = new[]
        {
            CreateParticipant("A", -1, "탈락A", 99.500, 99.600),
            CreateParticipant("A", 1, "가", 99.530, 99.600),
            CreateParticipant("A", 2, "나", 99.540, 99.600),
            CreateParticipant("A", 3, "다", 99.560, 99.600),
            CreateParticipant("B", -1, "탈락B", 99.600, 99.700),
            CreateParticipant("B", 1, "라", 99.640, 99.700),
            CreateParticipant("B", 2, "마", 99.650, 99.700),
            CreateParticipant("B", 3, "바", 99.670, 99.700),
            CreateParticipant("C", 1, "사", 99.710, 99.800),
            CreateParticipant("C", 2, "아", 99.740, 99.800),
            CreateParticipant("C", 3, "자", 99.741, 99.800),
        };

        var result = service.Analyze(rows, new CollisionAnalysisOptions(1, 99.5, 100.0));
        var high = result.AdvantageSummary.FirstOrDefault(x => x.Tier == "높음");
        var low = result.AdvantageSummary.FirstOrDefault(x => x.Tier == "낮음");

        Assert.NotNull(high);
        Assert.NotNull(low);
        Assert.True(high!.WinLift > low!.WinLift);
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
