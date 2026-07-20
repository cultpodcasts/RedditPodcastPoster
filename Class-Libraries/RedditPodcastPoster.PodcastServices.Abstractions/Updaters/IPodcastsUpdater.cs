using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

public interface IPodcastsUpdater
{
    Task<bool> UpdatePodcasts(Guid[] idsToIndex, IndexingContext indexingContext);
    Task<bool> UpdatePodcasts(IndexingContext indexingContext);
}