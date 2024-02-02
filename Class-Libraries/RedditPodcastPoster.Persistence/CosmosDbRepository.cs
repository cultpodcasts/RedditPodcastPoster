using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbRepository(
    Container container,
    ILogger<CosmosDbRepository> logger)
    : ICosmosDbRepository
{
    public async Task Write<T>(T data) where T : CosmosSelector
    {
        try
        {
            await container.UpsertItemAsync(data, new PartitionKey(data.GetPartitionKey()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Error UpsertItemAsync on document with partition-partitionKey '{data.GetPartitionKey()}' in Database.");
            throw;
        }
    }

    public async Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector
    {
        try
        {
            return await container.ReadItemAsync<T>(key, new PartitionKey(partitionKey));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Error ReadItemAsync on document with key '{key}', partition-partitionKey '{partitionKey}'.");
            throw;
        }
    }

    public IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector
    {
        try
        {
            var modelType = CosmosSelectorExtensions.GetModelType<T>();
            var query = $"SELECT VALUE root FROM root WHERE (root[\"type\"] = \"{modelType}\")";
            var feedIterator = container.GetItemQueryIterator<T>(query);
            return feedIterator.ToAsyncEnumerable();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetAll)}: Error retrieving all-documents.");
            throw;
        }
    }

    public async Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector
    {
        try
        {
            var guids = new List<Guid>();
            var query = new QueryDefinition($@"SELECT VALUE c.id FROM c WHERE c.type='{partitionKey}'");

            using var guidFeed = container.GetItemQueryIterator<Guid>(query);
            while (guidFeed.HasMoreResults)
            {
                var batch = await guidFeed.ReadNextAsync();
                guids.AddRange(batch);
            }

            return guids;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetAllIds)}: Error Getting-All-Ids of documents.");
            throw;
        }
    }

    public async Task<T?> GetBy<T>(string partitionKey, Expression<Func<T, bool>> selector) where T : CosmosSelector
    {
        var query = container
            .GetItemLinqQueryable<T>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                })
            .Where(selector)
            .ToFeedIterator();
        if (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                {
                    return item;
                }
            }
        }

        return null;
    }

    public async Task<IEnumerable<T>> GetAllBy<T>(string partitionKey, Expression<Func<T, bool>> selector)
        where T : CosmosSelector
    {
        var results = new List<T>();
        var query = container
            .GetItemLinqQueryable<T>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                })
            .Where(selector)
            .ToFeedIterator();
        if (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }

    public async Task<IEnumerable<T2>> GetAllBy<T, T2>(
        string partitionKey, Expression<Func<T, bool>> selector,
        Expression<Func<T, T2>> expr)
        where T : CosmosSelector
    {
        var results = new List<T2>();
        var query = container
            .GetItemLinqQueryable<T>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                })
            .Where(selector)
            .Select(expr)
            .ToFeedIterator();
        if (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }
}