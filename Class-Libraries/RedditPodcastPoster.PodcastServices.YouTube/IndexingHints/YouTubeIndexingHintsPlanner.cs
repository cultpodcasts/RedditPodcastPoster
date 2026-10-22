using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;

public enum YouTubePlaylistDiscoveryPlan
{
    /// <summary>Walk the whole playlist (the long-standing path).</summary>
    FullWalk,

    /// <summary>Check the channel's newest uploads against the playlist instead of walking it.</summary>
    UploadsHead
}

/// <summary>
/// Pure rules for the YouTube indexing hints: when a pass may check the uploads head instead of walking the playlist,
/// and how the hints change after either path. See docs/catalogue-pagination.md, "YouTube indexing hints".
/// </summary>
public static class YouTubeIndexingHintsPlanner
{
    public const string NoHints = "no-hints";
    public const string PlaylistChanged = "playlist-changed";
    public const string NoPlaylistCount = "no-playlist-count";
    public const string BelowThreshold = "below-threshold";
    public const string FullWalkDue = "full-walk-due";
    public const string NoWindow = "no-window";
    public const string SkipYouTube = "skip-youtube";
    public const string NotOptedIn = "not-opted-in";
    public const string HintsUsable = "hints-usable";

    /// <summary>
    /// Uploads-head only when every condition holds: YouTube is not skipped, the caller opted in
    /// (<see cref="IndexingContext.UseYouTubeIndexingHints"/>, the hourly indexer only), hints exist for the current
    /// playlist, the stored count is above <see cref="YouTubeIndexingHintsOptions.UploadsHeadMinimumPlaylistItems"/>,
    /// the last full walk is younger than <see cref="YouTubeIndexingHintsOptions.FullWalkInterval"/> less
    /// <see cref="YouTubeIndexingHintsOptions.FullWalkSlack"/>, and the pass has a date window. Otherwise a full walk
    /// (with the reason).
    /// </summary>
    public static (YouTubePlaylistDiscoveryPlan Plan, string Reason) Plan(
        IYouTubeIndexingSettings settings,
        IndexingContext indexingContext,
        YouTubeIndexingHintsOptions options,
        DateTime utcNow)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, SkipYouTube);
        }

        if (!indexingContext.UseYouTubeIndexingHints)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, NotOptedIn);
        }

        if (settings.YouTubeIndexingHints == null)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, NoHints);
        }

        var hints = settings.CurrentYouTubeIndexingHints();
        if (hints == null)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, PlaylistChanged);
        }

        if (hints.PlaylistItemCount is not { } count)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, NoPlaylistCount);
        }

        if (count <= options.UploadsHeadMinimumPlaylistItems)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, BelowThreshold);
        }

        if (hints.LastFullWalk is not { } lastFullWalk || utcNow - lastFullWalk >= options.FullWalkDueAfter)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, FullWalkDue);
        }

        if (!indexingContext.ReleasedSince.HasValue)
        {
            return (YouTubePlaylistDiscoveryPlan.FullWalk, NoWindow);
        }

        return (YouTubePlaylistDiscoveryPlan.UploadsHead, HintsUsable);
    }

    /// <summary>
    /// Uploads at or before this have been accounted for: the later of the last full walk's start and the newest
    /// upload already checked. Null when there are no usable hints.
    /// </summary>
    public static DateTime? Watermark(YouTubeIndexingHints hints)
    {
        var walked = hints.LastFullWalk;
        var checkedThrough = hints.UploadsCheckedThrough;
        if (walked == null)
        {
            return checkedThrough;
        }

        return checkedThrough > walked ? checkedThrough : walked;
    }

    /// <summary>
    /// Hints after a full walk that started at <paramref name="walkStartedUtc"/>.
    /// <list type="bullet">
    /// <item>Complete and the playlist holds more than the threshold: record the walk (playlist, start time, count),
    /// keeping the uploads watermark when the hints were for this playlist.</item>
    /// <item>Complete but small (or no count), or a newest-first walk that stopped at the window: no hints (null),
    /// because a full walk is already cheap.</item>
    /// <item>Incomplete (failure, guard Error stop, arbitrary page cap): unchanged, so a partial walk never counts as
    /// a full one.</item>
    /// </list>
    /// </summary>
    public static YouTubeIndexingHints? AfterFullWalk(
        IYouTubeIndexingSettings settings,
        YouTubePlaylistWalkOutcome outcome,
        int? totalResults,
        DateTime walkStartedUtc,
        YouTubeIndexingHintsOptions options)
    {
        switch (outcome)
        {
            case YouTubePlaylistWalkOutcome.Incomplete:
                return settings.YouTubeIndexingHints;
            case YouTubePlaylistWalkOutcome.StoppedAtWindow:
                return null;
        }

        if (totalResults is not { } total || total <= options.UploadsHeadMinimumPlaylistItems ||
            string.IsNullOrWhiteSpace(settings.YouTubePlaylistId))
        {
            return null;
        }

        var previous = settings.CurrentYouTubeIndexingHints();
        return new YouTubeIndexingHints
        {
            PlaylistId = settings.YouTubePlaylistId,
            LastFullWalk = walkStartedUtc,
            PlaylistItemCount = total,
            UploadsCheckedThrough = previous?.UploadsCheckedThrough,
            UploadsCheckedThroughVideoId = previous?.UploadsCheckedThroughVideoId
        };
    }

    /// <summary>
    /// Hints after a successful uploads-head check: the new playlist count and, when newer uploads were checked, the
    /// watermark moved to the newest of them. The last full walk is unchanged.
    /// </summary>
    public static YouTubeIndexingHints AfterUploadsHead(YouTubeIndexingHints hints, YouTubeUploadsHeadResult result)
    {
        var updated = hints.Copy();
        updated.PlaylistItemCount = result.TotalResults;
        if (result.CheckedThrough.HasValue)
        {
            updated.UploadsCheckedThrough = result.CheckedThrough;
            updated.UploadsCheckedThroughVideoId = result.CheckedThroughVideoId;
        }

        return updated;
    }
}
