namespace RedditPodcastPoster.Search;

public interface ISearchIndexerService
{
    Task<IndexerState> RunIndexer();
}