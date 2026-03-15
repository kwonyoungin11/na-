using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class NaraApiDashboardRepositoryTests
{
    [Fact]
    public void NormalizeKeywords_BlankInput_ReturnsEmpty()
    {
        var value = NaraApiDashboardRepository.TestHook_NormalizeKeywords(string.Empty);

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void NormalizeKeywords_RemovesDuplicatesAndSeparators()
    {
        var value = NaraApiDashboardRepository.TestHook_NormalizeKeywords("발굴조사, 시굴조사; 발굴조사 | 지표조사");

        Assert.Equal("발굴조사,시굴조사,지표조사", value);
    }

    [Fact]
    public void LoadDefaultKeywords_ReadsPrimaryKeywordsFromRuleProfile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collector-rule-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "primary_keywords": ["발굴조사", "정밀발굴조사", "시굴조사", "표본조사"]
                }
                """);

            var keywords = NaraApiDashboardRepository.TestHook_LoadDefaultKeywords(path);

            Assert.Equal("발굴조사,정밀발굴조사,시굴조사,표본조사", keywords);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData("대구/경북", "대구/경북")]
    [InlineData("전국,경북", "전국")]
    [InlineData("대구", "대구")]
    public void NormalizeRegionValue_NormalizesExpectedRegion(string source, string expected)
    {
        var value = NaraApiDashboardRepository.TestHook_NormalizeRegionValue(source);

        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("R25BK01210307-000", "R25BK01210307")]
    [InlineData("2503992-00", "2503992")]
    [InlineData("2503992", "2503992")]
    public void ExtractNoticeNo_ReturnsNormalizedNoticeNo(string noticeNumber, string expected)
    {
        var value = NaraApiDashboardRepository.TestHook_ExtractNoticeNo(noticeNumber);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void LoadCollectorNoticeFeed_ReadsRawBidAndAwardJsonl()
    {
        var root = Path.Combine(Path.GetTempPath(), $"collector-feed-{Guid.NewGuid():N}");
        var rawMonthDirectory = Path.Combine(root, "raw", "2026-03");
        Directory.CreateDirectory(rawMonthDirectory);

        try
        {
            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "bid_notice_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK01363668","bidNtceNm":"(긴급)대구 달성 북성벽 시굴·정밀발굴조사용역","dminsttNm":"대구광역시","opengDt":"2026-03-17 11:00:00","rgstDt":"2026-03-08 13:27:00","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"대구"}
                    """,
                    """
                    {"bidNtceNo":"R26BK99999999","bidNtceNm":"2026년 인천사회지표 조사 및 지역통계 작성 용역","dminsttNm":"인천광역시","opengDt":"2026-03-27 16:00:00","rgstDt":"2026-03-08 13:27:30","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"인천"}
                    """,
                    """
                    {"bidNtceNo":"R26BK88888888","bidNtceNm":"경주 서악동 고분군 정밀발굴조사 용역","dminsttNm":"경주시","opengDt":"2026-03-12 14:00:00","rgstDt":"2026-03-08 13:27:40","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"경북"}
                    """,
                    """
                    {"bidNtceNo":"R26BK77777777","bidNtceNm":"문화재 시굴조사 관련 장비 물품","dminsttNm":"경주시","opengDt":"2026-03-18 10:00:00","rgstDt":"2026-03-08 13:27:50","_src_group":"thng","rgnLmtBidLocplcJdgmBssNm":"경북"}
                    """
                ]);

            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "openg_result_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK01363668","bidNtceNm":"(긴급)대구 달성 북성벽 시굴·정밀발굴조사용역","dminsttNm":"대구광역시","opengDt":"2026-03-08 13:28:00","prtcptCnum":"8","_src_group":"servc"}
                    """,
                    """
                    {"bidNtceNo":"R26BK88888888","bidNtceNm":"경주 서악동 고분군 정밀발굴조사 용역","dminsttNm":"경주시","opengDt":"2026-03-08 13:28:20","prtcptCnum":"3","_src_group":"servc"}
                    """
                ]);

            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 3, 1, 0, 0, 0),
                EndDate: new DateTime(2026, 3, 31, 23, 59, 59),
                Region: "경북",
                Keyword: "발굴조사",
                OrderingAgency: string.Empty,
                DataType: "낙찰",
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var result = NaraApiDashboardRepository.TestHook_LoadCollectorNoticeFeed(root, filter);

            var bid = Assert.Single(result.BidRows);
            Assert.Equal("R26BK88888888", bid.NoticeNumber);
            Assert.Equal("경북", bid.RegionText);

            var award = Assert.Single(result.AwardRows);
            Assert.Equal("R26BK88888888", award.NoticeNumber);
            Assert.Equal("3개사", award.ParticipantCountText);
            Assert.Equal("경북", award.RegionText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadCollectorNoticeFeed_NormalizesGenericCollectorRegionToUnknown()
    {
        var root = Path.Combine(Path.GetTempPath(), $"collector-feed-region-{Guid.NewGuid():N}");
        var rawMonthDirectory = Path.Combine(root, "raw", "2026-03");
        Directory.CreateDirectory(rawMonthDirectory);

        try
        {
            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "bid_notice_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK11111111","bidNtceNm":"고창군 용계리 청자요지 보호구역 시굴조사 용역 수의(소액) 견적 제출 안내","dminsttNm":"전북특별자치도 고창군","opengDt":"2026-03-08 13:28:00","rgstDt":"2026-03-08 13:27:00","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"본사또는참여지사소재지"}
                    """
                ]);

            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 3, 1, 0, 0, 0),
                EndDate: new DateTime(2026, 3, 31, 23, 59, 59),
                Region: "경북",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: "낙찰",
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var result = NaraApiDashboardRepository.TestHook_LoadCollectorNoticeFeed(root, filter);

            Assert.Empty(result.BidRows);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadCollectorNoticeFeed_UsesMasterShapedCollectorScope()
    {
        var root = Path.Combine(Path.GetTempPath(), $"collector-feed-master-shape-{Guid.NewGuid():N}");
        var rawMonthDirectory = Path.Combine(root, "raw", "2026-03");
        Directory.CreateDirectory(rawMonthDirectory);

        try
        {
            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "bid_notice_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK20000001","bidNtceNm":"마령재터널 건설공사 문화유산 발굴조사용역","dminsttNm":"경상북도","bidClseDt":"2026-03-18 11:00:00","rgstDt":"2026-03-08 13:27:00","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"경북"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000002","bidNtceNm":"대구 미래스마트기술 국가산업단지 국가유산영향진단 용역","dminsttNm":"한국토지주택공사 대구경북지역본부","bidClseDt":"2026-03-19 11:00:00","rgstDt":"2026-03-08 13:27:10","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"전국"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000003","bidNtceNm":"익산망기 공공지원민간임대주택 공급촉진지구 매장유산 발굴(표본,시굴)조사 용역","dminsttNm":"전북특별자치도 익산시","bidClseDt":"2026-03-20 11:00:00","rgstDt":"2026-03-08 13:27:20","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"전북"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000004","bidNtceNm":"2026년 강원역사문화연구원 매장유산 발굴조사 중장비 단가계약","dminsttNm":"재단법인강원역사문화연구원","bidClseDt":"2026-03-21 11:00:00","rgstDt":"2026-03-08 13:27:30","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"강원"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000005","bidNtceNm":"조세이탄광 수몰사고 희생자 유해발굴 조사 방안 연구","dminsttNm":"행정안전부 과거사관련업무지원단","bidClseDt":"2026-03-22 11:00:00","rgstDt":"2026-03-08 13:27:40","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"전국"}
                    """
                ]);

            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "openg_result_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK20000001","bidNtceNm":"마령재터널 건설공사 문화유산 발굴조사용역","dminsttNm":"경상북도","opengDt":"2026-03-08 13:28:00","prtcptCnum":"14","_src_group":"servc"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000002","bidNtceNm":"대구 미래스마트기술 국가산업단지 국가유산영향진단 용역","dminsttNm":"한국토지주택공사 대구경북지역본부","opengDt":"2026-03-08 13:28:10","prtcptCnum":"8","_src_group":"servc"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000003","bidNtceNm":"익산망기 공공지원민간임대주택 공급촉진지구 매장유산 발굴(표본,시굴)조사 용역","dminsttNm":"전북특별자치도 익산시","opengDt":"2026-03-08 13:28:20","prtcptCnum":"11","_src_group":"servc"}
                    """,
                    """
                    {"bidNtceNo":"R26BK20000004","bidNtceNm":"2026년 강원역사문화연구원 매장유산 발굴조사 중장비 단가계약","dminsttNm":"재단법인강원역사문화연구원","opengDt":"2026-03-08 13:28:30","prtcptCnum":"2","_src_group":"servc"}
                    """
                ]);

            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 3, 1, 0, 0, 0),
                EndDate: new DateTime(2026, 3, 31, 23, 59, 59),
                Region: "전국",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: "낙찰",
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var result = NaraApiDashboardRepository.TestHook_LoadCollectorNoticeFeed(root, filter);

            Assert.Equal(
                ["R26BK20000002"],
                result.BidRows.Select(x => x.NoticeNumber).OrderBy(x => x).ToArray());
            Assert.Equal(
                ["R26BK20000002"],
                result.AwardRows.Select(x => x.NoticeNumber).OrderBy(x => x).ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadCollectorNoticeFeed_BidRowsUseBidDeadlineWindowInsteadOfRegistrationTime()
    {
        var root = Path.Combine(Path.GetTempPath(), $"collector-feed-bid-window-{Guid.NewGuid():N}");
        var rawMonthDirectory = Path.Combine(root, "raw", "2026-03");
        Directory.CreateDirectory(rawMonthDirectory);

        try
        {
            File.WriteAllLines(
                Path.Combine(rawMonthDirectory, "bid_notice_raw.jsonl"),
                [
                    """
                    {"bidNtceNo":"R26BK30000001","bidNtceNm":"상봉동 문화(역사)공원 조성사업 매장유산 발굴조사 용역","dminsttNm":"경상남도 진주시","bidClseDt":"2026-03-03 10:00:00","opengDt":"2026-03-03 11:00:00","rgstDt":"2026-02-12 16:37:31","_src_group":"servc","rgnLmtBidLocplcJdgmBssNm":"경남"}
                    """
                ]);

            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 2, 21, 0, 0, 0),
                EndDate: new DateTime(2026, 3, 8, 23, 59, 59),
                Region: "전체",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: "낙찰",
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var result = NaraApiDashboardRepository.TestHook_LoadCollectorNoticeFeed(root, filter);

            var bid = Assert.Single(result.BidRows);
            Assert.Equal("R26BK30000001", bid.NoticeNumber);
            Assert.Equal(new DateTime(2026, 3, 3, 10, 0, 0), bid.NoticeDate);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
