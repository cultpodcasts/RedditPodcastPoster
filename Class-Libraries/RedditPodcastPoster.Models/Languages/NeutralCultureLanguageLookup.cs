using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Models.Languages;

/// <summary>
/// Resolves admin language names to ISO codes via .NET neutral <see cref="CultureInfo"/> data.
/// Codes come from <see cref="CultureInfo.TwoLetterISOLanguageName"/> (may be longer than 2, e.g. Filipino → fil).
/// Matching uses EnglishName, NativeName, ASCII-fold, and a small alias table so ICU/.NET display-name
/// differences (e.g. Kiswahili vs Swahili) still resolve without dropping registered languages.
/// </summary>
public static class NeutralCultureLanguageLookup
{
    /// <summary>
    /// Alternate spellings seen in admin/R2 data or across Windows vs ICU EnglishName.
    /// Display is the spelling we keep when the alias is what matched (preserves Kiswahili in the register).
    /// </summary>
    private static readonly Dictionary<string, (string Iso, string Display)> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Kiswahili"] = ("sw", "Kiswahili"),
            ["Swahili"] = ("sw", "Swahili"),
            ["Maori"] = ("mi", "Māori")
        };

    private static readonly Lazy<CultureMaps> Maps = new(BuildMaps);

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

        var trimmed = languageName.Trim();
        var maps = Maps.Value;

        if (maps.ByEnglishName.TryGetValue(trimmed, out var culture))
        {
            return Set(culture.TwoLetterISOLanguageName, culture.EnglishName, out code, out canonicalName);
        }

        if (maps.ByNativeName.TryGetValue(trimmed, out culture))
        {
            // Prefer NativeName so hosts where EnglishName is "Swahili" still keep "Kiswahili".
            return Set(culture.TwoLetterISOLanguageName, culture.NativeName, out code, out canonicalName);
        }

        if (maps.ByFoldedName.TryGetValue(FoldKey(trimmed), out culture))
        {
            return Set(culture.TwoLetterISOLanguageName, culture.EnglishName, out code, out canonicalName);
        }

        if (Aliases.TryGetValue(trimmed, out var alias) &&
            maps.ByIso.TryGetValue(alias.Iso, out culture))
        {
            return Set(culture.TwoLetterISOLanguageName, alias.Display, out code, out canonicalName);
        }

        return false;
    }

    /// <summary>
    /// Neutral cultures for admin Add validation. Includes EnglishName and NativeName rows (same code)
    /// plus alias spellings so the client can resolve the same names as the server.
    /// </summary>
    public static IReadOnlyList<NeutralCultureLanguage> ListAll()
    {
        var maps = Maps.Value;
        var rows = new Dictionary<string, NeutralCultureLanguage>(StringComparer.OrdinalIgnoreCase);

        void Add(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            rows.TryAdd($"{code}\0{name}", new NeutralCultureLanguage(code, name));
        }

        foreach (var culture in maps.ByIso.Values)
        {
            Add(culture.TwoLetterISOLanguageName, culture.EnglishName);
            Add(culture.TwoLetterISOLanguageName, culture.NativeName);
        }

        foreach (var (alias, value) in Aliases)
        {
            Add(value.Iso, value.Display);
            Add(value.Iso, alias);
        }

        return rows.Values
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Set(string derivedCode, string displayName, out string code, out string canonicalName)
    {
        code = derivedCode;
        canonicalName = displayName;
        return !string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(canonicalName);
    }

    private static CultureMaps BuildMaps()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures).ToList();

        var byEnglishName = cultures
            .GroupBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var byNativeName = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in cultures)
        {
            if (string.IsNullOrWhiteSpace(culture.NativeName))
            {
                continue;
            }

            byNativeName.TryAdd(culture.NativeName.Trim(), culture);
        }

        var byIso = cultures
            .Where(c => !string.IsNullOrEmpty(c.TwoLetterISOLanguageName))
            .GroupBy(c => c.TwoLetterISOLanguageName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var byFolded = new Dictionary<string, CultureInfo>(StringComparer.Ordinal);
        foreach (var culture in cultures)
        {
            TryAddFold(byFolded, culture.EnglishName, culture);
            TryAddFold(byFolded, culture.NativeName, culture);
        }

        return new CultureMaps(byEnglishName, byNativeName, byFolded, byIso);
    }

    private static void TryAddFold(
        IDictionary<string, CultureInfo> byFolded,
        string? name,
        CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var folded = FoldKey(name);
        if (string.IsNullOrEmpty(folded))
        {
            return;
        }

        byFolded.TryAdd(folded, culture);
    }

    private static string FoldKey(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer.Append(char.ToLowerInvariant(ch));
        }

        return buffer.ToString();
    }

    private sealed record CultureMaps(
        IReadOnlyDictionary<string, CultureInfo> ByEnglishName,
        IReadOnlyDictionary<string, CultureInfo> ByNativeName,
        IReadOnlyDictionary<string, CultureInfo> ByFoldedName,
        IReadOnlyDictionary<string, CultureInfo> ByIso);
}

public sealed record NeutralCultureLanguage(string Code, string Name);
