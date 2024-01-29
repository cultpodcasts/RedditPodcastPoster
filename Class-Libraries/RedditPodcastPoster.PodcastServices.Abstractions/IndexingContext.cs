namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveYouTubeQueries = true,
    bool SkipPodcastDiscovery = true,
    bool SkipExpensiveSpotifyQueries = true,
    bool SkipShortEpisodes = true)
{
    public override string ToString()
    {
        var releasedSince = ReleasedSince.HasValue
            ? $"released-since: '{ReleasedSince:dd/MM/yyyy HH:mm:ss}'"
            : "released-since: Null";
        var bypassSpotify = $"bypass-spotify: '{SkipSpotifyUrlResolving}'";
        var bypassYouTube = $"bypass-youtube: '{SkipYouTubeUrlResolving}'";
        var bypassExpensiveSpotify = $"bypass-expensive-spotify-queries: '{SkipExpensiveSpotifyQueries}'";
        var bypassExpensiveYouTube = $"bypass-expensive-youtube-queries: '{SkipExpensiveYouTubeQueries}'";
        var skipPodcastDiscovery = $"skip-podcast-discovery: '{SkipPodcastDiscovery}'";
        var skipShortEpisodes = $"skip-short-episodes: '{SkipShortEpisodes}'";
        return
            $"{nameof(IndexingContext)} Indexing with options {string.Join(", ", releasedSince, bypassSpotify, bypassYouTube, bypassExpensiveSpotify, bypassExpensiveYouTube, skipPodcastDiscovery, skipShortEpisodes)}.";
    }
}