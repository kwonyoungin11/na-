using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class ParticipantSameDayClusterReducer
{
    public static IReadOnlyList<ParticipantRecord> Collapse(
        IReadOnlyList<ParticipantRecord> participants,
        double rateTolerance,
        int minClusterSize)
    {
        if (participants.Count == 0)
        {
            return participants;
        }

        var retained = new List<(int Index, ParticipantRecord Record)>();
        var indexed = participants
            .Select((record, index) => new IndexedParticipant(index, record))
            .ToList();

        var validGroups = indexed
            .Where(x => x.Record.AdjustRate.HasValue)
            .Where(x => !string.IsNullOrWhiteSpace(ResolveCompanyKey(x.Record)))
            .GroupBy(x => new SameDayGroupKey(ResolveCompanyKey(x.Record), ResolveGroupDate(x.Record)))
            .ToList();

        foreach (var group in validGroups)
        {
            var ordered = group
                .OrderBy(x => x.Record.AdjustRate!.Value)
                .ThenBy(x => x.Index)
                .ToList();

            var currentCluster = new List<IndexedParticipant>();
            double? currentLastRate = null;

            foreach (var item in ordered)
            {
                var rate = item.Record.AdjustRate!.Value;
                if (currentCluster.Count == 0)
                {
                    currentCluster.Add(item);
                    currentLastRate = rate;
                    continue;
                }

                if (currentLastRate.HasValue && rate - currentLastRate.Value <= rateTolerance)
                {
                    currentCluster.Add(item);
                    currentLastRate = rate;
                    continue;
                }

                AppendCluster(retained, currentCluster, minClusterSize);
                currentCluster = [item];
                currentLastRate = rate;
            }

            if (currentCluster.Count > 0)
            {
                AppendCluster(retained, currentCluster, minClusterSize);
            }
        }

        foreach (var item in indexed.Where(x => !x.Record.AdjustRate.HasValue || string.IsNullOrWhiteSpace(ResolveCompanyKey(x.Record))))
        {
            retained.Add((item.Index, item.Record));
        }

        return retained
            .OrderBy(x => x.Index)
            .Select(x => x.Record)
            .ToList();
    }

    private static void AppendCluster(
        List<(int Index, ParticipantRecord Record)> retained,
        List<IndexedParticipant> cluster,
        int minClusterSize)
    {
        if (cluster.Count < Math.Max(1, minClusterSize))
        {
            retained.AddRange(cluster.Select(x => (x.Index, x.Record)));
            return;
        }

        var representative = cluster
            .OrderBy(x => x.Index)
            .First();
        var medianRate = cluster
            .Select(x => x.Record.AdjustRate!.Value)
            .OrderBy(x => x)
            .ToArray();
        var center = medianRate.Length / 2;
        var representativeRate = medianRate.Length % 2 == 0
            ? (medianRate[center - 1] + medianRate[center]) / 2.0
            : medianRate[center];

        retained.Add((representative.Index, representative.Record with { AdjustRate = representativeRate }));
    }

    private static string ResolveCompanyKey(ParticipantRecord record)
    {
        return string.IsNullOrWhiteSpace(record.NormalizedCompanyName)
            ? (record.CompanyName ?? string.Empty).Trim()
            : record.NormalizedCompanyName.Trim();
    }

    private static DateOnly ResolveGroupDate(ParticipantRecord record)
    {
        var basis = record.OpenDate != default
            ? record.OpenDate
            : (record.BidDate ?? default);
        return DateOnly.FromDateTime(basis.Date);
    }

    private sealed record IndexedParticipant(int Index, ParticipantRecord Record);
    private sealed record SameDayGroupKey(string Company, DateOnly Day);
}
