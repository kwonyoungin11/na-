using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using NaraCollector.Desktop.Controls;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed record ChartExportRequest(
    string InputPath,
    string OutputPath,
    string Precision,
    double MinRate,
    double MaxRate,
    int Width,
    int Height,
    string Company);

public static class ChartImageExporter
{
    private const string AllCompanyOption = "전체경쟁업체";

    public static bool TryParse(string[] args, out ChartExportRequest? request)
    {
        request = null;
        if (args is null || args.Length == 0)
        {
            return false;
        }

        if (!args.Any(x => string.Equals(x, "--export-chart", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var map = ParseArgMap(args);

        var input = GetValue(map, "input", "/mnt/a/c/전체_통합_데이터_경북.xlsx");
        var output = GetValue(map, "output", "/mnt/a/c/경북_시각화_csharp.png");
        var precision = GetValue(map, "precision", "0.01");
        if (precision != "0.001")
        {
            precision = "0.01";
        }

        var minRate = ParseDouble(GetValue(map, "min-rate", "99.0"), 99.0);
        var maxRate = ParseDouble(GetValue(map, "max-rate", "101.0"), 101.0);
        if (minRate > maxRate)
        {
            (minRate, maxRate) = (maxRate, minRate);
        }

        var width = ParseInt(GetValue(map, "width", precision == "0.001" ? "10000" : "3600"), precision == "0.001" ? 10000 : 3600);
        var height = ParseInt(GetValue(map, "height", "2200"), 2200);
        width = Math.Clamp(width, 800, 20000);
        height = Math.Clamp(height, 600, 10000);

        var company = GetValue(map, "company", AllCompanyOption);

        request = new ChartExportRequest(
            InputPath: input,
            OutputPath: output,
            Precision: precision,
            MinRate: minRate,
            MaxRate: maxRate,
            Width: width,
            Height: height,
            Company: company);
        return true;
    }

    public static void Export(ChartExportRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var input = Path.GetFullPath(request.InputPath);
        if (!File.Exists(input))
        {
            throw new FileNotFoundException($"입력 파일을 찾을 수 없습니다: {input}");
        }

        var participants = LoadParticipantsFromExcel(input);
        if (participants.Count == 0)
        {
            throw new InvalidOperationException($"시각화 가능한 데이터가 없습니다: {input}");
        }

        var output = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        var control = new NativeBidChartControl
        {
            Width = request.Width,
            Height = request.Height,
            Mode = NativeBidChartMode.Scatter,
            Precision = request.Precision,
            SelectedCompany = string.IsNullOrWhiteSpace(request.Company) ? AllCompanyOption : request.Company.Trim(),
            MinAdjustRate = request.MinRate.ToString("F5", CultureInfo.InvariantCulture),
            MaxAdjustRate = request.MaxRate.ToString("F5", CultureInfo.InvariantCulture),
            Participants = participants,
            HighPerformanceMode = false,
        };

        control.Measure(new Size(request.Width, request.Height));
        control.Arrange(new Rect(0, 0, request.Width, request.Height));
        control.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            request.Width,
            request.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(control);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(output);
        encoder.Save(stream);
    }

    private static Dictionary<string, string> ParseArgMap(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = token[2..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if ((i + 1) < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                map[key] = args[i + 1];
                i++;
            }
            else
            {
                map[key] = "true";
            }
        }

        return map;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double ParseDouble(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return double.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static IReadOnlyList<ParticipantRecord> LoadParticipantsFromExcel(string path)
    {
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return [];
        }

        var headerMap = BuildHeaderMap(worksheet);
        if (headerMap.Count == 0)
        {
            return [];
        }

        var rows = new List<ParticipantRecord>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var noticeNumber = ReadCellText(row, headerMap, "공고번호");
            if (string.IsNullOrWhiteSpace(noticeNumber))
            {
                continue;
            }

            var companyName = ReadCellText(row, headerMap, "업체명");
            var adjustRate = ReadCellDouble(row, headerMap, "기초대비사정률", "기초대비사정율");
            var openDate = ReadCellDateTime(row, headerMap, "개찰일시") ?? DateTime.MinValue;
            var bidDate = ReadCellDateTime(row, headerMap, "투찰일시");
            var rank = ReadCellInt(row, headerMap, "순위");

            rows.Add(new ParticipantRecord(
                NoticeNo: noticeNumber,
                NoticeNumber: noticeNumber,
                NoticeName: ReadCellText(row, headerMap, "공고명"),
                OrderingAgency: ReadCellText(row, headerMap, "발주처"),
                Region: ReadCellText(row, headerMap, "지역제한"),
                Rank: rank,
                CompanyName: companyName,
                NormalizedCompanyName: NormalizeCompanyName(companyName),
                OpenDate: openDate,
                BidDate: bidDate,
                BidAmount: ReadCellLong(row, headerMap, "투찰금액"),
                BidRateVsEstimate: ReadCellDouble(row, headerMap, "예가대비투찰률"),
                BidRateVsBase: ReadCellDouble(row, headerMap, "기초대비투찰률"),
                AdjustRate: adjustRate,
                EstimatePrice: ReadCellLong(row, headerMap, "예정가격"),
                LowerLimitPrice: ReadCellLong(row, headerMap, "낙찰하한가"),
                BaseAmount: ReadCellLong(row, headerMap, "기초금액"),
                PriceAdjustmentRate: ReadCellDouble(row, headerMap, "사정률"),
                LowerRate: ReadCellDouble(row, headerMap, "발주처투찰률"),
                LowerDiff: ReadCellLong(row, headerMap, "낙찰하한가차이"),
                BizNo: string.Empty,
                CeoName: string.Empty,
                Remark: string.Empty,
                IsAwarded: rank == 1));
        }

        return rows
            .Where(x => x.AdjustRate.HasValue)
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(1);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetValue<string>().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!map.ContainsKey(text))
            {
                map[text] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static string ReadCellText(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!headerMap.TryGetValue(column, out var colIndex))
            {
                continue;
            }

            return row.Cell(colIndex).GetValue<string>().Trim();
        }

        return string.Empty;
    }

    private static double? ReadCellDouble(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columns)
    {
        var text = ReadCellText(row, headerMap, columns);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = text.Replace(",", "", StringComparison.Ordinal).Replace("%", "", StringComparison.Ordinal).Trim();
        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return double.TryParse(cleaned, out parsed) ? parsed : null;
    }

    private static long? ReadCellLong(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columns)
    {
        var value = ReadCellDouble(row, headerMap, columns);
        return value.HasValue
            ? (long)Math.Round(value.Value, 0, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int ReadCellInt(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columns)
    {
        var value = ReadCellDouble(row, headerMap, columns);
        return value.HasValue
            ? (int)Math.Round(value.Value, 0, MidpointRounding.AwayFromZero)
            : -1;
    }

    private static DateTime? ReadCellDateTime(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!headerMap.TryGetValue(column, out var colIndex))
            {
                continue;
            }

            var cell = row.Cell(colIndex);
            if (cell.IsEmpty())
            {
                continue;
            }

            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime();
            }

            if (cell.TryGetValue<DateTime>(out var dateValue))
            {
                return dateValue;
            }

            var text = cell.GetValue<string>().Trim();
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string NormalizeCompanyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var output = name.Trim();
        var patterns = new[]
        {
            @"\(재\)", "재단법인", "재단",
            @"\(주\)", "주식회사",
            @"\(사\)", "사단법인",
            @"\(유\)", "유한회사",
            @"\(합\)", "합명회사",
            @"\(합자\)", "합자회사",
        };

        foreach (var pattern in patterns)
        {
            output = System.Text.RegularExpressions.Regex.Replace(output, pattern, string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        output = System.Text.RegularExpressions.Regex.Replace(output, "문화유산연구원", "문화재연구원", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        output = System.Text.RegularExpressions.Regex.Replace(output, "문화재보존센터", "문화재연구원", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        output = System.Text.RegularExpressions.Regex.Replace(output, "고고학연구소", "문화재연구원", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        output = System.Text.RegularExpressions.Regex.Replace(output, @"\s+", " ");
        return output.Trim();
    }
}
