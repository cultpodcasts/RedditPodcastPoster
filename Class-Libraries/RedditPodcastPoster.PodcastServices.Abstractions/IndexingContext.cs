namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveYouTubeQueries = true,
    bool SkipPodcastDiscovery = true,
    bool SkipExpensiveSpotifyQueries= true)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
    public bool SkipExpensiveYouTubeQueries { get; set; } = SkipExpensiveYouTubeQueries;
    public bool SkipPodcastDiscovery { get; set; } = SkipPodcastDiscovery;
    public bool SkipExpensiveSpotifyQueries { get; set; } = SkipExpensiveSpotifyQueries;
}