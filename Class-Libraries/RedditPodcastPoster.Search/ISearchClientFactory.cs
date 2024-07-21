using Azure.Search.Documents;

namespace RedditPodcastPoster.Search;

public interface ISearchClientFactory
{
    SearchClient Create();
}