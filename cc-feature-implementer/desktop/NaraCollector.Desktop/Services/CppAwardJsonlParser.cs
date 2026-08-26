using System.Globalization;
using System.IO;
using System.Text.Json;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class CppAwardJsonlParser
{
    public static IReadOnlyList<ParticipantRecord> Load(string jsonlPath)
    {
        if (string.IsNullOrWhiteSpace(jsonlPath) || !File.Exists(jsonlPath))
        {
            return [];
        }

        var rows = new List<ParticipantRecord>();
        foreach (var line in File.ReadLines(jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var notice = ReadString(root, "notice_number");
            var company = ReadString(root, "company_name");
            if (string.IsNullOrWhiteSpace(notice) || string.IsNullOrWhiteSpace(company))
            {
                continue;
            }

            var openText = ReadString(root, "open_date").Replace('/', '-');
            var openDate = DateTime.TryParse(openText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.MinValue;
            var rank = ReadInt(root, "rank");
            var rawRate = ReadDouble(root, "adjust_rate");
            double? chartRate = rawRate;
            if (rawRate is >= 80 and <= 95)
            {
                var lower = ReadDouble(root, "lower_rate");
                if (lower is > 50 and < 95)
                {
                    chartRate = ScatterChartRules.FromBidRateVsLowerLimit(rawRate.Value, lower.Value);
                }
                else
                {
                    chartRate = null;
                }
            }

            rows.Add(
                new ParticipantRecord(
                    NoticeNo: notice.Contains('-', StringComparison.Ordinal) ? notice[..notice.LastIndexOf('-')] : notice,
                    NoticeNumber: notice,
                    NoticeName: ReadString(root, "notice_name"),
                    OrderingAgency: ReadString(root, "ordering_agency"),
                    Region: string.IsNullOrWhiteSpace(ReadString(root, "region")) ? "전국" : ReadString(root, "region"),
                    Rank: rank,
                    CompanyName: company,
                    NormalizedCompanyName: CompanyNamePresentation.Canonicalize(company),
                    OpenDate: openDate,
                    BidDate: null,
                    BidAmount: null,
                    BidRateVsEstimate: null,
                    BidRateVsBase: rawRate is >= 80 and <= 95 ? rawRate : null,
                    AdjustRate: chartRate,
                    EstimatePrice: null,
                    LowerLimitPrice: null,
                    BaseAmount: null,
                    PriceAdjustmentRate: null,
                    LowerRate: ReadDouble(root, "lower_rate"),
                    LowerDiff: null,
                    BizNo: string.Empty,
                    CeoName: string.Empty,
                    Remark: string.Empty,
                    IsAwarded: rank == 1));
        }

        return rows;
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double? ReadDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
