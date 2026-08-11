using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>Per-language title-casing rules that include mid-title lower-case terms.</summary>
public abstract class LanguageTitleCasingRulesDocument : TitleCasingRulesDocument
{
    protected LanguageTitleCasingRulesDocument()
    {
    }

    protected LanguageTitleCasingRulesDocument(string language) : base(language)
    {
    }

    [JsonPropertyName("lowerCaseTerms")]
    [JsonPropertyOrder(20)]
    public List<string> LowerCaseTerms { get; set; } = [];
}
