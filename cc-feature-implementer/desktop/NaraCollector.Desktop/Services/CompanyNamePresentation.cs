using System.Text.RegularExpressions;

namespace NaraCollector.Desktop.Services;

public static class CompanyNamePresentation
{
    private static readonly string[] RemovalPatterns =
    [
        @"\(재\)", @"\(재단\)", "재단법인", "재단",
        @"\(주\)", "주식회사",
        @"\(사\)", "사단법인",
        @"\(유\)", "유한회사",
        @"\(합\)", "합명회사",
        @"\(합자\)", "합자회사",
    ];

    private static readonly string[] ShortSuffixes =
    [
        "문화재연구원",
        "문화유산연구원",
        "문화연구원",
        "문화유산원",
        "연구원",
        "고고학연구소",
        "고고문화연구원",
        "역사문화연구원",
    ];

    public static string Canonicalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var output = name.Trim();
        foreach (var pattern in RemovalPatterns)
        {
            output = Regex.Replace(output, pattern, string.Empty, RegexOptions.IgnoreCase);
        }

        var collapsed = Regex.Replace(output, @"\s+", "", RegexOptions.None);
        if (!collapsed.Contains("한국문화유산연구원", StringComparison.OrdinalIgnoreCase))
        {
            output = Regex.Replace(output, "문화유산연구원", "문화재연구원", RegexOptions.IgnoreCase);
        }
        output = Regex.Replace(output, @"[()（）]+", " ");
        output = Regex.Replace(output, @"\s+", " ", RegexOptions.None);
        return output.Trim();
    }

    public static string BuildShortLabel(string? name)
    {
        var compact = BuildCompactName(name);
        if (string.IsNullOrWhiteSpace(compact))
        {
            return string.Empty;
        }

        return compact.Length >= 2 ? compact[..2] : compact;
    }

    public static IReadOnlyDictionary<string, string> BuildDisplayMap(IEnumerable<string> names)
    {
        var canonicalNames = names
            .Select(Canonicalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compactMap = canonicalNames.ToDictionary(x => x, BuildCompactName, StringComparer.OrdinalIgnoreCase);
        var rawCompactMap = canonicalNames.ToDictionary(
            x => x,
            x => Canonicalize(x).Replace(" ", string.Empty, StringComparison.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in canonicalNames)
        {
            var compact = compactMap[name];
            var rawCompact = rawCompactMap[name];
            if (string.IsNullOrWhiteSpace(compact) || string.IsNullOrWhiteSpace(rawCompact))
            {
                result[name] = string.Empty;
                continue;
            }

            var uniquePrefixLength = Math.Min(2, rawCompact.Length);
            while (uniquePrefixLength <= rawCompact.Length)
            {
                var prefix = rawCompact[..uniquePrefixLength];
                var isUnique = rawCompactMap
                    .Where(pair => !string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    .All(pair => !pair.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (isUnique)
                {
                    break;
                }

                uniquePrefixLength++;
            }

            var uniquePrefix = rawCompact[..Math.Min(uniquePrefixLength, rawCompact.Length)];
            var label = BuildCompressedUniqueLabel(uniquePrefix);
            result[name] = string.IsNullOrWhiteSpace(label) ? BuildShortLabel(name) : label;
        }

        return result;
    }

    private static string BuildCompactName(string? name)
    {
        var canonical = Canonicalize(name);
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return string.Empty;
        }

        var compact = canonical.Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (var suffix in ShortSuffixes)
        {
            compact = compact.Replace(suffix, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (compact.Length == 0)
        {
            compact = canonical.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        var hangul = compact.Where(ch => ch is >= '\uAC00' and <= '\uD7A3').ToArray();
        return hangul.Length > 0 ? new string(hangul) : compact;
    }

    private static string BuildCompressedUniqueLabel(string uniquePrefix)
    {
        if (string.IsNullOrWhiteSpace(uniquePrefix))
        {
            return string.Empty;
        }

        if (uniquePrefix.Length <= 2)
        {
            return uniquePrefix;
        }

        if (uniquePrefix.Length == 3)
        {
            return $"{uniquePrefix[0]}{uniquePrefix[2]}";
        }

        return $"{uniquePrefix[0]}{uniquePrefix[2]}{uniquePrefix[^1]}";
    }
}
