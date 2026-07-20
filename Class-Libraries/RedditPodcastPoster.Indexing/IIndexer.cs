using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Indexing;

public interface IIndexer
{
    Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext, bool forceIndex = false);
    Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext);
}
