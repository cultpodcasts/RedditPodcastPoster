using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext);
}