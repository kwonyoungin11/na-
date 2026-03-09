using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.ViewModels;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class MainWindowViewModelComponentsTests
{
    [Fact]
    public void FilterState_BuildFilter_NormalizesRangeAndText()
    {
        var state = new MainWindowFilterState();

        var filter = state.BuildFilter(
            startDate: new DateTime(2026, 3, 5),
            endDate: new DateTime(2026, 3, 1),
            selectedRegion: "전국",
            keywordText: "  발굴조사  ",
            orderingAgencyText: "  문화재청 ",
            selectedCompany: "  동국문화재연구원  ",
            selectedMinAdjustRate: "100.10000",
            selectedMaxAdjustRate: "99.90000");

        Assert.Equal(new DateTime(2026, 3, 5), filter.StartDate);
        Assert.Equal(new DateTime(2026, 3, 5), filter.EndDate);
        Assert.Equal(99.9, filter.MinAdjustRate);
        Assert.Equal(100.1, filter.MaxAdjustRate);
        Assert.Equal("발굴조사", filter.Keyword);
        Assert.Equal("문화재청", filter.OrderingAgency);
        Assert.Equal("동국문화재연구원", filter.Company);
        Assert.Equal("낙찰", filter.DataType);
    }

    [Fact]
    public void SnapshotCoordinator_ReturnsStoredSnapshot()
    {
        var coordinator = new MainWindowSnapshotCoordinator();
        var filter = new DashboardFilter(
            StartDate: new DateTime(2026, 1, 1),
            EndDate: new DateTime(2026, 1, 31),
            Region: "전국",
            Keyword: "발굴",
            OrderingAgency: "청",
            DataType: "낙찰",
            Company: "전체",
            MinAdjustRate: 99.5,
            MaxAdjustRate: 100.0);
        var snapshot = new DashboardSnapshot(
            Kpis: [new KpiCardModel("조회 공고 수", "1", "건")],
            NoticeRows: [new NoticeRowModel("N1", "공고", "기관", DateTime.Today, 100.0, true)],
            DataSource: "test");

        coordinator.StoreSnapshot(filter, snapshot);

        var found = coordinator.TryGetSnapshot(filter, out var cached);

        Assert.True(found);
        Assert.NotNull(cached);
        Assert.Equal("test", cached!.DataSource);
    }

    [Fact]
    public void AlertPublisher_InitializesBaseline_ThenPublishesOnlyNewAlerts()
    {
        var publisher = new MainWindowAlertPublisher();
        var events = new List<string>();

        var baselineRows = new[]
        {
            CreateParticipant("N-1", 1, new DateTime(2026, 3, 1), 1000),
            CreateParticipant("N-2", 2, new DateTime(2026, 3, 2), 2000),
        };

        var first = publisher.Publish(baselineRows, events.Add);
        var second = publisher.Publish(baselineRows, events.Add);

        var incoming = baselineRows
            .Concat([CreateParticipant("N-3", 1, new DateTime(2026, 3, 3), 3000)])
            .ToList();
        var third = publisher.Publish(incoming, events.Add);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Single(third);
        Assert.Equal("N-3", third[0].NoticeNumber);
        Assert.Contains(events, x => x.Contains("모니터링 기준선 설정", StringComparison.Ordinal));
        Assert.Contains(events, x => x.Contains("동국 순위 알림", StringComparison.Ordinal));
    }

    private static ParticipantRecord CreateParticipant(string noticeNumber, int rank, DateTime openDate, long bidAmount)
    {
        return new ParticipantRecord(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: $"공고-{noticeNumber}",
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: "(재)동국문화재연구원",
            NormalizedCompanyName: "동국문화재연구원",
            OpenDate: openDate,
            BidDate: openDate,
            BidAmount: bidAmount,
            BidRateVsEstimate: 99.0,
            BidRateVsBase: 99.0,
            AdjustRate: 99.999,
            EstimatePrice: 100000,
            LowerLimitPrice: 90000,
            BaseAmount: 100000,
            PriceAdjustmentRate: 1.0,
            LowerRate: 87.0,
            LowerDiff: 100,
            BizNo: string.Empty,
            CeoName: string.Empty,
            Remark: string.Empty,
            IsAwarded: rank == 1);
    }
}
