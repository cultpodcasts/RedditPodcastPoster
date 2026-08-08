using System.Globalization;

namespace RedditPodcastPoster.Models.Languages;

/// <summary>
/// Resolves admin language names to ISO codes via .NET neutral <see cref="CultureInfo"/> data.
/// Codes come from <see cref="CultureInfo.TwoLetterISOLanguageName"/> (may be longer than 2, e.g. Filipino → fil).
/// </summary>
public static class NeutralCultureLanguageLookup
{
    private static readonly Lazy<IReadOnlyDictionary<string, CultureInfo>> CulturesByEnglishName = new(BuildMap);

    public static bool TryResolveByEnglishName(
        string? languageName,
        out string code,
        out string canonicalName)
    {
        code = string.Empty;
        canonicalName = string.Empty;

        if (string.IsNullOrWhiteSpace(languageName))
        {
            return false;
        }

        if (!CulturesByEnglishName.Value.TryGetValue(languageName.Trim(), out var culture))
        {
            return false;
        }

        code = culture.TwoLetterISOLanguageName;
        canonicalName = culture.EnglishName;
        return !string.IsNullOrEmpty(code);
    }

    private static IReadOnlyDictionary<string, CultureInfo> BuildMap()
    {
        return CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .GroupBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }
}
