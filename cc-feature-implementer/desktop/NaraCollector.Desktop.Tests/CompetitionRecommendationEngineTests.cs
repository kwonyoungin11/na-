using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class CompetitionRecommendationEngineTests
{
    [Fact]
    public void Analyze_ReturnsCandidateRecommendationOnlyWhenZoneSemanticsAreCandidate()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
            ShadowBaseOffset = 0.013,
            ShadowMinOffset = 0.013,
            ShadowOffsetStdMultiplier = 0.0,
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = BuildCandidateParticipants();

        var result = engine.Analyze(participants, 99.5, 100.0);

        Assert.True(result.HasRecommendation);
        Assert.Equal("앵커연구원", result.SourceCompany);
        Assert.Equal("앵커연구원", result.TargetCompany);
        Assert.Equal("candidate", result.DecisionCategory);
        Assert.Equal("앵커연구원", result.AnchorCompany);
        Assert.True(result.LeftSpaceWidth.HasValue);
        Assert.Equal("left_anchor", result.LeftSpaceSource);
        Assert.True(result.LeftSpaceWidth.Value >= profile.DecisionMinLeftSpaceWidth);
        Assert.True(result.SupportingNoticeCount >= 3);
        Assert.True(result.AwardNoticeCount >= 1);
        Assert.True(result.EvidenceScore >= profile.ShadowMinimumPriorityScore);
    }

    [Fact]
    public void Analyze_ReturnsEmptyBoundaryWhenOnlyBoundarySemanticsExist()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = new List<ParticipantRecord>();

        for (var i = 0; i < 8; i++)
        {
            var openDate = new DateTime(2026, 1, 1).AddDays(i * 7);
            participants.Add(CreateParticipant($"B{i:000}", openDate, "앵커연구원", 99.7300 + ((i % 2) * 0.0001), 2));
            if (i < 4)
            {
                participants.Add(CreateParticipant($"B{i:000}", openDate, "동국문화재연구원", 99.7240 + ((i % 2) * 0.0001), 1));
            }
        }

        var result = engine.Analyze(participants, 99.5, 100.0);
        var detail = engine.AnalyzeDetailed(participants, 99.5, 100.0);
        var boundaryZone = detail.ZoneDecisions.First(x => x.AnchorCompany == "앵커연구원" && x.LeftSpaceSource == "boundary");
        var expectedBoundaryDistance = Math.Round(((boundaryZone.ZoneStart + boundaryZone.ZoneEnd) / 2.0) - 99.5, 5);

        Assert.False(result.HasRecommendation);
        Assert.Equal("boundary", result.DecisionCategory);
        Assert.Empty(detail.Recommendations);
        Assert.Equal("boundary", boundaryZone.Category);
        Assert.Null(boundaryZone.LeftSpaceWidth);
        Assert.Equal(expectedBoundaryDistance, boundaryZone.LeftBoundaryDistance);
    }

    [Fact]
    public void AnalyzeDetailed_DoesNotPromoteAnchorlessZoneToCandidate()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = new List<ParticipantRecord>();

        for (var i = 0; i < 8; i++)
        {
            var openDate = new DateTime(2026, 1, 1).AddDays(i * 7);
            participants.Add(CreateParticipant($"F{i:000}", openDate, "앵커연구원", 99.7300 + ((i % 2) * 0.0001), 2));
            participants.Add(CreateParticipant($"F{i:000}", openDate, "동국문화재연구원", 99.7240 + ((i % 2) * 0.0001), 1));
            participants.Add(CreateParticipant($"F{i:000}", openDate, "보조연구원", 99.7010 + ((i % 3) * 0.0002), 3));
        }

        var detail = engine.AnalyzeDetailed(participants, 99.5, 100.0);

        Assert.True(detail.FixedBidderCount >= 1);
        Assert.DoesNotContain(
            detail.ZoneDecisions,
            x => x.Category == "candidate" && x.AnchorGap is null);
    }

    [Fact]
    public void AnalyzeDetailed_ExcludesMyCompanyFromCompetitorSampleCount()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = BuildCandidateParticipants();

        var detail = engine.AnalyzeDetailed(participants, 99.5, 100.0);

        Assert.Equal(19, detail.CompetitorSampleCount);
        Assert.DoesNotContain(
            detail.Recommendations,
            x => string.Equals(x.SourceCompany, "동국문화재연구원", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ReturnsEmptyBoundaryWhenSupportingEvidenceIsTooThin()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = new List<ParticipantRecord>();

        for (var i = 0; i < 2; i++)
        {
            var openDate = new DateTime(2026, 2, 1).AddDays(i * 7);
            participants.Add(CreateParticipant($"T{i:000}", openDate, "앵커연구원", 99.7300, 2));
            participants.Add(CreateParticipant($"T{i:000}", openDate, "보조연구원", 99.7010, i == 0 ? 1 : 3));
        }

        var result = engine.Analyze(participants, 99.5, 100.0);

        Assert.False(result.HasRecommendation);
        Assert.Equal("boundary", result.DecisionCategory);
    }

    [Fact]
    public void AnalyzeDetailed_UsesRequestedMinRateForBoundaryDistance()
    {
        var profile = CompetitionAnalysisProfile.Default with
        {
            MyCompanyKeyword = "동국문화재연구원",
            ExcludedCompanyKeywords = [],
        };
        var engine = new CompetitionRecommendationEngine(profile);
        var participants = new List<ParticipantRecord>();

        for (var i = 0; i < 6; i++)
        {
            var openDate = new DateTime(2026, 1, 1).AddDays(i * 7);
            participants.Add(CreateParticipant($"M{i:000}", openDate, "앵커연구원", 99.7300 + ((i % 2) * 0.0001), 2));
        }

        var detail = engine.AnalyzeDetailed(participants, 99.0, 100.0);
        var boundaryZone = detail.ZoneDecisions.First(x => x.LeftSpaceSource == "boundary");
        var zoneCenter = Math.Round((boundaryZone.ZoneStart + boundaryZone.ZoneEnd) / 2.0, 5);
        var expectedBoundaryDistance = Math.Round(zoneCenter - 99.0, 5);

        Assert.Equal(expectedBoundaryDistance, boundaryZone.LeftBoundaryDistance);
    }

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
    {
        return new ParticipantRecord(
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
}
