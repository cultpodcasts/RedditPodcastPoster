using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Legacy;

public interface ICosmosDbClientFactory
{
    CosmosClient Create();
}
