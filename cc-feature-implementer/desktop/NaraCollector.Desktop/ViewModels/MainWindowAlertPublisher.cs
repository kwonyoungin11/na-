using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.ViewModels;

internal sealed class MainWindowAlertPublisher
{
    private readonly HashSet<string> _donggukAlertKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _baselineInitialized;

    public IReadOnlyList<RankAlertModel> Publish(
        IReadOnlyList<ParticipantRecord> participants,
        Action<string>? appendEvent)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var latestRows = participants
            .Where(IsDonggukParticipant)
            .GroupBy(GetDonggukNoticeKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => g
                .OrderBy(x => x.Rank <= 0 ? int.MaxValue : x.Rank)
                .ThenByDescending(x => x.OpenDate)
                .First())
            .OrderByDescending(x => x.OpenDate)
            .Take(200)
            .ToList();

        if (latestRows.Count == 0)
        {
            return [];
        }

        if (!_baselineInitialized)
        {
            foreach (var row in latestRows)
            {
                _donggukAlertKeys.Add(BuildDonggukAlertKey(row));
            }

            _baselineInitialized = true;
            appendEvent?.Invoke($"동국문화재연구원 모니터링 기준선 설정: {latestRows.Count:N0}건");
            return [];
        }

        var newAlerts = new List<RankAlertModel>();
        foreach (var row in latestRows)
        {
            if (!_donggukAlertKeys.Add(BuildDonggukAlertKey(row)))
            {
                continue;
            }

            var companyName = string.IsNullOrWhiteSpace(row.NormalizedCompanyName)
                ? row.CompanyName
                : row.NormalizedCompanyName;
            newAlerts.Add(
                new RankAlertModel(
                    NoticeNumber: GetDonggukNoticeKey(row),
                    NoticeName: row.NoticeName,
                    OpenDate: row.OpenDate,
                    CompanyName: companyName,
                    Rank: row.Rank,
                    AdjustRate: row.AdjustRate,
                    Region: row.Region));
        }

        if (newAlerts.Count == 0)
        {
            return [];
        }

        foreach (var alert in newAlerts.Take(3))
        {
            appendEvent?.Invoke($"동국 순위 알림: {alert.OpenDate:MM-dd HH:mm} {alert.NoticeNumber} {alert.Rank}위");
        }

        if (newAlerts.Count > 3)
        {
            appendEvent?.Invoke($"동국 순위 알림: 추가 {newAlerts.Count - 3:N0}건");
        }

        return newAlerts;
    }

    private static bool IsDonggukParticipant(ParticipantRecord row)
    {
        var company = string.IsNullOrWhiteSpace(row.NormalizedCompanyName)
            ? row.CompanyName
            : row.NormalizedCompanyName;
        return !string.IsNullOrWhiteSpace(company)
               && company.Contains("동국문화재연구원", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDonggukNoticeKey(ParticipantRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.NoticeNumber))
        {
            return row.NoticeNumber.Trim();
        }

        return row.NoticeNo?.Trim() ?? string.Empty;
    }

    private static string BuildDonggukAlertKey(ParticipantRecord row)
    {
        var notice = GetDonggukNoticeKey(row);
        var company = string.IsNullOrWhiteSpace(row.NormalizedCompanyName)
            ? row.CompanyName
            : row.NormalizedCompanyName;
        var openDate = row.OpenDate == default ? string.Empty : row.OpenDate.ToString("yyyyMMddHHmmss");
        var amount = row.BidAmount?.ToString() ?? string.Empty;
        return $"{notice}|{openDate}|{company?.Trim()}|{row.Rank}|{amount}";
    }
}
