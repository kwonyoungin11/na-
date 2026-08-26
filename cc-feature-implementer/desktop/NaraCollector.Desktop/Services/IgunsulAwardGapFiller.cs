using System.IO;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public static class IgunsulAwardGapFiller
{
    public static IReadOnlyList<ParticipantRecord> FillFromCache(
        IReadOnlyList<string> missingNoticeNumbers,
        string cacheDirectory)
    {
        if (missingNoticeNumbers.Count == 0
            || string.IsNullOrWhiteSpace(cacheDirectory)
            || !Directory.Exists(cacheDirectory))
        {
            return [];
        }

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var notice in missingNoticeNumbers)
        {
            if (string.IsNullOrWhiteSpace(notice))
            {
                continue;
            }

            missing.Add(notice.Trim());
            missing.Add(CollectorNoticeRules.StripNoticeSuffix(notice));
        }

        return IgunsulExcelConverter.ConvertDirectory(cacheDirectory)
            .Where(x => missing.Contains(x.NoticeNumber) || missing.Contains(x.NoticeNo))
            .ToList();
    }
}
