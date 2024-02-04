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

    public IAsyncEnumerable<Guid> GetAllIds<T>() where T : CosmosSelector
    {
        try
        {
            var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
            var query = "SELECT VALUE root.id FROM root";
            var feedIterator = container.GetItemQueryIterator<Guid>(query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                });
            return feedIterator.ToAsyncEnumerable();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetAll)}: Error retrieving all-documents.");
            throw;
        }
    }

    public async Task<T?> Read<T>(string key) where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
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

    public async Task<T?> GetBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();

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

    public async Task<IEnumerable<T>> GetAllBy<T>(Expression<Func<T, bool>> selector)
        where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
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

    public async Task<IEnumerable<T2>> GetAllBy<T, T2>(Expression<Func<T, bool>> selector, Expression<Func<T, T2>> expr)
        where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
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

    public IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector
    {
        try
        {
            var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
            var feedIterator = container.GetItemQueryIterator<T>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey)
            });
            return feedIterator.ToAsyncEnumerable();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetAll)}: Error retrieving all-documents.");
            throw;
        }
    }
}