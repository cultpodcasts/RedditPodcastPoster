using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, IndexingContext indexingContext);
}