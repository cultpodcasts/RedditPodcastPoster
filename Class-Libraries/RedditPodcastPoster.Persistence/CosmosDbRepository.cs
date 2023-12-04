using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbRepository : ICosmosDbRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbRepository(
        Container container,
        ILogger<CosmosDbRepository> logger)
    {
        _container = container;
        _logger = logger;
    }


    public async Task Write<T>(T data) where T : CosmosSelector
    {
        try
        {
            await _container.UpsertItemAsync(data, new PartitionKey(data.GetPartitionKey()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error UpsertItemAsync on document with partition-partitionKey '{data.GetPartitionKey()}' in Database.");
            throw;
        }
    }

    public async Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector
    {
        try
        {
            return await _container.ReadItemAsync<T>(key, new PartitionKey(partitionKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error ReadItemAsync on document with key '{key}', partition-partitionKey '{partitionKey}'.");
            throw;
        }
    }

    public IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector
    {
        try
        {
            return _container
                .GetItemLinqQueryable<T>()
                .ToFeedIterator()
                .ToAsyncEnumerable()
                .Where(x => x.IsOfType<T>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"{nameof(GetAll)}: Error retrieving all-documents.");
            throw;
        }
    }

    public async Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector
    {
        try
        {
            var guids = new List<Guid>();
            var query = new QueryDefinition(
                $@"SELECT VALUE c.id FROM c WHERE c.type='{partitionKey}'");

            using var guidFeed = _container.GetItemQueryIterator<Guid>(query);
            while (guidFeed.HasMoreResults)
            {
                var batch = await guidFeed.ReadNextAsync();
                guids.AddRange(batch);
            }

            return guids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetAllIds)}: Error Getting-All-Ids of documents.");
            throw;
        }
    }
}