using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Common.Persistence;

public interface ICosmosDbClientFactory
{
    CosmosClient Create();
}