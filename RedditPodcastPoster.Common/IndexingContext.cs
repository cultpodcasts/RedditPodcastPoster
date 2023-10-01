namespace RedditPodcastPoster.Common;

public record IndexingContext(
    DateTime? ReleasedSince = null, 
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
}