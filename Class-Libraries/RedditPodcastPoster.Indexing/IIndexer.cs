using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Indexing;

public interface IIndexer
{
    Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext);
    Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext);
}