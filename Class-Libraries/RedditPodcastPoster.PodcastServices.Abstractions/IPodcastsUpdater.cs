namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastsUpdater
{
    Task<bool> UpdatePodcasts(Guid[] idsToIndex, IndexingContext indexingContext);
    Task<bool> UpdatePodcasts(IndexingContext indexingContext);
}