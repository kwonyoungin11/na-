using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class DonggukAdjustRateTrendBuilderTests
{
    [Fact]
    public void BuildAwardNoticeRows_UsesRequestedFilterWindow_NotFixedThreeMonths()
    {
        var participants = new List<ParticipantRecord>
        {
            CreateParticipant("N-1", 3, new DateTime(2025, 1, 15), 1000) with { AdjustRate = 99.222, CompanyName = "동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", NoticeName = "공고-1", OrderingAgency = "기관A", Region = "전국" },
            CreateParticipant("N-1", 1, new DateTime(2025, 1, 15), 1000) with { AdjustRate = 99.456, CompanyName = "다른기관", NormalizedCompanyName = "다른기관", IsAwarded = true, NoticeName = "공고-1", OrderingAgency = "기관A", Region = "전국" },
            CreateParticipant("N-2", 4, new DateTime(2025, 6, 15), 1000) with { AdjustRate = 99.333, CompanyName = "동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", NoticeName = "공고-2", OrderingAgency = "기관B", Region = "경북" },
            CreateParticipant("N-2", 1, new DateTime(2025, 6, 15), 1000) with { AdjustRate = 99.777, CompanyName = "다른기관", NormalizedCompanyName = "다른기관", IsAwarded = true, NoticeName = "공고-2", OrderingAgency = "기관B", Region = "경북" },
        };

        var rows = DonggukAdjustRateTrendBuilder.BuildAwardNoticeRows(
            participants,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 12, 31));

        Assert.Equal(2, rows.Count);
        Assert.Equal(["N-2", "N-1"], rows.Select(x => x.NoticeNumber).ToArray());
    }

    private static ParticipantRecord CreateParticipant(string noticeNumber, int rank, DateTime openDate, long bidAmount)
    {
        return new ParticipantRecord(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: string.Empty,
            OrderingAgency: string.Empty,
            Region: string.Empty,
            Rank: rank,
            CompanyName: string.Empty,
            NormalizedCompanyName: string.Empty,
            OpenDate: openDate,
            BidDate: openDate.AddHours(-1),
            BidAmount: bidAmount,
            BidRateVsEstimate: null,
            BidRateVsBase: null,
            AdjustRate: null,
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
