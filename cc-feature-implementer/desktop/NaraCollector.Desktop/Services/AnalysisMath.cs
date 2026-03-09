namespace NaraCollector.Desktop.Services;

internal static class AnalysisMath
{
    public static double Quantile(IReadOnlyList<double> values, double probability)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var ordered = values.OrderBy(x => x).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var position = (ordered.Length - 1) * probability;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var weight = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * weight);
    }

    public static string ClassifyTertile(double value, IReadOnlyList<double> values, string lowLabel, string midLabel, string highLabel)
    {
        if (values.Count == 0)
        {
            return midLabel;
        }

        var q1 = Quantile(values, 0.33);
        var q2 = Quantile(values, 0.67);
        if (value <= q1)
        {
            return lowLabel;
        }

        if (value >= q2)
        {
            return highLabel;
        }

        return midLabel;
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var ordered = values.OrderBy(x => x).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    public static double PopulationStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    public static double SafeMean(IEnumerable<double> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0.0 : materialized.Average();
    }

    public static double RoundRate(double value) => Math.Round(value, 5);
}
