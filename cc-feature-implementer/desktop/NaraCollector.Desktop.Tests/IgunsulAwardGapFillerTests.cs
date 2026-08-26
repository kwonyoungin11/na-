using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class IgunsulAwardGapFillerTests
{
    [Fact]
    public void FindMissingAwardNoticeNumbers_ReturnsAwardsAbsentFromNaraRows()
    {
        var naraRows = new List<ParticipantRecord>
        {
            MakeRow("R26BK00000001-000", 1, "동국문화재연구원"),
        };
        var awards = new[]
        {
            "R26BK00000001-000",
            "R26BK01673928-000",
            "R26BK01674020-000",
        };

        var missing = CollectorNoticeRules.FindMissingAwardNoticeNumbers(naraRows, awards);

        Assert.Equal(["R26BK01673928-000", "R26BK01674020-000"], missing);
    }

    [Fact]
    public void FillFromCache_LoadsOnlyMissingNoticesAndAppliesCompanyRule()
    {
        var missing = new[] { "R26BK01673928-000" };
        var rows = IgunsulAwardGapFiller.FillFromCache(missing, FixtureDir());

        Assert.NotEmpty(rows);
        Assert.All(rows, x => Assert.Equal("R26BK01673928-000", x.NoticeNumber));
        Assert.True(rows.Select(x => x.NormalizedCompanyName).Distinct().Count() >= CollectorNoticeRules.MinParticipatingCompanies);
        Assert.DoesNotContain(rows, x => x.NoticeNumber == "R26BK01661807-000");
    }

    [Fact]
    public void FillFromCache_SkipsNoticesAlreadyOnNara()
    {
        var missing = CollectorNoticeRules.FindMissingAwardNoticeNumbers(
            IgunsulExcelConverter.ConvertDirectory(FixtureDir()),
            ["R26BK01673928-000", "R26BK01674020-000"]);

        Assert.Empty(missing);
    }

    private static string FixtureDir()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "igunsul");
    }

    private static ParticipantRecord MakeRow(string notice, int rank, string company)
    {
        return new ParticipantRecord(
            NoticeNo: notice.Split('-')[0],
            NoticeNumber: notice,
            NoticeName: "공고",
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
}
