using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class ParticipantSameDayClusterReducerTests
{
    [Fact]
    public void Collapse_MergesSameCompanySameDayNearbyRates()
    {
        var rows = new[]
        {
            Build("N1", "가람문화재연구원", "가람문화재연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.6310),
            Build("N2", "가람문화재연구원", "가람문화재연구원", new DateTime(2026, 3, 1, 11, 0, 0), 99.6318),
            Build("N3", "가람문화재연구원", "가람문화재연구원", new DateTime(2026, 3, 1, 12, 0, 0), 99.6322),
            Build("N4", "가람문화재연구원", "가람문화재연구원", new DateTime(2026, 3, 1, 13, 0, 0), 99.6325),
            Build("N5", "가람문화재연구원", "가람문화재연구원", new DateTime(2026, 3, 1, 14, 0, 0), 99.6500),
            Build("N6", "다른연구원", "다른연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.7000),
        };

        var collapsed = ParticipantSameDayClusterReducer.Collapse(rows, 0.003, 2);

        Assert.Equal(3, collapsed.Count);
        Assert.Equal("N1", collapsed[0].NoticeNumber);
        Assert.Equal(99.6320, collapsed[0].AdjustRate!.Value, 4);
        Assert.Equal("N5", collapsed[1].NoticeNumber);
        Assert.Equal("N6", collapsed[2].NoticeNumber);
    }

    [Fact]
    public void Collapse_KeepsFarApartSameDayRatesAsSeparateClusters()
    {
        var rows = new[]
        {
            Build("A1", "다온문화재연구원", "다온문화재연구원", new DateTime(2026, 3, 1, 10, 0, 0), 99.5510),
            Build("A2", "다온문화재연구원", "다온문화재연구원", new DateTime(2026, 3, 1, 11, 0, 0), 99.5520),
            Build("A3", "다온문화재연구원", "다온문화재연구원", new DateTime(2026, 3, 1, 12, 0, 0), 99.8320),
            Build("A4", "다온문화재연구원", "다온문화재연구원", new DateTime(2026, 3, 1, 13, 0, 0), 99.8330),
        };

        var collapsed = ParticipantSameDayClusterReducer.Collapse(rows, 0.003, 2);

        Assert.Equal(2, collapsed.Count);
        Assert.Equal(99.5515, collapsed[0].AdjustRate!.Value, 4);
        Assert.Equal(99.8325, collapsed[1].AdjustRate!.Value, 4);
    }

    private static ParticipantRecord Build(string noticeNumber, string companyName, string normalizedCompanyName, DateTime openDate, double adjustRate)
    {
        return new ParticipantRecord(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: noticeNumber,
            OrderingAgency: "기관",
            Region: "전국",
            Rank: 1,
            CompanyName: companyName,
            NormalizedCompanyName: normalizedCompanyName,
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
