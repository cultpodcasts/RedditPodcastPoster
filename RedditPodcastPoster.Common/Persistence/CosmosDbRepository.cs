using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public async Task Write<T>(string key, T data)
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            await c.UpsertItemAsync(data, new PartitionKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error UpsertItemAsync on document with partition-key '{key}' in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }

    public async Task<T?> Read<T>(string key) where T : class
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            return await c.ReadItemAsync<T>(key, new PartitionKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error ReadItemAsync on document with partition-key '{key}' in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }

    public IAsyncEnumerable<T> GetAll<T>() where T : class
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            return c.GetItemLinqQueryable<T>().ToFeedIterator().ToAsyncEnumerable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error GetItemLinqQueryable on documents in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }
}