using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CrowdingHeatMapBuilderTests
{
    [Fact]
    public void Build_FillsEveryColumnOnSameAxisAsScatter()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 1, "한울문화재연구원", 99.50000, new DateTime(2026, 8, 1)),
            Row("A", 2, "동국문화재연구원", 99.50000, new DateTime(2026, 8, 1)),
            Row("B", 1, "한울문화재연구원", 99.50000, new DateTime(2026, 8, 10)),
        };

        var cells = CrowdingHeatMapBuilder.Build(rows, new DateTime(2026, 2, 25), new DateTime(2026, 8, 25), 99.0, 101.0, 0.01);

        Assert.Equal(201, cells.Count);
        Assert.Equal(99.0, cells[0].BandStart, 5);
        Assert.Equal(101.0, cells[^1].BandStart, 5);
        var hot = cells.Single(x => Math.Abs(x.BandStart - 99.50) < 0.00001);
        Assert.Equal(3, hot.BidCount);
        Assert.Equal(2, hot.NoticeCount);
        Assert.Equal(2, hot.UniqueCompanyCount);
        Assert.Equal("과열", hot.DensityTier);
        Assert.Equal(1.0, hot.Intensity);
        Assert.Contains(cells, x => x.BidCount == 0 && x.DensityTier == "공백");
        Assert.DoesNotContain(cells, x => x.DensityTier == "1위");
    }

    [Fact]
    public void Build_DropsRowsOutsideSelectedPeriod()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("OLD", 1, "한울문화재연구원", 99.60, new DateTime(2026, 1, 10)),
            Row("NEW", 1, "한울문화재연구원", 99.80, new DateTime(2026, 8, 20)),
        };

        var cells = CrowdingHeatMapBuilder.Build(rows, new DateTime(2026, 7, 25), new DateTime(2026, 8, 25), 99.0, 101.0, 0.01);

        Assert.Equal(0, cells.Single(x => Math.Abs(x.BandStart - 99.60) < 0.00001).BidCount);
        Assert.Equal(1, cells.Single(x => Math.Abs(x.BandStart - 99.80) < 0.00001).BidCount);
    }

    [Fact]
    public void Build_SingleOccupancyColumnsAreMidNotHot()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 1, "한울문화재연구원", 99.50, new DateTime(2026, 8, 1)),
            Row("B", 1, "한울문화재연구원", 99.80, new DateTime(2026, 8, 10)),
        };

        var cells = CrowdingHeatMapBuilder.Build(rows, new DateTime(2026, 2, 25), new DateTime(2026, 8, 25), 99.0, 101.0, 0.01);
        var a = cells.Single(x => Math.Abs(x.BandStart - 99.50) < 0.00001);
        var b = cells.Single(x => Math.Abs(x.BandStart - 99.80) < 0.00001);

        Assert.Equal(1, a.BidCount);
        Assert.Equal("중간", a.DensityTier);
        Assert.Equal("중간", b.DensityTier);
        Assert.DoesNotContain(cells, x => x.DensityTier == "과열");
    }

    [Fact]
    public void Build_IntensityFollowsBidCountNotStoredRank()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 62, "동국문화재연구원", 99.70, new DateTime(2026, 8, 1)),
            Row("A", 1, "한울문화재연구원", 99.70, new DateTime(2026, 8, 1)),
            Row("B", 40, "한울문화재연구원", 99.40, new DateTime(2026, 8, 2)),
        };

        var cells = CrowdingHeatMapBuilder.Build(rows, new DateTime(2026, 2, 25), new DateTime(2026, 8, 25), 99.0, 101.0, 0.01);
        var crowded = cells.Single(x => Math.Abs(x.BandStart - 99.70) < 0.00001);
        var sparse = cells.Single(x => Math.Abs(x.BandStart - 99.40) < 0.00001);

        Assert.Equal(2, crowded.BidCount);
        Assert.Equal(1, sparse.BidCount);
        Assert.True(crowded.Intensity > sparse.Intensity);
        Assert.Equal("과열", crowded.DensityTier);
        Assert.Equal("중간", sparse.DensityTier);
    }

    private static ParticipantRecord Row(string notice, int rank, string company, double rate, DateTime openDate)
        => new(
            NoticeNo: notice,
            NoticeNumber: notice,
            NoticeName: "공고",
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: company,
            NormalizedCompanyName: company,
            OpenDate: openDate,
            BidDate: null,
            BidAmount: 1,
            BidRateVsEstimate: null,
            BidRateVsBase: null,
            AdjustRate: rate,
            EstimatePrice: null,
            LowerLimitPrice: null,
            BaseAmount: null,
            PriceAdjustmentRate: null,
            LowerRate: null,
            LowerDiff: null,
            BizNo: string.Empty,
            CeoName: string.Empty,
            Remark: string.Empty,
            IsAwarded: rank == 1);
}
