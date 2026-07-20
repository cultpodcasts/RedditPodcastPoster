using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.Search.Services;

public interface ISearchIndexerService
{
    Task<IndexerStateWrapper> RunIndexer();
}