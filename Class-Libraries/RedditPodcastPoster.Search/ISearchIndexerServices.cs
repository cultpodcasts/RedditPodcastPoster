namespace RedditPodcastPoster.Search;

public interface ISearchIndexerService
{
    Task<IndexerStateWrapper> RunIndexer();
}