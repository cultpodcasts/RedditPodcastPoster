namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Caps arbitrary-order (curated) YouTube playlist walks so a mis-tagged news-channel-scale
/// playlist cannot burn the YouTube quota. Tripping logs at Error via
/// <see cref="CircuitBreakerTrippedMessagePrefix"/> because in-window episodes may be missing.
/// At <see cref="BatchSize"/> items per page, <see cref="MaxPages"/> covers ~1000 playlist items —
/// enough for curated show playlists, deliberately too small for channel-scale uploads feeds.
/// </summary>
public static class ArbitraryYouTubePlaylistWalk
{
    /// <summary>
    /// Full-walk page size — the YouTube API maximum. playlistItems.list costs 1 quota unit per page.
    /// </summary>
    public const int BatchSize = 50;

    /// <summary>
    /// Maximum pages an arbitrary-order walk may fetch. Beyond this, stop and LogError so an
    /// operator can reclassify the playlist (or shrink it) rather than burn quota.
    /// </summary>
    public const int MaxPages = 20;

    public const string CircuitBreakerTrippedMessagePrefix =
        "YouTube arbitrary-playlist walk circuit-breaker tripped:";

    public const string CircuitBreakerTrippedMessageTemplate =
        CircuitBreakerTrippedMessagePrefix +
        " playlist-id='{PlaylistId}' pages-fetched='{PagesFetched}' max-pages='{MaxPages}' " +
        "released-since='{ReleasedSince}' next='{Next}'. Stopped to protect YouTube quota; " +
        "in-window episodes may be missing — reclassify or shrink this playlist.";

    /// <summary>
    /// True when another page remains after <paramref name="pagesFetched"/> fetches and the
    /// walk must stop. Call before requesting the next page.
    /// </summary>
    public static bool ShouldTripCircuitBreaker(int pagesFetched, string? nextPageToken) =>
        pagesFetched >= MaxPages && !string.IsNullOrEmpty(nextPageToken);
}
