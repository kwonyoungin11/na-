using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
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

    [Fact]
    public void DonggukAdjustRateTrendPoints_UseLatestSixMonthNoticeWindow()
    {
        var participants = new List<ParticipantRecord>
        {
            CreateParticipant("N-0", 1, new DateTime(2024, 12, 15), 1000) with { AdjustRate = 99.001, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원" },
            CreateParticipant("N-1", 1, new DateTime(2025, 4, 15), 1000) with { AdjustRate = 99.111, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원" },
            CreateParticipant("N-2", 1, new DateTime(2025, 5, 15), 1000) with { AdjustRate = 99.222, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원" },
            CreateParticipant("N-3", 1, new DateTime(2025, 7, 15), 1000) with { AdjustRate = 99.333, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원" },
            CreateParticipant("N-4", 1, new DateTime(2025, 7, 20), 1000) with { AdjustRate = 99.444, CompanyName = "다른기관", NormalizedCompanyName = "다른기관" },
        };

        var endDate = new DateTime(2025, 7, 31);
        var trendPoints = DonggukAdjustRateTrendBuilder.BuildPoints(participants, endDate);
        var summary = DonggukAdjustRateTrendBuilder.BuildSummary(participants, endDate);

        Assert.Equal(3, trendPoints.Count);
        Assert.Equal("04-15", trendPoints[0].Label);
        Assert.Equal("07-15", trendPoints[^1].Label);
        Assert.Equal(99.111, trendPoints[0].Value, 3);
        Assert.Equal(99.333, trendPoints[^1].Value, 3);
        Assert.Equal("N-1", trendPoints[0].Key);
        Assert.Equal("N-3", trendPoints[^1].Key);
        Assert.DoesNotContain(trendPoints, x => x.Key == "N-0");
        Assert.Contains("2025-04-15~2025-07-15", summary, StringComparison.Ordinal);
        Assert.Contains("공고 3건", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DonggukAdjustRateTrendPoints_BuildAwardReferenceForSameNotice()
    {
        var participants = new List<ParticipantRecord>
        {
            CreateParticipant("N-1", 3, new DateTime(2025, 5, 15), 1000) with { AdjustRate = 99.222, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", IsAwarded = false },
            CreateParticipant("N-1", 1, new DateTime(2025, 5, 15), 1000) with { AdjustRate = 99.456, CompanyName = "다른기관", NormalizedCompanyName = "다른기관", IsAwarded = true },
            CreateParticipant("N-2", 4, new DateTime(2025, 6, 15), 1000) with { AdjustRate = 99.333, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", IsAwarded = false },
            CreateParticipant("N-2", 1, new DateTime(2025, 6, 15), 1000) with { AdjustRate = 99.888, CompanyName = "또다른기관", NormalizedCompanyName = "또다른기관", IsAwarded = true },
        };

        var endDate = new DateTime(2025, 6, 30);
        var awardTrendPoints = DonggukAdjustRateTrendBuilder.BuildAwardReferencePoints(participants, endDate);

        Assert.Equal(2, awardTrendPoints.Count);
        Assert.Equal("05-15", awardTrendPoints[0].Label);
        Assert.Equal("06-15", awardTrendPoints[1].Label);
        Assert.Equal(99.456, awardTrendPoints[0].Value, 3);
        Assert.Equal(99.888, awardTrendPoints[1].Value, 3);
        Assert.Equal("N-1", awardTrendPoints[0].Key);
        Assert.Equal("N-2", awardTrendPoints[1].Key);
    }

    [Fact]
    public void DonggukAdjustRateTrendPoints_BuildAwardNoticeRows_FromSameNoticeSet()
    {
        var participants = new List<ParticipantRecord>
        {
            CreateParticipant("N-1", 3, new DateTime(2025, 5, 15), 1000) with { AdjustRate = 99.222, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", IsAwarded = false, NoticeName = "공고-1", OrderingAgency = "기관A", Region = "경북" },
            CreateParticipant("N-1", 1, new DateTime(2025, 5, 15), 1000) with { AdjustRate = 99.456, CompanyName = "다른기관", NormalizedCompanyName = "다른기관", IsAwarded = true, NoticeName = "공고-1", OrderingAgency = "기관A", Region = "경북" },
            CreateParticipant("N-2", 4, new DateTime(2025, 6, 15), 1000) with { AdjustRate = 99.333, CompanyName = "(재)동국문화재연구원", NormalizedCompanyName = "동국문화재연구원", IsAwarded = false, NoticeName = "공고-2", OrderingAgency = "기관B", Region = "전국" },
        };

        var endDate = new DateTime(2025, 6, 30);
        var rows = DonggukAdjustRateTrendBuilder.BuildAwardNoticeRows(participants, endDate);

        Assert.Single(rows);
        Assert.Equal("N-1", rows[0].NoticeNumber);
        Assert.Equal("공고-1", rows[0].NoticeName);
        Assert.Equal("기관A", rows[0].OrderingAgency);
        Assert.Equal("경북", rows[0].RegionText);
        Assert.Contains("동국 99.22200", rows[0].ParticipantCountText, StringComparison.Ordinal);
        Assert.Contains("2위", rows[0].ParticipantCountText, StringComparison.Ordinal);
        Assert.Contains("2개사", rows[0].ParticipantCountText, StringComparison.Ordinal);
        Assert.Contains("낙찰 99.45600", rows[0].ParticipantCountText, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectWindowStart_DefaultsToOneYear_AndFollowsLookback()
    {
        var now = new DateTime(2026, 8, 25, 10, 0, 0);
        Assert.Equal(12, CollectorNoticeRules.CollectLookbackMonths);
        Assert.Equal(new DateTime(2025, 8, 25, 10, 0, 0), CollectorNoticeRules.CollectWindowStart(now));
        Assert.Equal(new DateTime(2025, 8, 25), CollectorNoticeRules.AwardRetentionStart(now));
        Assert.Equal(6, CollectorNoticeRules.CollectLookbackMonthsFor(now.AddMonths(-6), now));
        Assert.Equal(12, CollectorNoticeRules.CollectLookbackMonthsFor(now.AddYears(-1), now));
        Assert.Equal(12, CollectorNoticeRules.CollectLookbackMonthsFor(new DateTime(2000, 1, 1), now));
        Assert.Equal(new DateTime(2026, 2, 25, 10, 0, 0), CollectorNoticeRules.CollectWindowStart(now, 6));
    }

    [Fact]
    public void ResolveVisibleAwardNoticeRows_UsesCollectWindow()
    {
        var rows = Enumerable.Range(0, 15)
            .Select(index => new CollectorNoticeRowModel(
                NoticeDate: new DateTime(2026, 3, 1).AddDays(index),
                NoticeNumber: $"AW-{index:00}",
                NoticeName: $"낙찰-{index:00}",
                OrderingAgency: "기관",
                RegionText: "전국",
                ParticipantCountText: $"{index + 1}개사"))
            .Concat(
            [
                new CollectorNoticeRowModel(new DateTime(2026, 1, 20), "AW-SIX-MONTHS", "낙찰-6개월안", "기관", "전국", "2개사"),
                new CollectorNoticeRowModel(new DateTime(2024, 8, 1), "AW-OLD", "낙찰-과거", "기관", "전국", "2개사"),
                new CollectorNoticeRowModel(new DateTime(2026, 4, 2), "AW-FUTURE", "낙찰-미래", "기관", "전국", "2개사"),
            ])
            .ToList();

        var visible = CollectorNoticeRules.CreateVisibleAwardRows(
            rows,
            [],
            new DateTime(2026, 3, 31, 12, 0, 0));

        Assert.Equal(16, visible.Count);
        Assert.Contains(visible, x => x.NoticeNumber == "AW-SIX-MONTHS");
        Assert.DoesNotContain(visible, x => x.NoticeNumber == "AW-OLD");
        Assert.DoesNotContain(visible, x => x.NoticeNumber == "AW-FUTURE");
    }

    [Fact]
    public void ResolveVisibleBidNoticeRows_UsesAllNonAwardedSnapshotRows()
    {
        var snapshotRows = new List<NoticeRowModel>
        {
            new("B-1", "입찰-1", "기관", new DateTime(2026, 3, 4), 99.91, false),
            new("B-2", "입찰-2", "기관", new DateTime(2026, 3, 5), 99.92, false),
            new("A-1", "낙찰-1", "기관", new DateTime(2026, 3, 6), 99.93, true),
        };

        var visible = CollectorNoticeRules.CreateVisibleBidRows(
            [],
            [],
            snapshotRows,
            new DateTime(2026, 3, 3, 12, 0, 0));

        Assert.Equal(2, visible.Count);
        Assert.Equal(["B-1", "B-2"], visible.Select(x => x.NoticeNumber).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void ResolveVisibleBidNoticeRows_ExcludesIncrementalRowsAlreadyAwarded()
    {
        var bidRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 3, 9, 7, 0, 0), "PAST-1", "입찰-마감", "기관", "경북", "3개사"),
            new(new DateTime(2026, 3, 9, 9, 0, 0), "B-1", "입찰-1", "기관", "경북", "5개사"),
            new(new DateTime(2026, 3, 9, 10, 0, 0), "A-1", "입찰-낙찰전환", "기관", "경북", "7개사"),
        };
        var awardRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 3, 9, 11, 0, 0), "A-1", "낙찰-1", "기관", "경북", "1건"),
        };
        var snapshotRows = new List<NoticeRowModel>
        {
            new("A-1", "낙찰-1", "기관", new DateTime(2026, 3, 9, 11, 0, 0), 99.95, true),
        };

        var visible = CollectorNoticeRules.CreateVisibleBidRows(
            bidRows,
            awardRows,
            snapshotRows,
            new DateTime(2026, 3, 9, 8, 0, 0));

        Assert.Single(visible);
        Assert.Equal("B-1", visible[0].NoticeNumber);
    }

    [Fact]
    public void ResolveVisibleBidNoticeRows_CollapsesDuplicateDisplayRows()
    {
        var bidRows = new List<CollectorNoticeRowModel>
        {
            new(new DateTime(2026, 3, 17, 18, 0, 0), "R26BK01375264", "창녕 영산읍성지 정밀발굴조사 용역", "경상남도 창녕군", "경남", "5개사"),
            new(new DateTime(2026, 3, 17, 18, 0, 0), "R26BK01376115", "창녕 영산읍성지 정밀발굴조사 용역", "경상남도 창녕군", "경남", "5개사"),
        };

        var visible = CollectorNoticeRules.CreateVisibleBidRows(
            bidRows,
            [],
            [],
            new DateTime(2026, 3, 17, 17, 0, 0));

        Assert.Single(visible);
        Assert.Equal("R26BK01376115", visible[0].NoticeNumber);
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
