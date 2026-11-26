using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>
/// Indexing <i>policy</i>: value-equality flags copied with <c>with</c> per podcast and compared. Run-time state of a
/// single podcast's update (its budget token and stage reporter) is not policy and lives on
/// <see cref="PodcastRunScope"/> instead (#1073).
/// </summary>
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

    /// <summary>
    /// Lets YouTube discovery read a large playlist from the channel's uploads head when its stored indexing hints
    /// allow it (docs/catalogue-pagination.md "YouTube indexing hints"). Only the hourly Cloud indexer sets this:
    /// curator re-index (Api) and backfills (Index console app) always walk the playlist in full, so they can recover
    /// an in-window episode the uploads check would not offer again. A full walk still refreshes the hints.
    /// </summary>
    public bool UseYouTubeIndexingHints { get; init; }

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
        var useYouTubeIndexingHints = $"use-youtube-indexing-hints: '{UseYouTubeIndexingHints}'";
        return
            $"{nameof(IndexingContext)} Indexing with options {string.Join(", ", releasedSince, indexSpotify, bypassSpotify, bypassYouTube, bypassExpensiveSpotify, bypassExpensiveYouTube, skipPodcastDiscovery, skipShortEpisodes, lookupFlow, useYouTubeIndexingHints)}.";
    }
}