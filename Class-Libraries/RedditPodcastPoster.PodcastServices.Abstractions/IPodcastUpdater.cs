using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, IndexingContext indexingContext);
}