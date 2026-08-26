using ClosedXML.Excel;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class IgunsulExcelConverterTests
{
    [Fact]
    public void Convert_ChangnyeongGoam_UsesOverviewParenAdjustRateNotSignedParticipantRate()
    {
        var overviewPath = FixturePath("changnyeong-goam_overview.xlsx");
        var participantsPath = FixturePath("changnyeong-goam_participants.xlsx");

        var rows = IgunsulExcelConverter.Convert(overviewPath, participantsPath);

        using var overview = new XLWorkbook(overviewPath);
        using var participants = new XLWorkbook(participantsPath);
        var overviewSheet = overview.Worksheet(1);
        var participantSheet = participants.Worksheet(1);

        var expectedParen = ReadOverviewHundredScale(overviewSheet);
        var expectedCompany = participantSheet.Cell(2, 3).GetString().Trim();
        var signedCell = participantSheet.Cell(2, 7).GetDouble();
        var expectedCount = participantSheet.RowsUsed().Count() - 1;

        Assert.Equal(expectedCount, rows.Count);
        var rank1 = Assert.Single(rows, x => x.Rank == 1);
        Assert.Equal(expectedCompany, rank1.CompanyName);
        Assert.Equal(expectedParen, rank1.PriceAdjustmentRate);
        Assert.NotEqual(signedCell, rank1.AdjustRate);
        Assert.InRange(rank1.AdjustRate ?? 0, 99.0, 101.0);
        Assert.Equal("R26BK01673928-000", rank1.NoticeNumber);
        Assert.Contains("창녕-고암", rank1.NoticeName, StringComparison.Ordinal);

        var dongguk = Assert.Single(rows, x => x.CompanyName.Contains("동국", StringComparison.Ordinal));
        Assert.InRange(dongguk.AdjustRate ?? 0, 99.0, 101.0);
    }

    [Fact]
    public void Convert_ClosedXmlWorkbook_ReadsSharedStringsAndParenRate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"igunsul-closedxml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var overviewPath = Path.Combine(root, "notice_overview.xlsx");
        var participantsPath = Path.Combine(root, "notice_participants.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Sheet1");
                ws.Cell(1, 1).Value = "항목";
                ws.Cell(1, 2).Value = "값";
                ws.Cell(2, 1).Value = "공고번호";
                ws.Cell(2, 2).Value = "R26BK01673928-000";
                ws.Cell(3, 1).Value = "공고명";
                ws.Cell(3, 2).Value = "창녕-고암 국도건설공사 문화재 정밀발굴조사용역";
                ws.Cell(4, 1).Value = "발주처";
                ws.Cell(4, 2).Value = "부산지방조달청";
                ws.Cell(5, 1).Value = "지역제한";
                ws.Cell(5, 2).Value = "전국";
                ws.Cell(6, 1).Value = "개찰일시";
                ws.Cell(6, 2).Value = "2026/08/20 11:00:00";
                ws.Cell(7, 1).Value = "기초금액";
                ws.Cell(7, 2).Value = 143400000;
                ws.Cell(8, 1).Value = "예정가격";
                ws.Cell(8, 2).Value = 143063425;
                ws.Cell(9, 1).Value = "낙찰하한가";
                ws.Cell(9, 2).Value = 123385051;
                ws.Cell(10, 1).Value = "사정률";
                ws.Cell(10, 2).Value = "-0.23472% (99.76628%)";
                workbook.SaveAs(overviewPath);
            }

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Sheet1");
                ws.Cell(1, 1).Value = "순위";
                ws.Cell(1, 2).Value = "사업자등록번호";
                ws.Cell(1, 3).Value = "업체명";
                ws.Cell(1, 4).Value = "투찰금액";
                ws.Cell(1, 5).Value = "예가대비투찰률(%)";
                ws.Cell(1, 6).Value = "기초대비투찰률(%)";
                ws.Cell(1, 7).Value = "기초대비사정률(%)";
                ws.Cell(1, 8).Value = "투찰일시";
                var companies = new (int Rank, string Biz, string Name, long Amount, double VsEst, double VsBase, double Signed)[]
                {
                    (1, "8118200255", "재단법인 춘추문화유산연구원", 123403300, 86.257, 86.055, -0.21996),
                    (2, "4028214439", "재단법인 전북문화유산연구원", 123404820, 86.258, 86.056, -0.21873),
                    (3, "7868200186", "재단법인경강문화유산연구원", 123407900, 86.260, 86.058, -0.21624),
                    (4, "1234567890", "한울문화재연구원", 123412000, 86.263, 86.061, -0.21000),
                    (5, "1234567891", "가람문화재연구원", 123420000, 86.270, 86.068, -0.20000),
                    (6, "1234567892", "나라문화연구원", 123430000, 86.280, 86.078, -0.19000),
                };
                for (var i = 0; i < companies.Length; i++)
                {
                    var row = i + 2;
                    ws.Cell(row, 1).Value = companies[i].Rank;
                    ws.Cell(row, 2).Value = companies[i].Biz;
                    ws.Cell(row, 3).Value = companies[i].Name;
                    ws.Cell(row, 4).Value = companies[i].Amount;
                    ws.Cell(row, 5).Value = companies[i].VsEst;
                    ws.Cell(row, 6).Value = companies[i].VsBase;
                    ws.Cell(row, 7).Value = companies[i].Signed;
                    ws.Cell(row, 8).Value = "2026/08/20 09:03:00";
                }
                workbook.SaveAs(participantsPath);
            }

            var rows = IgunsulExcelConverter.Convert(overviewPath, participantsPath);
            Assert.Equal(6, rows.Count);
            var rank1 = Assert.Single(rows, x => x.Rank == 1);
            using var overview = new XLWorkbook(overviewPath);
            Assert.True(
                IgunsulExcelConverter.TryParseSignedParen(
                    overview.Worksheet(1).Cell(10, 2).GetString(),
                    out _,
                    out var paren));
            Assert.Equal(paren, rank1.PriceAdjustmentRate);
            Assert.NotEqual(-0.21996, rank1.AdjustRate);
            Assert.InRange(rank1.AdjustRate ?? 0, 99.0, 101.0);
            Assert.Equal("재단법인 춘추문화유산연구원", rank1.CompanyName);
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
    public void ConvertDirectory_EmitsAllThreeNotices()
    {
        var rows = IgunsulExcelConverter.ConvertDirectory(FixtureDir());

        var notices = rows
            .Select(x => x.NoticeNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(
            ["R26BK01673928-000", "R26BK01674020-000"],
            notices);
        Assert.Contains(rows, x => x.NoticeNumber == "R26BK01674020-000" && x.Rank == 1);
        Assert.DoesNotContain(rows, x => x.NoticeNumber == "R26BK01661807-000");
    }

    private static double ReadOverviewHundredScale(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed())
        {
            var label = row.Cell(1).GetString().Trim();
            if (label.Contains("사정률", StringComparison.Ordinal))
            {
                Assert.True(
                    IgunsulExcelConverter.TryParseSignedParen(row.Cell(2).GetString(), out _, out var hundred));
                return hundred;
            }
        }

        throw new InvalidOperationException("사정률 셀을 fixture에서 찾지 못했습니다.");
    }

    private static string FixtureDir()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "igunsul");
    }

    private static string FixturePath(string fileName)
    {
        return Path.Combine(FixtureDir(), fileName);
    }
}
