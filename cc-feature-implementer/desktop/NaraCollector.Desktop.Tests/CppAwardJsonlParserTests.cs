using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CppAwardJsonlParserTests
{
    [Fact]
    public void Load_ReadsAgencyRateAndDonggukRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cpp-award-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(
            path,
            """
            {"notice_number":"R26BK01366528-000","notice_name":"울산 서생리 정밀발굴조사 용역","ordering_agency":"울산광역시","region":"전국","open_date":"2026-03-12 11:00:00","rank":1,"company_name":"한울문화재연구원","adjust_rate":99.78}
            {"notice_number":"R26BK01366528-000","notice_name":"울산 서생리 정밀발굴조사 용역","ordering_agency":"울산광역시","region":"전국","open_date":"2026-03-12 11:00:00","rank":4,"company_name":"동국문화재연구원","adjust_rate":99.65}
            """);
        try
        {
            var rows = CppAwardJsonlParser.Load(path);
            Assert.Equal(2, rows.Count);
            Assert.Equal("울산광역시", rows[0].OrderingAgency);
            Assert.Equal(99.65, rows[1].AdjustRate);
            Assert.Contains(rows, x => x.CompanyName.Contains("동국", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SortOverlappingChips_DrawsDonggukLast()
    {
        var bucket = new List<NaraCollector.Desktop.Models.ParticipantRecord>
        {
            Row("한울문화재연구원", 2),
            Row("동국문화재연구원", 54),
            Row("춘추문화재연구원", 1),
        };

        var sorted = ScatterChartRules.SortOverlappingChips(bucket);

        Assert.Equal("동국문화재연구원", sorted[^1].CompanyName);
    }

    [Fact]
    public void Load_Converts86ScaleUsingLowerRate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cpp-award-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(
            path,
            """
            {"notice_number":"R26BK00000001-000","notice_name":"조사","ordering_agency":"기관","region":"전국","open_date":"2026-03-12 11:00:00","rank":4,"company_name":"동국문화재연구원","adjust_rate":86.447,"lower_rate":86.745}
            """);
        try
        {
            var rows = CppAwardJsonlParser.Load(path);
            Assert.Single(rows);
            Assert.InRange(rows[0].AdjustRate!.Value, 99.0, 101.0);
            Assert.Equal(99.65647, rows[0].AdjustRate!.Value, 4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static NaraCollector.Desktop.Models.ParticipantRecord Row(string company, int rank)
    {
        return new(
            NoticeNo: "N",
            NoticeNumber: "N",
            NoticeName: "공고",
            OrderingAgency: "기관",
            Region: "전국",
            Rank: rank,
            CompanyName: company,
            NormalizedCompanyName: company,
            OpenDate: new DateTime(2026, 8, 1),
            BidDate: null,
            BidAmount: 1,
            BidRateVsEstimate: null,
            BidRateVsBase: null,
            AdjustRate: 99.6,
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
