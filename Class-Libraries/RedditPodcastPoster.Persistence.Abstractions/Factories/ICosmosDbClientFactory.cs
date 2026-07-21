using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions.Factories;

public interface ICosmosDbClientFactory
{
    CosmosClient Create();
}
