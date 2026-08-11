namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <summary>
/// Shared Apple catalogue walk rules: newest-first probe and circuit-breaker caps.
/// Equal release timestamps still count as newest-first (non-increasing), matching Spotify's probe.
/// </summary>
public static class AppleCataloguePagination
{
    /// <summary>
    /// Cap subsequent page fetches when the catalogue is not newest-first and a
    /// <c>ReleasedSince</c> window is set. Without this, equal-or-ascending heads used to
    /// disable ReleasedSince early-stop and walk entire high-volume shows.
    /// </summary>
    public const int MaxPages = 20;

    public const string CircuitBreakerTrippedMessagePrefix = "Apple pagination circuit-breaker tripped:";

    public const string CircuitBreakerTrippedMessageTemplate =
        CircuitBreakerTrippedMessagePrefix +
        " pages-fetched='{PagesFetched}' max-pages='{MaxPages}' released-since='{ReleasedSince}' next='{Next}' newest-first='false'. Stopped to protect Apple API quota; in-window episodes may be missing.";

    /// <summary>
    /// True when releases are monotonically non-increasing (newest-first, allowing equal dates).
    /// Fewer than two samples is treated as newest-first so ReleasedSince early-stop still applies.
    /// </summary>
    public static bool IsNewestFirst(IReadOnlyList<DateTime> releases)
    {
        if (releases.Count < 2)
        {
            return true;
        }

        var previous = releases[0];
        for (var i = 1; i < releases.Count; i++)
        {
            if (previous < releases[i])
            {
                return false;
            }

            previous = releases[i];
        }

        return true;
    }

    /// <summary>
    /// Whether to follow <paramref name="next"/> after the pages already collected.
    /// <paramref name="pagesFetchedAfterFirst"/> counts subsequent HTTP pages (not including page one).
    /// </summary>
    public static bool ShouldContinuePaging(
        bool hasNext,
        DateTime? releasedSince,
        DateTime lastCollectedRelease,
        bool newestFirst,
        int pagesFetchedAfterFirst)
    {
        if (!hasNext)
        {
            return false;
        }

        if (!releasedSince.HasValue)
        {
            // Full-catalogue callers (rare for MatchOtherServices) keep walking.
            return true;
        }

        if (newestFirst)
        {
            return lastCollectedRelease >= releasedSince.Value;
        }

        // Ascending / unordered with a date window: hard-cap like Spotify's forward crawl.
        return pagesFetchedAfterFirst < MaxPages;
    }
}
