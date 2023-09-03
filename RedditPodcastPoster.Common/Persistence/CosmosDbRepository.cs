using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class CosmosDbRepository : IDataRepository, ICosmosDbRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _cosmosDbSettings;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> cosmosDbSettings,
        ICosmosDbKeySelector cosmosDbKeySelector,
        ILogger<CosmosDbRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _cosmosDbSettings = cosmosDbSettings.Value;
        KeySelector = cosmosDbKeySelector;
        _logger = logger;
    }

    public IKeySelector KeySelector { get; }

    public async Task Write<T>(string partitionKey, T data)
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            await c.UpsertItemAsync(data, new PartitionKey(partitionKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error UpsertItemAsync on document with partition-partitionKey '{partitionKey}' in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }

    public async Task<T?> Read<T>(string key, string partitionKey) where T : class
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            return await c.ReadItemAsync<T>(key, new PartitionKey(partitionKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error ReadItemAsync on document with key '{key}', partition-partitionKey '{partitionKey}' in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }

    public IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            return c
                .GetItemLinqQueryable<T>()
                .ToFeedIterator()
                .ToAsyncEnumerable()
                .Where(x => x.IsOfType<T>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error GetItemLinqQueryable on documents in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }
}