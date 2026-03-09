using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

internal sealed class NaraApiSnapshotBuilder
{
    public DashboardSnapshot BuildSnapshot(
        IReadOnlyList<ParticipantRecord> participants,
        DashboardFilter filter,
        string sourceLabel)
    {
        var noticeCount = participants
            .Select(GetNoticeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var awardCount = participants.Count(IsAwardRow);
        var cultureCount = participants.Count(IsCultureCompanyRow);
        var cultureAwardCount = participants.Count(x => IsCultureCompanyRow(x) && IsAwardRow(x));
        var avgAdjust = participants
            .Where(x => x.AdjustRate.HasValue)
            .Select(x => x.AdjustRate!.Value)
            .DefaultIfEmpty(0)
            .Average();
        var awardRate = participants.Count == 0
            ? 0
            : (double)awardCount / participants.Count * 100.0;

        var kpis = new List<KpiCardModel>
        {
            new("조회 공고 수", noticeCount.ToString("N0", CultureInfo.InvariantCulture), "건"),
            new("낙찰참여업체 수", participants.Count.ToString("N0", CultureInfo.InvariantCulture), "건"),
            new("낙찰데이터 수", awardCount.ToString("N0", CultureInfo.InvariantCulture), "건"),
            new("문화재연구원 참여건", cultureCount.ToString("N0", CultureInfo.InvariantCulture), "건"),
            new("문화재연구원 낙찰건", cultureAwardCount.ToString("N0", CultureInfo.InvariantCulture), "건"),
            new("평균 기초대비사정률", avgAdjust.ToString("F3", CultureInfo.InvariantCulture), "%"),
            new("낙찰률", awardRate.ToString("F2", CultureInfo.InvariantCulture), "%"),
            new("데이터구분", filter.DataType, "선택"),
            new("기간", $"{Math.Max(1, (filter.EndDate.Date - filter.StartDate.Date).Days + 1)}", "일"),
        };

        var noticeRows = participants
            .GroupBy(GetNoticeKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => g
                .OrderBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
                .ThenByDescending(x => x.OpenDate)
                .First())
            .OrderByDescending(x => x.OpenDate)
            .Take(300)
            .Select(x => new NoticeRowModel(
                NoticeNumber: x.NoticeNumber,
                NoticeName: x.NoticeName,
                OrderingAgency: x.OrderingAgency,
                OpenDate: x.OpenDate,
                AdjustRate: x.AdjustRate ?? 0,
                IsAwarded: IsAwardRow(x)))
            .ToList();

        var companyOptions = participants
            .Select(GetCompanyOption)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(800)
            .ToList();

        return new DashboardSnapshot(
            Kpis: kpis,
            NoticeRows: noticeRows,
            DataSource: sourceLabel,
            SavedFilePath: string.Empty,
            PositionMapPath: string.Empty,
            RecommendationPath: string.Empty,
            CompanyOptions: companyOptions,
            Participants: participants);
    }

    private static bool IsAwardRow(ParticipantRecord row)
    {
        return row.IsAwarded || row.Rank == 1;
    }

    private static bool IsCultureCompanyRow(ParticipantRecord row)
    {
        var company = GetCompanyOption(row);
        return company.Contains("동국문화재연구원", StringComparison.OrdinalIgnoreCase)
               || company.Contains("문화재연구원", StringComparison.OrdinalIgnoreCase)
               || company.Contains("문화유산연구원", StringComparison.OrdinalIgnoreCase);
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
}
