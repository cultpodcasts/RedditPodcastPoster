using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     Whether an episode must be absent from Azure Search (delete / skip upload).
/// </summary>
public static class EpisodeSearchIndexEligibility
{
    /// <remarks>An episode held for curator approval (#1484) is excluded whatever its removed flag says.</remarks>
    public static bool ShouldExcludeFromSearch(Podcast podcast, Episode episode) =>
        episode.IsRemoved() || episode.IsHeldForApproval() || podcast.IsRemoved();
}
