using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>
/// Non-English language title-casing rules, including subject names that must not match
/// during enrichment for episodes in this language (e.g. Spanish <c>hoy</c> vs subject HOY).
/// </summary>
public sealed class NonEnglishTitleCasingRulesDocument : LanguageTitleCasingRulesDocument
{
    public NonEnglishTitleCasingRulesDocument()
    {
    }

    public NonEnglishTitleCasingRulesDocument(string language) : base(language)
    {
        if (IsUniversal(Language) ||
            string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Non-English title-casing rules require a non-English, non-universal language code.",
                nameof(language));
        }
    }

    /// <summary>Canonical subject names to skip during enrichment; <c>null</c> means none.</summary>
    [JsonPropertyName("ignoredSubjects")]
    [JsonPropertyOrder(40)]
    public string[]? IgnoredSubjects { get; set; }
}
