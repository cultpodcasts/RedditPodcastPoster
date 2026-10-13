using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record IndexingContext(
    DateTime? ReleasedSince = null,
    bool IndexSpotify = true,
    bool SkipYouTubeUrlResolving = false,
    bool SkipSpotifyUrlResolving = false,
    bool SkipExpensiveYouTubeQueries = true,
    bool SkipPodcastDiscovery = true,
    bool SkipExpensiveSpotifyQueries = true,
    bool SkipShortEpisodes = true,
    bool YouTubeQuotaExhausted = false)
{
    public bool SkipYouTubeUrlResolving { get; set; } = SkipYouTubeUrlResolving;
    public bool SkipSpotifyUrlResolving { get; set; } = SkipSpotifyUrlResolving;
    public bool IndexSpotify { get; set; } = IndexSpotify;
    public bool YouTubeQuotaExhausted { get; set; } = YouTubeQuotaExhausted;

    /// <summary>
    /// Use-case for episode lookups (docs/catalogue-pagination.md "Episode lookup planner"). Defaults to Submit;
    /// PodcastUpdater sets Index, the Enrich CLI sets Enrich, discovery enrichers set Discovery.
    /// </summary>
    public EpisodeLookupFlow LookupFlow { get; set; } = EpisodeLookupFlow.Submit;

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
        var lookupFlow = $"lookup-flow: '{LookupFlow}'";
        return
            $"{nameof(IndexingContext)} Indexing with options {string.Join(", ", releasedSince, indexSpotify, bypassSpotify, bypassYouTube, bypassExpensiveSpotify, bypassExpensiveYouTube, skipPodcastDiscovery, skipShortEpisodes, lookupFlow)}.";
    }
}