namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>
/// Why a channel-videos or playlist fetch returned no result. <see cref="NotFound"/> and <see cref="ApiError"/>
/// are real fetch failures; <see cref="Skipped"/> and <see cref="QuotaExhausted"/> are not reported as fetch
/// failures because the lookup was bypassed or its quota failure was already logged. None of these members change
/// SkipYouTubeUrlResolving; quota exhaustion is tracked on IndexingContext.YouTubeQuotaExhausted.
/// </summary>
public enum YouTubePlaylistFetchFailure
{
    /// <summary>HTTP 404 - playlist deleted, private, or id wrong; update YouTubePlaylistId.</summary>
    NotFound,

    /// <summary>Other Google API / unexpected error while fetching playlist items.</summary>
    ApiError,

    /// <summary>No API call was made because SkipYouTubeUrlResolving is set for this pass; a non-error bypass.</summary>
    Skipped,

    /// <summary>
    /// Every api-key hit its quota before the lookup could run. The tolerant service already logged the error and
    /// set IndexingContext.YouTubeQuotaExhausted, so callers must not log a second fetch failure.
    /// </summary>
    QuotaExhausted
}
