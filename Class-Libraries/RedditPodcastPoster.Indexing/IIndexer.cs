using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Indexing;

public interface IIndexer
{
    Task Index(Guid podcastId, IndexingContext indexingContext);
    Task Index(string podcastName, IndexingContext indexingContext);
}