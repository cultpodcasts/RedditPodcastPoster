using RedditPodcastPoster.Models.Languages;

namespace Api.Services.SupportedLanguages;

/// <summary>
/// Pure business rules for admin supported-language PUT payloads.
/// Codes are derived from .NET neutral culture names (EnglishName / NativeName / ASCII-fold).
/// Clients cannot invent codes or rename in place by keeping an old code.
/// Existing registered rows are not filtered or dropped here — the proposed list is what the client sends
/// (deletes are omissions the user made). Validation never removes a resolvable existing entry.
/// </summary>
public static class SupportedLanguagesUpdateRules
{
    public static SupportedLanguagesUpdateValidationResult ValidateAndBuild(
        IReadOnlyList<SupportedLanguageProposal> proposed)
    {
        if (proposed.Count == 0)
        {
            return SupportedLanguagesUpdateValidationResult.Fail(
                "languages must contain at least one entry.");
        }

        var languages = new List<SupportedLanguage>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknownNames = new List<string>();

        foreach (var entry in proposed)
        {
            var name = entry.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return SupportedLanguagesUpdateValidationResult.Fail(
                    "Each language must have a non-empty name.");
            }

            if (!NeutralCultureLanguageLookup.TryResolveByEnglishName(name, out var derivedCode, out var displayName))
            {
                unknownNames.Add(name);
                continue;
            }

            var providedCode = entry.Code?.Trim();
            if (!string.IsNullOrEmpty(providedCode) &&
                !providedCode.Equals(derivedCode, StringComparison.OrdinalIgnoreCase))
            {
                // Covers inventing a code and in-place "edits" that keep an old code while changing the name.
                return SupportedLanguagesUpdateValidationResult.Fail(
                    $"Language code for '{name}' is derived as '{derivedCode}' and cannot be set to '{providedCode}'.");
            }

            if (!seenCodes.Add(derivedCode))
            {
                continue;
            }

            languages.Add(new SupportedLanguage
            {
                Code = derivedCode,
                Name = displayName
            });
        }

        if (unknownNames.Count > 0)
        {
            var listed = string.Join(", ", unknownNames.Select(n => $"'{n}'"));
            return SupportedLanguagesUpdateValidationResult.Fail(
                unknownNames.Count == 1
                    ? $"Unknown language name {listed}. The name must match a .NET neutral culture English or native name."
                    : $"Unknown language names: {listed}. Each name must match a .NET neutral culture English or native name.");
        }

        if (languages.Count == 0)
        {
            return SupportedLanguagesUpdateValidationResult.Fail(
                "languages must contain at least one unique code.");
        }

        var ordered = languages
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return SupportedLanguagesUpdateValidationResult.Ok(ordered);
    }
}

public sealed record SupportedLanguageProposal(string? Code, string? Name);

public sealed record SupportedLanguagesUpdateValidationResult(
    bool IsValid,
    IReadOnlyList<SupportedLanguage> Languages,
    string? Error)
{
    public static SupportedLanguagesUpdateValidationResult Ok(IReadOnlyList<SupportedLanguage> languages) =>
        new(true, languages, null);

    public static SupportedLanguagesUpdateValidationResult Fail(string error) =>
        new(false, [], error);
}
