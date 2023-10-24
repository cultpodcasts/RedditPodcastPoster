using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICosmosDbClientFactory
{
    CosmosClient Create();
}