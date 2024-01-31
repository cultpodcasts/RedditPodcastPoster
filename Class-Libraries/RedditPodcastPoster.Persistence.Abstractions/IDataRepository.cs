using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IDataRepository
{
    Task Write<T>(T data) where T : CosmosSelector;
    Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector;
    Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector;
    Task<T?> GetBy<T>(string partitionKey, Expression<Func<T, bool>> selector) where T : CosmosSelector;
    Task<IEnumerable<T>> GetAllBy<T>(string partitionKey, Expression<Func<T, bool>> selector) where T : CosmosSelector;

    Task<IEnumerable<T2>> GetAllBy<T, T2>(
        string partitionKey, Expression<Func<T, bool>> selector,
        Expression<Func<T, T2>> expr)
        where T : CosmosSelector;
}