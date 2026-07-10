namespace RedditPodcastPoster.People.Models;

public record GuestEnrichmentOptions(
    bool TitleOnly = true,
    int MinTermLength = 4,
    bool RequireTitleMatch = true,
    int? MinMatchCount = null)
{
    public static GuestEnrichmentOptions Default { get; } = new();
}
