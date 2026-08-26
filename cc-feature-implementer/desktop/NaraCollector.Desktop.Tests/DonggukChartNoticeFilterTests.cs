using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class DonggukChartNoticeFilterTests
{
    [Fact]
    public void KeepDonggukParticipatingNotices_DropsNoticesWithoutDongguk()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 1, "동국문화재연구원"),
            Row("A", 2, "한울문화재연구원"),
            Row("B", 1, "한울문화재연구원"),
            Row("B", 2, "호서문화재연구원"),
        };

        var kept = CollectorNoticeRules.KeepDonggukParticipatingNotices(rows);

        Assert.All(kept, x => Assert.Equal("A", x.NoticeNumber));
        Assert.Equal(2, kept.Count);
        Assert.DoesNotContain(kept, x => x.NoticeNumber == "B");
    }

    [Fact]
    public void DropExcludedNoticeNames_DropsImpactAssessment()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("G", 1, "동국문화재연구원", "금강 공주권역 배수영향구간 실시설계 국가유산영향진단 용역"),
            Row("D", 1, "동국문화재연구원", "창녕-고암 국도건설공사 문화재 정밀발굴조사용역"),
        };

        var kept = CollectorNoticeRules.DropExcludedNoticeNames(rows);

        Assert.Single(kept);
        Assert.Equal("D", kept[0].NoticeNumber);
        Assert.DoesNotContain(kept, x => x.NoticeName.Contains("영향진단", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyPersistRules_DropsImpactAssessmentAndNoticesWithFiveOrFewerCompanies()
    {
        var rows = new List<ParticipantRecord>();
        for (var i = 1; i <= 6; i++)
        {
            rows.Add(Row("DIG", i, $"업체{i}", "창녕-고암 문화재 정밀발굴조사용역"));
        }

        for (var i = 1; i <= 6; i++)
        {
            rows.Add(Row("IMP", i, $"진단{i}", "국가유산영향진단 용역"));
        }

        for (var i = 1; i <= 5; i++)
        {
            rows.Add(Row("TINY", i, $"소수{i}", "소규모 정밀발굴조사용역"));
        }

        var kept = CollectorNoticeRules.ApplyPersistRules(rows);

        Assert.All(kept, x => Assert.Equal("DIG", x.NoticeNumber));
        Assert.Equal(6, kept.Count);
    }

    [Fact]
    public void IsExcludedNoticeName_DoesNotTreat영향구간As영향진단()
    {
        Assert.False(CollectorNoticeRules.IsExcludedNoticeName("배수영향구간 정밀발굴조사용역"));
        Assert.True(CollectorNoticeRules.IsExcludedNoticeName("국가유산영향진단 용역"));
    }

    [Fact]
    public void ApplyChartRules_KeepsCoBiddersOnDonggukNoticeAndDrops영향진단EvenIfDonggukBid()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 1, "동국문화재연구원", "정밀발굴조사 용역"),
            Row("A", 2, "한울문화재연구원", "정밀발굴조사 용역"),
            Row("G", 1, "동국문화재연구원", "국가유산영향진단 용역"),
            Row("G", 2, "한울문화재연구원", "국가유산영향진단 용역"),
        };

        var kept = CollectorNoticeRules.ApplyChartRules(rows);

        Assert.Equal(2, kept.Count);
        Assert.All(kept, x => Assert.Equal("A", x.NoticeNumber));
        Assert.Contains(kept, x => x.CompanyName.Contains("한울", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyChartRules_ThenCompanySlice_KeepsCoBidderOnDonggukNotice()
    {
        var rows = new List<ParticipantRecord>
        {
            Row("A", 1, "동국문화재연구원"),
            Row("A", 2, "한울문화재연구원"),
            Row("B", 1, "한울문화재연구원"),
            Row("B", 2, "호서문화재연구원"),
        };

        var universe = CollectorNoticeRules.ApplyChartRules(rows);
        var hanul = universe.Where(x => x.CompanyName.Contains("한울", StringComparison.Ordinal)).ToList();

        Assert.Single(hanul);
        Assert.Equal("A", hanul[0].NoticeNumber);
    }

    private static ParticipantRecord Row(string notice, int rank, string company, string? name = null)
    {
        return new ParticipantRecord(
            NoticeNo: notice,
            NoticeNumber: notice,
            NoticeName: name ?? "정밀발굴조사 용역",
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: company,
            NormalizedCompanyName: company,
            OpenDate: new DateTime(2026, 8, 20),
            BidDate: null,
            BidAmount: 1,
            BidRateVsEstimate: 99,
            BidRateVsBase: 99,
            AdjustRate: 99.6,
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
}
