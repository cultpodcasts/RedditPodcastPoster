namespace RedditPodcastPoster.UrlSubmission.Languages;

/// <summary>How the caller answered an unsupported-language prompt. Unset means they have not answered.</summary>
public enum CatalogueLanguageChoice
{
    Unset = 0,
    Add,
    Continue,
    Cancel
}

public static class CatalogueLanguageChoices
{
    public static CatalogueLanguageChoice Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "add" => CatalogueLanguageChoice.Add,
            "continue" => CatalogueLanguageChoice.Continue,
            "cancel" => CatalogueLanguageChoice.Cancel,
            _ => CatalogueLanguageChoice.Unset
        };
}

public sealed record CatalogueLanguageField(string Key, string Label, string? Text);

public sealed record CatalogueLanguageTexts(IReadOnlyList<CatalogueLanguageField> Fields)
{
    public static CatalogueLanguageTexts From(
        string? parentTitle,
        string? parentDescription,
        string? childTitle,
        string? childDescription,
        string parentNoun,
        string childNoun) =>
        new([
            new CatalogueLanguageField("parentTitle", $"{parentNoun} title", parentTitle),
            new CatalogueLanguageField("parentDescription", $"{parentNoun} description", parentDescription),
            new CatalogueLanguageField("episodeTitle", $"{childNoun} title", childTitle),
            new CatalogueLanguageField("episodeDescription", $"{childNoun} description", childDescription)
        ]);
}

public sealed record CatalogueLanguageSignal(string Field, string Label, string Excerpt, string Reason);

public sealed record CatalogueLanguageDetection(
    string? Code,
    string? EnglishName,
    IReadOnlyList<CatalogueLanguageSignal> Signals);

public sealed record CatalogueLanguageHold(
    string Code,
    string Name,
    IReadOnlyList<CatalogueLanguageSignal> Signals,
    Guid? DiscoveryItemId = null);

/// <summary>
/// Outcome of the pre-create check. <see cref="Hold"/> means nothing may be written yet.
/// <see cref="LanguageCode"/> is set only when a new parent's default language should be stored.
/// </summary>
public sealed record CatalogueLanguageAdmission(
    bool Hold,
    string? LanguageCode,
    bool SetsDefaultLanguage,
    CatalogueLanguageHold? Prompt)
{
    public static CatalogueLanguageAdmission Proceed(string? languageCode = null, bool setsDefault = false) =>
        new(false, languageCode, setsDefault, null);

    public static CatalogueLanguageAdmission NeedsPrompt(CatalogueLanguageHold prompt) =>
        new(true, null, false, prompt);
}
