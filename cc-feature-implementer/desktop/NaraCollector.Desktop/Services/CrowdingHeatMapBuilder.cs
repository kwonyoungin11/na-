using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed record CrowdingHeatCell(
    int BandIndex,
    double BandStart,
    double BandEnd,
    int BidCount,
    int NoticeCount,
    int UniqueCompanyCount,
    string DensityTier,
    double Intensity);

public static class CrowdingHeatMapBuilder
{
    public static IReadOnlyList<CrowdingHeatCell> Build(
        IEnumerable<ParticipantRecord> participants,
        DateTime startDate,
        DateTime endDate,
        double minRate,
        double maxRate,
        double precision)
    {
        var safeMin = Math.Min(minRate, maxRate);
        var safeMax = Math.Max(minRate, maxRate);
        var step = precision > 0 ? precision : 0.01;
        var columnCount = Math.Max(1, (int)Math.Round((safeMax - safeMin) / step, MidpointRounding.AwayFromZero) + 1);
        var inPeriod = ScatterChartRules.InPeriod(
            (participants ?? Array.Empty<ParticipantRecord>()).ToList(),
            startDate,
            endDate);

        var buckets = Enumerable.Range(0, columnCount)
            .Select(_ => new List<ParticipantRecord>())
            .ToArray();

        foreach (var row in inPeriod)
        {
            if (!row.AdjustRate.HasValue)
            {
                continue;
            }

            var column = ToColumnIndex(row.AdjustRate.Value, safeMin, step, columnCount);
            buckets[column].Add(row);
        }

        var maxBids = Math.Max(1, buckets.Max(x => x.Count));
        var cells = new CrowdingHeatCell[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            var bids = buckets[i];
            var bidCount = bids.Count;
            var bandStart = Math.Round(safeMin + (i * step), 5, MidpointRounding.AwayFromZero);
            var bandEnd = Math.Round(bandStart + step, 5, MidpointRounding.AwayFromZero);
            var noticeCount = bids
                .Select(x => string.IsNullOrWhiteSpace(x.NoticeNumber) ? x.NoticeNo : x.NoticeNumber)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var uniqueCompanyCount = bids
                .Select(CompanyKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            cells[i] = new CrowdingHeatCell(
                BandIndex: i,
                BandStart: bandStart,
                BandEnd: bandEnd,
                BidCount: bidCount,
                NoticeCount: noticeCount,
                UniqueCompanyCount: uniqueCompanyCount,
                DensityTier: ClassifyTier(bidCount, maxBids),
                Intensity: bidCount / (double)maxBids);
        }

        return cells;
    }

    private static string ClassifyTier(int bidCount, int maxBids)
    {
        if (bidCount <= 0)
        {
            return "공백";
        }

        if (maxBids <= 1)
        {
            return "중간";
        }

        var hotFloor = Math.Max(2, (int)Math.Ceiling(maxBids * 0.67));
        return bidCount >= hotFloor ? "과열" : "중간";
    }

    private static string CompanyKey(ParticipantRecord row)
        => string.IsNullOrWhiteSpace(row.NormalizedCompanyName) ? row.CompanyName : row.NormalizedCompanyName;

    private static int ToColumnIndex(double rate, double minRate, double step, int columnCount)
    {
        var raw = (rate - minRate) / step;
        var col = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(col, 0, Math.Max(0, columnCount - 1));
    }
}
