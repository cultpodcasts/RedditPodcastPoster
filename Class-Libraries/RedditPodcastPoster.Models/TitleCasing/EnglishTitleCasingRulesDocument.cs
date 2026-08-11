namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>English title-casing rules — lower-case + known terms; no ignored subjects.</summary>
public sealed class EnglishTitleCasingRulesDocument : LanguageTitleCasingRulesDocument
{
    public EnglishTitleCasingRulesDocument() : base("en")
    {
    }
}
