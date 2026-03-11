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
}
