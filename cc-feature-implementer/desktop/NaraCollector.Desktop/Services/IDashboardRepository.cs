using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public interface IDashboardRepository
{
    Task<DashboardSnapshot> GetSnapshotAsync(
        DashboardFilter filter,
        IProgress<LoadingProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false,
        DateTime? incrementalStartOverride = null);
}
