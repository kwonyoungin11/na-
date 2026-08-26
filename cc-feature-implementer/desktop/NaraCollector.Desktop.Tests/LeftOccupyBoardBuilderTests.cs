using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class LeftOccupyBoardBuilderTests
{
    [Fact]
    public void Build_KeepsEngineCategoriesAndKoreanLabels()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
            ShadowBaseOffset = 0.013,
            ShadowMinOffset = 0.013,
            ShadowOffsetStdMultiplier = 0.0,
        };
        var rows = BuildCandidateParticipants();

        var items = LeftOccupyBoardBuilder.Build(rows, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 99.5, 100.0, profile);

        Assert.Contains(items, x => x.Category == "candidate" && x.CategoryLabel == "좌측선점 후보");
        Assert.Contains(items, x => x.Category == "forbidden" || x.Category == "boundary");
        var candidate = items.First(x => x.Category == "candidate");
        Assert.False(string.IsNullOrWhiteSpace(candidate.AnchorCompany));
        Assert.True(candidate.ZoneEnd > candidate.ZoneStart);
        Assert.Contains("앵커", candidate.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(items, x => x.Category == "boundary" && string.IsNullOrWhiteSpace(x.AnchorCompany));
        Assert.Equal(items.OrderBy(x => CategoryOrder(x.Category)).ThenBy(x => x.ZoneStart).Select(x => x.ZoneStart), items.Select(x => x.ZoneStart));
    }

    [Fact]
    public void Build_DropsRowsOutsideSelectedPeriod()
    {
        var rows = BuildCandidateParticipants()
            .Select(x => x with { OpenDate = new DateTime(2025, 1, 1) })
            .ToList();
        rows.AddRange(BuildCandidateParticipants());

        var items = LeftOccupyBoardBuilder.Build(
            rows,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            99.5,
            100.0,
            CompetitionAnalysisProfile.Default with
            {
                MyCompanyKeyword = "동국문화재연구원",
                ExcludedCompanyKeywords = [],
                ShadowBaseOffset = 0.013,
                ShadowMinOffset = 0.013,
                ShadowOffsetStdMultiplier = 0.0,
            });

        Assert.NotEmpty(items);
        Assert.All(items, x => Assert.InRange(x.ZoneStart, 99.5, 100.0));
    }

    private static int CategoryOrder(string category)
        => category switch
        {
            "candidate" => 0,
            "forbidden" => 1,
            "boundary" => 2,
            _ => 9,
        };

    private static IReadOnlyList<ParticipantRecord> BuildCandidateParticipants()
    {
        var participants = new List<ParticipantRecord>();
        for (var i = 0; i < 8; i++)
        {
            var openDate = new DateTime(2026, 1, 1).AddDays(i * 7);
            participants.Add(CreateParticipant($"C{i:000}", openDate, "앵커연구원", 99.7300 + ((i % 2) * 0.0001), i % 3 == 0 ? 1 : 2));
            participants.Add(CreateParticipant($"C{i:000}", openDate, "동국문화재연구원", 99.7240 + ((i % 2) * 0.0001), 2));
            participants.Add(CreateParticipant($"C{i:000}", openDate, "보조연구원", 99.7010 + ((i % 3) * 0.0002), 3));
            if (i < 3)
            {
                participants.Add(CreateParticipant($"C{i:000}", openDate, $"지원연구원{i + 1}", 99.7162 + (i * 0.0002), i == 0 ? 1 : 4));
            }
        }

        return participants;
    }

    private static ParticipantRecord CreateParticipant(
        string noticeNumber,
        DateTime openDate,
        string companyName,
        double adjustRate,
        int rank)
        => new(
            NoticeNo: noticeNumber,
            NoticeNumber: noticeNumber,
            NoticeName: $"공고-{noticeNumber}",
            OrderingAgency: "테스트기관",
            Region: "전국",
            Rank: rank,
            CompanyName: companyName,
            NormalizedCompanyName: companyName,
            OpenDate: openDate,
            BidDate: openDate.AddMinutes(-30),
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
            IsAwarded: rank == 1);
}
