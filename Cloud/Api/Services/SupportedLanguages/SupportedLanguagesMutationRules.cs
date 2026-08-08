using RedditPodcastPoster.Models.Languages;

namespace Api.Services.SupportedLanguages;

/// <summary>
/// Pure business rules for admin supported-language delta mutations (POST add / DELETE by code).
/// Add resolves the code from the culture English/native name; clients never invent codes.
/// </summary>
public static class SupportedLanguagesMutationRules
{
    public static SupportedLanguagesMutationValidationResult TryAdd(
        IReadOnlyList<SupportedLanguage> existing,
        string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return SupportedLanguagesMutationValidationResult.Fail(
                "Language name is required.");
        }

        if (!NeutralCultureLanguageLookup.TryResolveByEnglishName(trimmed, out var derivedCode, out var displayName))
        {
            return SupportedLanguagesMutationValidationResult.Fail(
                $"Unknown language name '{trimmed}'. The name must match a .NET neutral culture English or native name.");
        }

        if (existing.Any(l => l.Code.Equals(derivedCode, StringComparison.OrdinalIgnoreCase)))
        {
            var unchanged = Order(existing);
            return SupportedLanguagesMutationValidationResult.Ok(unchanged);
        }

        var next = existing
            .Select(l => new SupportedLanguage { Code = l.Code, Name = l.Name })
            .Append(new SupportedLanguage { Code = derivedCode, Name = displayName })
            .ToList();

        return SupportedLanguagesMutationValidationResult.Ok(Order(next));
    }

    public static SupportedLanguagesMutationValidationResult TryRemove(
        IReadOnlyList<SupportedLanguage> existing,
        string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return SupportedLanguagesMutationValidationResult.Fail(
                "Language code is required.");
        }

        var remaining = existing
            .Where(l => !l.Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .Select(l => new SupportedLanguage { Code = l.Code, Name = l.Name })
            .ToList();

        if (remaining.Count == existing.Count)
        {
            return SupportedLanguagesMutationValidationResult.Fail(
                $"Language code '{trimmed}' is not in the supported list.");
        }

        if (remaining.Count == 0)
        {
            return SupportedLanguagesMutationValidationResult.Fail(
                "Cannot remove the last supported language.");
        }

        return SupportedLanguagesMutationValidationResult.Ok(Order(remaining));
    }

    private static IReadOnlyList<SupportedLanguage> Order(IEnumerable<SupportedLanguage> languages) =>
        languages
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public sealed record SupportedLanguagesMutationValidationResult(
    bool IsValid,
    IReadOnlyList<SupportedLanguage> Languages,
    string? Error)
{
    public static SupportedLanguagesMutationValidationResult Ok(IReadOnlyList<SupportedLanguage> languages) =>
        new(true, languages, null);

    public static SupportedLanguagesMutationValidationResult Fail(string error) =>
        new(false, [], error);
}
