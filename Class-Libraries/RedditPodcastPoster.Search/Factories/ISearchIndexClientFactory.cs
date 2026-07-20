using Azure.Search.Documents.Indexes;

namespace RedditPodcastPoster.Search.Factories;

public interface ISearchIndexClientFactory
{
    SearchIndexClient Create();
}