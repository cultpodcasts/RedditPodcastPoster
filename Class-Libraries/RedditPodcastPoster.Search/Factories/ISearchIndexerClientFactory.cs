using Azure.Search.Documents.Indexes;

namespace RedditPodcastPoster.Search.Factories;

public interface ISearchIndexerClientFactory
{
    SearchIndexerClient Create();
}