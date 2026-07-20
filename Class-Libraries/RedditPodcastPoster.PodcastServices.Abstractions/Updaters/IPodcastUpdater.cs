using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

public interface IPodcastUpdater
{
    Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext);
}