using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Shared by every route that turns podcast episodes into news reports (the podcast kind transfer and Move to news,
/// #1388), so the same episode gets the same <see cref="Playable.Ignored"/> whichever route moved it.
/// </summary>
public static class CatalogueShortEpisodeIgnore
{
    /// <summary>
    /// True when the episode is ignored and podcast short-episode checking would have set that from length alone
    /// (same enforce/threshold as <c>PodcastUpdater</c>). A news parent forces <c>bypassShortEpisodeChecking</c>, so
    /// such an episode would never have been ignored there and its report is mapped with <c>ignored</c> cleared.
    /// Longer ignored episodes (manual / ignore-all / other) stay ignored. Short episodes that were ignore-all are
    /// indistinguishable from length-derived and are also cleared. Submit <c>EpisodeFactory</c> differs: when
    /// <c>BypassShortEpisodeChecking</c> is true it skips length-ignore entirely, even with an explicit
    /// <see cref="Podcast.MinimumDuration"/>.
    /// </summary>
    /// <param name="postingMinimumDuration">The global floor (<c>PostingCriteria.MinimumDuration</c>), used when the
    /// podcast has no <see cref="Podcast.MinimumDuration"/>.</param>
    public static bool IsLengthDerivedIgnored(Podcast podcast, Episode episode, TimeSpan postingMinimumDuration)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episode);
        if (!episode.Ignored)
        {
            return false;
        }

        // Explicit podcast MinimumDuration always applies. Bypass only skips the global posting-criteria floor.
        var bypassGlobalShortCheck = podcast.BypassShortEpisodeChecking == true;
        var enforceMinimumDuration = podcast.MinimumDuration.HasValue || !bypassGlobalShortCheck;
        if (!enforceMinimumDuration)
        {
            return false;
        }

        var threshold = podcast.MinimumDuration ?? postingMinimumDuration;
        return episode.Length < threshold;
    }
}