using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class SqliteDashboardRepositoryCollectorNoticeTests
{
    [Fact]
    public void FilterBidCollectorNoticeRows_ExcludesNoticesAlreadyAwarded()
    {
        var filter = new DashboardFilter(
            StartDate: new DateTime(2026, 3, 1),
            EndDate: new DateTime(2026, 3, 31),
            Region: "전국",
            Keyword: string.Empty,
            OrderingAgency: string.Empty,
            DataType: "낙찰",
            Company: string.Empty,
            MinAdjustRate: null,
            MaxAdjustRate: null);

        var bidRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 3, 9, 10, 0, 0), "NB-1", "입찰-1", "기관", "경북", "5개사"),
            new(new DateTime(2026, 3, 9, 11, 0, 0), "NB-2", "입찰-2", "기관", "경북", "7개사"),
        };
        var awardRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 3, 9, 12, 0, 0), "NB-2", "낙찰-2", "기관", "경북", "1건"),
        };

        var filtered = CollectorNoticeRules.ExcludeAwardedBidRows(bidRows, awardRows);

        var noticeNumbers = filtered.Select(x => x.NoticeNumber).ToArray();
        Assert.Equal(["NB-1"], noticeNumbers);
    }

    [Fact]
    public void DropNoticesWithTooFewCompanies_RemovesFiveOrFewerKeepsSix()
    {
        var rows = new List<ParticipantRecord>();
        for (var i = 1; i <= 5; i++)
        {
            rows.Add(MakeRow("SMALL", i, $"업체{i}"));
        }

        for (var i = 1; i <= 6; i++)
        {
            rows.Add(MakeRow("BIG", i, $"업체{i}"));
        }

        var kept = CollectorNoticeRules.DropNoticesWithTooFewCompanies(rows);

        Assert.All(kept, x => Assert.Equal("BIG", x.NoticeNumber));
        Assert.Equal(6, kept.Count);
        Assert.DoesNotContain(kept, x => x.NoticeNumber == "SMALL");
    }

    private static ParticipantRecord MakeRow(string notice, int rank, string company)
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
            OpenDate: new DateTime(2026, 8, 1),
            BidDate: null,
            BidAmount: 1,
            BidRateVsEstimate: 99,
            BidRateVsBase: 99,
            AdjustRate: 99.5,
            EstimatePrice: 1,
            LowerLimitPrice: 1,
            BaseAmount: 1,
            PriceAdjustmentRate: 99.7,
            LowerRate: 86,
            LowerDiff: null,
            BizNo: string.Empty,
            CeoName: string.Empty,
            Remark: string.Empty,
            IsAwarded: rank == 1);
    }

    [Fact]
    public void ToCollectorViewFilter_MapsNationwideUiRegionToAllRegions()
    {
        var filter = CreateFilter("전국");

        var collectorFilter = CollectorNoticeRules.ToCollectorViewFilter(filter);

        Assert.Equal("전체", collectorFilter.Region);
        Assert.Equal(filter.StartDate, collectorFilter.StartDate);
        Assert.Equal(filter.EndDate, collectorFilter.EndDate);
    }

    [Fact]
    public void ToCollectorViewFilter_KeepsGyeongbukRegion()
    {
        var filter = CreateFilter("경북");

        var collectorFilter = CollectorNoticeRules.ToCollectorViewFilter(filter);

        Assert.Equal("경북", collectorFilter.Region);
    }

    [Fact]
    public void ResolveDonggukAwardNoticeRows_PrefersDonggukRowsOverCollectorFeed()
    {
        var donggukRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 8, 20, 11, 0, 0), "DG-1", "동국공고", "기관", "전국", "동국 99.61122 / 5위 · 낙찰 99.48199"),
        };
        var collectorRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 8, 24, 15, 0, 0), "AW-1", "수집공고", "기관", "경북", "8개사"),
        };

        var resolved = CollectorNoticeRules.ResolveDonggukAwardNoticeRows(donggukRows, collectorRows);

        var noticeNumbers = resolved.Select(x => x.NoticeNumber).ToArray();
        Assert.Equal(["DG-1"], noticeNumbers);
        Assert.Contains("동국 99.61122", resolved[0].ParticipantCountText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDonggukAwardNoticeRows_DoesNotFillWithCollectorWhenDonggukEmpty()
    {
        var collectorRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 8, 24, 15, 0, 0), "AW-1", "수집공고", "기관", "경북", "8개사"),
        };

        var resolved = CollectorNoticeRules.ResolveDonggukAwardNoticeRows([], collectorRows);

        Assert.Empty(resolved);
    }

    private static DashboardFilter CreateFilter(string region)
    {
        return new DashboardFilter(
            StartDate: new DateTime(2026, 5, 25),
            EndDate: new DateTime(2026, 8, 25),
            Region: region,
            Keyword: string.Empty,
            OrderingAgency: string.Empty,
            DataType: "낙찰",
            Company: string.Empty,
            MinAdjustRate: 99.0,
            MaxAdjustRate: 101.0);
    }
}
