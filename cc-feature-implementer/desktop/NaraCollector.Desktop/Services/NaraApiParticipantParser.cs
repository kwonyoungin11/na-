using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed class NaraApiParticipantParser
{
    public List<ParticipantRecord> LoadParticipantsFromMasterExcel(
        string masterPath,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(masterPath);
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

        var start = filter.StartDate.Date;
        var endExclusive = filter.EndDate.Date.AddDays(1);

        var rows = new List<ParticipantRecord>();
        var index = 0;
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            if (index % 1000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var noticeNumber = ReadCellText(row, headerMap, "공고번호", "입찰공고번호", "공고번호(원본)");
            if (string.IsNullOrWhiteSpace(noticeNumber))
            {
                continue;
            }

            var openDate = ReadCellDateTime(row, headerMap, "개찰일시", "개찰일자") ?? DateTime.MinValue;
            if (openDate == DateTime.MinValue)
            {
                continue;
            }

            if (openDate < start || openDate >= endExclusive)
            {
                continue;
            }

            var companyName = ReadCellText(row, headerMap, "업체명", "업체명(원본)");
            var normalizedCompanyName = NormalizeCompanyName(companyName);
            if (IsRetiredCompany(normalizedCompanyName, companyName))
            {
                continue;
            }

            var rank = ReadCellInt(row, headerMap, "순위", "낙찰순위");
            var isAwarded = rank == 1;
            var region = NormalizeRegionValue(ReadCellText(row, headerMap, "지역제한", "지역"));

            rows.Add(
                new ParticipantRecord(
                    NoticeNo: ExtractNoticeNo(noticeNumber),
                    NoticeNumber: noticeNumber.Trim(),
                    NoticeName: ReadCellText(row, headerMap, "공고명", "공사명", "입찰공고명"),
                    OrderingAgency: ReadCellText(row, headerMap, "발주처", "수요기관"),
                    Region: region,
                    Rank: rank,
                    CompanyName: companyName,
                    NormalizedCompanyName: normalizedCompanyName,
                    OpenDate: openDate,
                    BidDate: ReadCellDateTime(row, headerMap, "투찰일시", "입찰일시"),
                    BidAmount: ReadCellLong(row, headerMap, "투찰금액"),
                    BidRateVsEstimate: ReadCellDouble(row, headerMap, "예가대비투찰률", "예가대비투찰률(%)"),
                    BidRateVsBase: ReadCellDouble(row, headerMap, "기초대비투찰률", "기초대비투찰률(%)"),
                    AdjustRate: ReadCellDouble(row, headerMap, "기초대비사정률", "기초대비사정율", "기초대비사정률(%)"),
                    EstimatePrice: ReadCellLong(row, headerMap, "예정가격"),
                    LowerLimitPrice: ReadCellLong(row, headerMap, "낙찰하한가"),
                    BaseAmount: ReadCellLong(row, headerMap, "기초금액"),
                    PriceAdjustmentRate: ReadCellDouble(row, headerMap, "사정률"),
                    LowerRate: ReadCellDouble(row, headerMap, "발주처투찰률", "투찰률"),
                    LowerDiff: ReadCellLong(row, headerMap, "낙찰하한가차이"),
                    BizNo: string.Empty,
                    CeoName: string.Empty,
                    Remark: string.Empty,
                    IsAwarded: isAwarded));
        }

        return rows
            .GroupBy(BuildParticipantKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.BidDate ?? x.OpenDate)
                .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                .First())
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string NormalizeKeywords(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var tokens = raw
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tokens.Count == 0 ? string.Empty : string.Join(",", tokens);
    }

    internal static string NormalizeRegionValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("/", ",", StringComparison.Ordinal)
            .Replace(";", ",", StringComparison.Ordinal)
            .Trim();

        if (normalized.Contains("전국", StringComparison.Ordinal))
        {
            return "전국";
        }

        if (normalized.Contains("대구", StringComparison.Ordinal)
            && (normalized.Contains("경북", StringComparison.Ordinal)
                || normalized.Contains("경상북", StringComparison.Ordinal)))
        {
            return "대구/경북";
        }

        if (normalized.Contains("대구", StringComparison.Ordinal)
            && !normalized.Contains("경북", StringComparison.Ordinal)
            && !normalized.Contains("경상북", StringComparison.Ordinal))
        {
            return "대구";
        }

        if (normalized.Contains("경북", StringComparison.Ordinal)
            || normalized.Contains("경상북", StringComparison.Ordinal))
        {
            return "경북";
        }

        return value.Trim();
    }

    internal static string ExtractNoticeNo(string noticeNumber)
    {
        var text = noticeNumber.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var dashIndex = text.LastIndexOf("-", StringComparison.Ordinal);
        if (dashIndex <= 0)
        {
            return text;
        }

        var suffix = text[(dashIndex + 1)..];
        if (!suffix.All(char.IsDigit))
        {
            return text;
        }

        if (text.StartsWith("R", StringComparison.OrdinalIgnoreCase))
        {
            return text[..dashIndex];
        }

        return suffix.Length <= 3 ? text[..dashIndex] : text;
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

            var key = NormalizeHeader(text);
            if (!map.ContainsKey(key))
            {
                map[key] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static string ReadCellText(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnCandidates)
    {
        var columnIndex = ResolveColumnIndex(headerMap, columnCandidates);
        if (columnIndex is null)
        {
            return string.Empty;
        }

        return row.Cell(columnIndex.Value).GetValue<string>().Trim();
    }

    private static double? ReadCellDouble(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnCandidates)
    {
        var text = ReadCellText(row, headerMap, columnCandidates);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = text
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("%", "", StringComparison.Ordinal)
            .Trim();

        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ReadCellLong(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnCandidates)
    {
        var number = ReadCellDouble(row, headerMap, columnCandidates);
        return number.HasValue
            ? (long)Math.Round(number.Value, 0, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int ReadCellInt(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnCandidates)
    {
        var number = ReadCellDouble(row, headerMap, columnCandidates);
        return number.HasValue
            ? (int)Math.Round(number.Value, 0, MidpointRounding.AwayFromZero)
            : -1;
    }

    private static DateTime? ReadCellDateTime(IXLRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnCandidates)
    {
        var columnIndex = ResolveColumnIndex(headerMap, columnCandidates);
        if (columnIndex is null)
        {
            return null;
        }

        var cell = row.Cell(columnIndex.Value);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime();
        }

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return dateTime;
        }

        var text = cell.GetValue<string>().Trim();
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate))
        {
            try
            {
                return DateTime.FromOADate(oaDate);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static int? ResolveColumnIndex(IReadOnlyDictionary<string, int> headerMap, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var key = NormalizeHeader(candidate);
            if (headerMap.TryGetValue(key, out var idx))
            {
                return idx;
            }
        }

        return null;
    }

    private static string NormalizeHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Regex.Replace(text, @"[\s%()（）]+", string.Empty, RegexOptions.None).Trim();
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
            @"\(재\)", @"\(재단\)", "재단법인", "재단",
            @"\(주\)", "주식회사",
            @"\(사\)", "사단법인",
            @"\(유\)", "유한회사",
            @"\(합\)", "합명회사",
            @"\(합자\)", "합자회사",
        };

        foreach (var pattern in patterns)
        {
            output = Regex.Replace(output, pattern, string.Empty, RegexOptions.IgnoreCase);
        }

        output = Regex.Replace(output, "문화유산연구원", "문화재연구원", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, "문화재보존센터", "문화재연구원", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, "고고학연구소", "문화재연구원", RegexOptions.IgnoreCase);
        if (output.Contains("문화유산마을", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("문화재연구원", StringComparison.OrdinalIgnoreCase))
        {
            output = "문화유산마을 문화재연구원";
        }
        output = Regex.Replace(output, @"[()（）]+", " ");
        output = Regex.Replace(output, @"\s+", " ", RegexOptions.None);
        return output.Trim();
    }

    private static bool IsRetiredCompany(string? normalizedCompanyName, string? rawCompanyName)
    {
        var normalized = NormalizeToken(normalizedCompanyName);
        if (normalized.Contains("효림", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var raw = NormalizeToken(rawCompanyName);
        return raw.Contains("효림", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildParticipantKey(ParticipantRecord row)
    {
        var notice = NormalizeToken(GetNoticeKey(row));
        var company = NormalizeToken(GetCompanyOption(row));
        var amount = row.BidAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{notice}|{company}|{row.Rank}|{amount}";
    }

    private static string GetNoticeKey(ParticipantRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.NoticeNumber))
        {
            return row.NoticeNumber.Trim();
        }

        return row.NoticeNo?.Trim() ?? string.Empty;
    }

    private static string GetCompanyOption(ParticipantRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.NormalizedCompanyName))
        {
            return row.NormalizedCompanyName.Trim();
        }

        return row.CompanyName?.Trim() ?? string.Empty;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", string.Empty, RegexOptions.None).ToUpperInvariant();
    }
}
