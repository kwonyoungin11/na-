using ClosedXML.Excel;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class RetiredCompanyExclusionTests
{
    [Fact]
    public void LoadParticipantsFromMasterExcel_ExcludesOnlyHyorim_AndKeepsKoreanInstitute()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"participants-{Guid.NewGuid():N}.xlsx");
        try
        {
            var openDate = new DateTime(2026, 2, 15, 10, 30, 0);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Sheet1");
                ws.Cell(1, 1).Value = "공고번호";
                ws.Cell(1, 2).Value = "개찰일시";
                ws.Cell(1, 3).Value = "업체명";
                ws.Cell(1, 4).Value = "순위";
                ws.Cell(1, 5).Value = "지역제한";
                ws.Cell(1, 6).Value = "공고명";
                ws.Cell(1, 7).Value = "발주처";
                ws.Cell(1, 8).Value = "기초대비사정률";

                WriteRow(ws, 2, "R26BK0001-000", openDate, "동국문화재연구원", 1, "전국", "공고-1", "기관A", 99.81234);
                WriteRow(ws, 3, "R26BK0002-000", openDate, "효림문화재연구원", 2, "전국", "공고-2", "기관A", 99.71234);
                WriteRow(ws, 4, "R26BK0003-000", openDate, "한국문화재연구원", 3, "전국", "공고-3", "기관A", 99.61234);

                workbook.SaveAs(tempFile);
            }

            var parser = new NaraApiParticipantParser();
            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 2, 1),
                EndDate: new DateTime(2026, 2, 28),
                Region: "전국",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: "전체",
                Company: "전체",
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var participants = parser.LoadParticipantsFromMasterExcel(tempFile, filter, CancellationToken.None);

            Assert.Equal(2, participants.Count);
            Assert.DoesNotContain(
                participants,
                row => row.CompanyName.Contains("효림", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                participants,
                row => row.CompanyName.Contains("한국문화재연구원", StringComparison.OrdinalIgnoreCase));

            var snapshot = new NaraApiSnapshotBuilder().BuildSnapshot(participants, filter, "unit-test");
            var companyOptions = snapshot.CompanyOptions ?? [];
            Assert.Equal(2, companyOptions.Count);
            Assert.DoesNotContain(
                companyOptions,
                name => name.Contains("효림", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                companyOptions,
                name => name.Contains("한국문화재연구원", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void WriteRow(
        IXLWorksheet worksheet,
        int rowIndex,
        string noticeNumber,
        DateTime openDate,
        string companyName,
        int rank,
        string region,
        string noticeName,
        string orderingAgency,
        double adjustRate)
    {
        worksheet.Cell(rowIndex, 1).Value = noticeNumber;
        worksheet.Cell(rowIndex, 2).Value = openDate.ToString("yyyy-MM-dd HH:mm:ss");
        worksheet.Cell(rowIndex, 3).Value = companyName;
        worksheet.Cell(rowIndex, 4).Value = rank;
        worksheet.Cell(rowIndex, 5).Value = region;
        worksheet.Cell(rowIndex, 6).Value = noticeName;
        worksheet.Cell(rowIndex, 7).Value = orderingAgency;
        worksheet.Cell(rowIndex, 8).Value = adjustRate;
    }
}
