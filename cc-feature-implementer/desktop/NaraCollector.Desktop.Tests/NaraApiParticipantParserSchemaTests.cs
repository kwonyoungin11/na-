using ClosedXML.Excel;
using NaraCollector.Desktop.Models;
using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public class NaraApiParticipantParserSchemaTests
{
    [Fact]
    public void LoadParticipantsFromMasterExcel_SupportsCurrentMasterSchemaAliases()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"participants-schema-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Sheet1");
                ws.Cell(1, 1).Value = "공고번호";
                ws.Cell(1, 2).Value = "개찰일시";
                ws.Cell(1, 3).Value = "업체명";
                ws.Cell(1, 4).Value = "순위";
                ws.Cell(1, 5).Value = "지역제한";
                ws.Cell(1, 6).Value = "공사명";
                ws.Cell(1, 7).Value = "발주처";
                ws.Cell(1, 8).Value = "예가대비투찰률(%)";
                ws.Cell(1, 9).Value = "기초대비투찰률(%)";
                ws.Cell(1, 10).Value = "기초대비사정률(%)";
                ws.Cell(1, 11).Value = "투찰률";

                ws.Cell(2, 1).Value = "R26BK0001-000";
                ws.Cell(2, 2).Value = "2026-03-12 11:00:00";
                ws.Cell(2, 3).Value = "동국문화재연구원";
                ws.Cell(2, 4).Value = 3;
                ws.Cell(2, 5).Value = "전국";
                ws.Cell(2, 6).Value = "경주 발굴조사";
                ws.Cell(2, 7).Value = "기관A";
                ws.Cell(2, 8).Value = 99.111;
                ws.Cell(2, 9).Value = 99.222;
                ws.Cell(2, 10).Value = 99.33333;
                ws.Cell(2, 11).Value = 87.745;

                workbook.SaveAs(tempFile);
            }

            var parser = new NaraApiParticipantParser();
            var filter = new DashboardFilter(
                StartDate: new DateTime(2026, 3, 1),
                EndDate: new DateTime(2026, 3, 31),
                Region: "전국",
                Keyword: string.Empty,
                OrderingAgency: string.Empty,
                DataType: "전체",
                Company: string.Empty,
                MinAdjustRate: null,
                MaxAdjustRate: null);

            var participants = parser.LoadParticipantsFromMasterExcel(tempFile, filter, CancellationToken.None);

            var row = Assert.Single(participants);
            Assert.Equal("경주 발굴조사", row.NoticeName);
            Assert.True(row.BidRateVsEstimate.HasValue);
            Assert.True(row.BidRateVsBase.HasValue);
            Assert.True(row.AdjustRate.HasValue);
            Assert.True(row.LowerRate.HasValue);
            Assert.Equal(99.111, row.BidRateVsEstimate!.Value, 3);
            Assert.Equal(99.222, row.BidRateVsBase!.Value, 3);
            Assert.Equal(99.33333, row.AdjustRate!.Value, 5);
            Assert.Equal(87.745, row.LowerRate!.Value, 3);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
