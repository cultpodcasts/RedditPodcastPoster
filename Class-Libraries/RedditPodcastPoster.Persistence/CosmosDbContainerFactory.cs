using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    CosmosClient cosmosClient,
    IOptions<CosmosDbSettings> cosmosDbSettings,
    ILogger<CosmosDbContainerFactory> logger)
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

    public Container Create()
    {
        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
    }
}