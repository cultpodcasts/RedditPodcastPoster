using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory : ICosmosDbContainerFactory
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _cosmosDbSettings;
    private readonly ILogger<CosmosDbContainerFactory> _logger;

    public CosmosDbContainerFactory(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> cosmosDbSettings,
        ILogger<CosmosDbContainerFactory> logger)
    {
        _cosmosClient = cosmosClient;
        _cosmosDbSettings = cosmosDbSettings.Value;
        _logger = logger;
    }

    public Container Create()
    {
        return _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
    }
}