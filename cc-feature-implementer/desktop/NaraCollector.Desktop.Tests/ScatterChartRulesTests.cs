using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class ScatterChartRulesTests
{
    [Fact]
    public void BuildNotice_ChipCountEqualsUniqueCompaniesInRateWindow_NotStoredRank()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("R26BK01673928-000", 1, "춘추문화재연구원", 99.78004),
            Row("R26BK01673928-000", 2, "전북문화유산연구원", 99.78127),
            Row("R26BK01673928-000", 3, "경강문화유산연구원", 99.78376),
            Row("R26BK01673928-000", 4, "한울문화재연구원", 99.79000),
            Row("R26BK01673928-000", 5, "가람문화재연구원", 99.80000),
            Row("R26BK01673928-000", 62, "동국문화재연구원", 99.60535),
            Row("R26BK01673928-000", 70, "범위밖연구원", 98.10000),
        };

        var scatter = ScatterChartRules.BuildNotice(rows, "R26BK01673928-000", 99.0, 101.0);

        Assert.Equal(6, scatter.ChipCount);
        Assert.Equal(6, scatter.DonggukRank);
        Assert.Equal(7, scatter.TotalCompanies);
        var label = ScatterChartRules.FormatDonggukRankLabel(scatter, 99.60535, 99.78004);
        Assert.Contains("6위", label, StringComparison.Ordinal);
        Assert.Contains("6개사", label, StringComparison.Ordinal);
        Assert.DoesNotContain("62위", label, StringComparison.Ordinal);
    }

    [Fact]
    public void ToChartAdjustRate_MapsOpeng96ScaleUsingRank1IgunsulAnchor()
    {
        var mappedRank1 = ScatterChartRules.ToChartAdjustRate(96.78818, 96.78818, 99.78004);
        var mappedDongguk = ScatterChartRules.ToChartAdjustRate(96.40535, 96.78818, 99.78004);

        Assert.Equal(99.78004, mappedRank1, 5);
        Assert.InRange(mappedDongguk, 99.0, 101.0);
        Assert.Equal(99.39721, mappedDongguk, 5);
    }

    [Fact]
    public void FromBidRateVsLowerLimit_Converts86ScaleTo99ChartScale()
    {
        var chartRate = ScatterChartRules.FromBidRateVsLowerLimit(86.447, 86.745);
        Assert.InRange(chartRate!.Value, 99.0, 101.0);
        Assert.Equal(99.65647, chartRate.Value, 4);
    }

    [Fact]
    public void InPeriod_KeepsNoticesInsideSelectedWindowOnly()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("FEB", 1, "동국문화재연구원", 99.5) with { OpenDate = new DateTime(2026, 2, 20) },
            Row("JUN", 1, "동국문화재연구원", 99.6) with { OpenDate = new DateTime(2026, 6, 9) },
            Row("AUG", 1, "동국문화재연구원", 99.7) with { OpenDate = new DateTime(2026, 8, 20) },
        };

        var sixMonths = ScatterChartRules.InPeriod(rows, new DateTime(2026, 2, 25), new DateTime(2026, 8, 25));
        var oneMonth = ScatterChartRules.InPeriod(rows, new DateTime(2026, 7, 25), new DateTime(2026, 8, 25));

        Assert.Equal(["JUN", "AUG"], sixMonths.Select(x => x.NoticeNumber).ToArray());
        Assert.Equal(["AUG"], oneMonth.Select(x => x.NoticeNumber).ToArray());
        Assert.DoesNotContain(sixMonths, x => x.NoticeNumber == "FEB");
    }

    [Fact]
    public void SortOverlappingChips_Same01Cell_DrawsDonggukInFront()
    {
        var bucket = new List<ParticipantRecord>
        {
            Row("N", 1, "춘추문화재연구원", 99.60000),
            Row("N", 8, "한울문화재연구원", 99.60000),
            Row("N", 54, "동국문화재연구원", 99.60000),
            Row("N", 3, "가람문화재연구원", 99.61000),
        };

        var sorted = ScatterChartRules.SortOverlappingChips(bucket);

        Assert.Equal("동국문화재연구원", sorted[^1].CompanyName);
        Assert.NotEqual("동국문화재연구원", sorted[0].CompanyName);
    }

    private static ParticipantRecord Row(string notice, int rank, string company, double rate)
    {
        return new ParticipantRecord(
            NoticeNo: notice,
            NoticeNumber: notice,
            NoticeName: "공고",
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: company,
            NormalizedCompanyName: company,
            OpenDate: new DateTime(2026, 8, 20, 11, 0, 0),
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
}
