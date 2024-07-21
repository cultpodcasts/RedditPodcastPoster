using Azure.Search.Documents.Indexes;

namespace RedditPodcastPoster.Search;

public interface ISearchIndexClientFactory
{
    SearchIndexClient Create();
}