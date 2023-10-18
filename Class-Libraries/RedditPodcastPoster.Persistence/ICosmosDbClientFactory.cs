using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence;

public interface ICosmosDbClientFactory
{
    CosmosClient Create();
}