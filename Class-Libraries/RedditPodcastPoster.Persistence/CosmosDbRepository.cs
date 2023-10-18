using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbRepository : IDataRepository, ICosmosDbRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _cosmosDbSettings;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> cosmosDbSettings,
        ILogger<CosmosDbRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _cosmosDbSettings = cosmosDbSettings.Value;
        _logger = logger;
    }


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

    public async Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector
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

    public IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            return CosmosDbExtensions
                .ToAsyncEnumerable<T>(c
                    .GetItemLinqQueryable<T>()
                    .ToFeedIterator())
                .Where(x => x.IsOfType<T>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"{nameof(GetAll)}: Error documents in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }
    }

    public async Task<IEnumerable<Guid>> GetAllIds<T>(string key) where T : CosmosSelector
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
        try
        {
            var guids = new List<Guid>();
            var query = new QueryDefinition(
                $@"SELECT VALUE c.id FROM c WHERE c.type='{key}'");

            using var guidFeed = c.GetItemQueryIterator<Guid>(query);
            while (guidFeed.HasMoreResults)
            {
                var batch = await guidFeed.ReadNextAsync();
                guids.AddRange(batch);
            }

            return guids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"{nameof(GetAllIds)}: Error Ids of documents in Database with DatabaseId '{_cosmosDbSettings.DatabaseId}' and Container '{_cosmosDbSettings.Container}'.");
            throw;
        }

    }
}