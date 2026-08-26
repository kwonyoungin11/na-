using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class IgunsulExcelConverter
{
    private static readonly Regex SignedParenRegex = new(
        @"(?<signed>[-+]?\d+(?:\.\d+)?)\s*%?\s*\(\s*(?<paren>[-+]?\d+(?:\.\d+)?)\s*%?\s*\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<ParticipantRecord> ConvertDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var rows = new List<ParticipantRecord>();
        foreach (var overviewPath in Directory.GetFiles(directory, "*_overview.xlsx"))
        {
            var participantsPath = overviewPath.Replace("_overview.xlsx", "_participants.xlsx", StringComparison.OrdinalIgnoreCase);
            if (!File.Exists(participantsPath))
            {
                continue;
            }

            rows.AddRange(Convert(overviewPath, participantsPath));
        }

        return rows
            .OrderBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ToList();
    }

    public static IReadOnlyList<ParticipantRecord> Convert(string overviewPath, string participantsPath)
    {
        var overview = LoadOverview(overviewPath);
        using var workbook = new XLWorkbook(participantsPath);
        var worksheet = workbook.Worksheets.First();
        var headerMap = BuildHeaderMap(worksheet);
        var rows = new List<ParticipantRecord>();

        foreach (var excelRow in worksheet.RowsUsed().Skip(1))
        {
            var companyName = ReadText(excelRow, headerMap, "업체명");
            if (string.IsNullOrWhiteSpace(companyName))
            {
                continue;
            }

            var rank = ReadInt(excelRow, headerMap, "순위", "낙찰순위");
            var signedAdjust = ReadDouble(excelRow, headerMap, "기초대비사정률", "기초대비사정률(%)", "기초대비사정율");
            rows.Add(
                new ParticipantRecord(
                    NoticeNo: NaraApiParticipantParser.ExtractNoticeNo(overview.NoticeNumber),
                    NoticeNumber: overview.NoticeNumber,
                    NoticeName: overview.NoticeName,
                    OrderingAgency: overview.OrderingAgency,
                    Region: NaraApiParticipantParser.NormalizeRegionValue(overview.Region),
                    Rank: rank,
                    CompanyName: companyName.Trim(),
                    NormalizedCompanyName: CompanyNamePresentation.Canonicalize(companyName),
                    OpenDate: overview.OpenDate,
                    BidDate: ReadDate(excelRow, headerMap, "투찰일시", "입찰일시"),
                    BidAmount: ReadLong(excelRow, headerMap, "투찰금액"),
                    BidRateVsEstimate: ReadDouble(excelRow, headerMap, "예가대비투찰률", "예가대비투찰률(%)"),
                    BidRateVsBase: ReadDouble(excelRow, headerMap, "기초대비투찰률", "기초대비투찰률(%)"),
                    AdjustRate: ToHundredScaleAdjustRate(signedAdjust),
                    EstimatePrice: overview.EstimatePrice,
                    LowerLimitPrice: overview.LowerLimitPrice,
                    BaseAmount: overview.BaseAmount,
                    PriceAdjustmentRate: overview.PriceAdjustmentRate,
                    LowerRate: overview.LowerRate,
                    LowerDiff: null,
                    BizNo: ReadText(excelRow, headerMap, "사업자등록번호"),
                    CeoName: string.Empty,
                    Remark: string.Empty,
                    IsAwarded: rank == 1));
        }

        return CollectorNoticeRules.ApplyPersistRules(
            rows.OrderBy(x => x.Rank > 0 ? x.Rank : int.MaxValue).ToList());
    }

    public static bool TryParseSignedParen(string? raw, out double signedPercent, out double hundredScale)
    {
        signedPercent = 0;
        hundredScale = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var match = SignedParenRegex.Match(raw);
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(match.Groups["signed"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out signedPercent))
        {
            return false;
        }

        return double.TryParse(match.Groups["paren"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out hundredScale);
    }

    public static double? ToHundredScaleAdjustRate(double? raw)
    {
        if (!raw.HasValue)
        {
            return null;
        }

        var value = raw.Value;
        if (Math.Abs(value) <= 10.0)
        {
            return Math.Round(100.0 + value, 5, MidpointRounding.AwayFromZero);
        }

        return value;
    }

    private static Overview LoadOverview(string overviewPath)
    {
        using var workbook = new XLWorkbook(overviewPath);
        var worksheet = workbook.Worksheets.First();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in worksheet.RowsUsed())
        {
            var label = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            map[label] = row.Cell(2).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(map[label]))
            {
                map[label] = row.Cell(2).GetString().Trim();
            }
        }

        var noticeNumber = ReadMap(map, "공고번호");
        var 사정률 = ReadMap(map, "사정률");
        TryParseSignedParen(사정률, out _, out var paren);
        if (paren == 0 && double.TryParse(ReadMap(map, "기초대비사정율", "기초대비사정률"), NumberStyles.Float, CultureInfo.InvariantCulture, out var labeled))
        {
            paren = labeled;
        }

        var openDate = ParseDate(ReadMap(map, "개찰일시")) ?? DateTime.MinValue;
        var lowerRateText = ReadMap(map, "투찰률");
        double? lowerRate = null;
        if (double.TryParse(lowerRateText.Replace("%", string.Empty, StringComparison.Ordinal).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLower))
        {
            lowerRate = parsedLower;
        }

        return new Overview(
            NoticeNumber: noticeNumber,
            NoticeName: ReadMap(map, "공고명", "공사명"),
            OrderingAgency: ReadMap(map, "발주처"),
            Region: ReadMap(map, "지역제한", "지역"),
            OpenDate: openDate,
            BaseAmount: ParseLong(ReadMap(map, "기초금액")),
            EstimatePrice: ParseLong(ReadMap(map, "예정가격")),
            LowerLimitPrice: ParseLong(ReadMap(map, "낙찰하한가")),
            PriceAdjustmentRate: paren == 0 ? null : paren,
            LowerRate: lowerRate);
    }

    private static string ReadMap(Dictionary<string, string> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            foreach (var pair in map)
            {
                if (pair.Key.Contains(key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    return pair.Value;
                }
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                map[text] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static string ReadText(IXLRow row, Dictionary<string, int> headerMap, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (headerMap.TryGetValue(key, out var col))
            {
                return row.Cell(col).GetFormattedString().Trim();
            }

            foreach (var pair in headerMap)
            {
                if (pair.Key.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return row.Cell(pair.Value).GetFormattedString().Trim();
                }
            }
        }

        return string.Empty;
    }

    private static int ReadInt(IXLRow row, Dictionary<string, int> headerMap, params string[] keys)
    {
        var text = ReadText(row, headerMap, keys);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static long? ReadLong(IXLRow row, Dictionary<string, int> headerMap, params string[] keys)
    {
        return ParseLong(ReadText(row, headerMap, keys));
    }

    private static double? ReadDouble(IXLRow row, Dictionary<string, int> headerMap, params string[] keys)
    {
        var text = ReadText(row, headerMap, keys).Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var current) ? current : null;
    }

    private static DateTime? ReadDate(IXLRow row, Dictionary<string, int> headerMap, params string[] keys)
    {
        return ParseDate(ReadText(row, headerMap, keys));
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim().Replace('/', '-');
        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
        {
            return invariant;
        }

        return DateTime.TryParse(text, out var current) ? current : null;
    }

    private static long? ParseLong(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = text.Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("원", string.Empty, StringComparison.Ordinal)
            .Trim();
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private sealed record Overview(
        string NoticeNumber,
        string NoticeName,
        string OrderingAgency,
        string Region,
        DateTime OpenDate,
        long? BaseAmount,
        long? EstimatePrice,
        long? LowerLimitPrice,
        double? PriceAdjustmentRate,
        double? LowerRate);
}
