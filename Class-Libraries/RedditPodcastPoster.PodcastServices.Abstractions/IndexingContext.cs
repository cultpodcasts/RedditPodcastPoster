namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool IndexSpotify = true,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveYouTubeQueries = true,
    bool SkipPodcastDiscovery = true,
    bool SkipExpensiveSpotifyQueries = true,
    bool SkipShortEpisodes = true)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
    public bool IndexSpotify { get; set; } = IndexSpotify;

    public override string ToString()
    {
        var releasedSince = ReleasedSince.HasValue
            ? $"released-since: '{ReleasedSince:dd/MM/yyyy HH:mm:ss}'"
            : "released-since: Null";
        var indexSpotify = $"index-spotify: {IndexSpotify}";
        var bypassSpotify = $"bypass-spotify: '{SkipSpotifyUrlResolving}'";
        var bypassYouTube = $"bypass-youtube: '{SkipYouTubeUrlResolving}'";
        var bypassExpensiveSpotify = $"bypass-expensive-spotify-queries: '{SkipExpensiveSpotifyQueries}'";
        var bypassExpensiveYouTube = $"bypass-expensive-youtube-queries: '{SkipExpensiveYouTubeQueries}'";
        var skipPodcastDiscovery = $"skip-podcast-discovery: '{SkipPodcastDiscovery}'";
        var skipShortEpisodes = $"skip-short-episodes: '{SkipShortEpisodes}'";
        return
            $"{nameof(IndexingContext)} Indexing with options {string.Join(", ", releasedSince, indexSpotify, bypassSpotify, bypassYouTube, bypassExpensiveSpotify, bypassExpensiveYouTube, skipPodcastDiscovery, skipShortEpisodes)}.";
    }
}