using Azure.Search.Documents;

namespace RedditPodcastPoster.Search.Factories;

public interface ISearchClientFactory
{
    SearchClient Create();
}