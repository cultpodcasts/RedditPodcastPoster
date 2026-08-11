namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>Universal (<c>*</c>) known terms — no lower-case terms, no ignored subjects.</summary>
public sealed class UniversalTitleCasingRulesDocument : TitleCasingRulesDocument
{
    public UniversalTitleCasingRulesDocument() : base(UniversalLanguageKey)
    {
    }
}
