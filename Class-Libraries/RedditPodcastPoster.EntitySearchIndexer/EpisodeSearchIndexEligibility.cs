using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     Whether an episode must be absent from Azure Search (delete / skip upload).
/// </summary>
public static class EpisodeSearchIndexEligibility
{
    public static bool ShouldExcludeFromSearch(Podcast podcast, Episode episode) =>
        episode.Removed || podcast.IsRemoved();
}
