namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveQueries = true,
    bool SkipPodcastDiscovery = true)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
    public bool SkipExpensiveQueries { get; set; } = SkipExpensiveQueries;
    public bool SkipPodcastDiscovery { get; set; } = SkipPodcastDiscovery;
}