using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICosmosDbClientFactoryV2
{
    CosmosClient Create();
}
