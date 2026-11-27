using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext);

    /// <summary>
    /// Dry run of <see cref="Update"/> for a full (non enrich-only) pass: discovers, applies the short-episode floor,
    /// merges against stored episodes and drops catalogue-owned URLs, then counts what would be added. Saves nothing
    /// (no episode, no podcast, no hero promotion, no quota record).
    /// </summary>
    Task<IndexPreviewResult> Preview(Podcast podcast, IndexingContext indexingContext);
}
