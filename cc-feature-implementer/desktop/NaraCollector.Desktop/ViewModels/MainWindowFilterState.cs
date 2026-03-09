using System.Globalization;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.ViewModels;

internal sealed class MainWindowFilterState
{
    public DashboardFilter BuildFilter(
        DateTime startDate,
        DateTime endDate,
        string selectedRegion,
        string keywordText,
        string orderingAgencyText,
        string selectedCompany,
        string selectedMinAdjustRate,
        string selectedMaxAdjustRate)
    {
        var minAdjustRate = ParseAdjustRate(selectedMinAdjustRate);
        var maxAdjustRate = ParseAdjustRate(selectedMaxAdjustRate);
        if (minAdjustRate.HasValue && maxAdjustRate.HasValue && minAdjustRate.Value > maxAdjustRate.Value)
        {
            (minAdjustRate, maxAdjustRate) = (maxAdjustRate, minAdjustRate);
        }

        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date < normalizedStart ? normalizedStart : endDate.Date;

        return new DashboardFilter(
            normalizedStart,
            normalizedEnd,
            selectedRegion,
            keywordText.Trim(),
            orderingAgencyText.Trim(),
            "낙찰",
            selectedCompany.Trim(),
            minAdjustRate,
            maxAdjustRate);
    }

    public (string MinText, string MaxText, bool WasSwapped) EnsureValidAdjustRange(
        string selectedMinAdjustRate,
        string selectedMaxAdjustRate)
    {
        var minValue = ParseAdjustRate(selectedMinAdjustRate);
        var maxValue = ParseAdjustRate(selectedMaxAdjustRate);
        if (!minValue.HasValue || !maxValue.HasValue || minValue.Value <= maxValue.Value)
        {
            return (selectedMinAdjustRate, selectedMaxAdjustRate, false);
        }

        return (selectedMaxAdjustRate, selectedMinAdjustRate, true);
    }

    public double? ParseAdjustRate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var token = text.Trim();
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        return double.TryParse(token, out var currentCulture) ? currentCulture : null;
    }
}
