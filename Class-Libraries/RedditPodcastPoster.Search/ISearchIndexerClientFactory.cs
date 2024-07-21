using Azure.Search.Documents.Indexes;

namespace RedditPodcastPoster.Search;

public interface ISearchIndexerClientFactory
{
    SearchIndexerClient Create();
}