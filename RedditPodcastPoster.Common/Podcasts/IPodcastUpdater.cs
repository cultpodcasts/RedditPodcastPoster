using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, IndexingContext indexingContext);
}