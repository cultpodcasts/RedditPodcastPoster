namespace RedditPodcastPoster.Common;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveQueries = true)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
    public bool SkipExpensiveQueries { get; set; } = SkipExpensiveQueries;
}