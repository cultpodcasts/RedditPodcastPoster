namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastsUpdater
{
    Task<bool> UpdatePodcasts(IndexingContext indexingContext);
}