using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IDataRepository
{
    Task Write<T>(T data) where T : CosmosSelector;
    Task<T?> Read<T>(string key) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector;
    IAsyncEnumerable<T2> GetAll<T, T2>(Expression<Func<T, T2>> expr) where T : CosmosSelector;
    IAsyncEnumerable<Guid> GetAllIds<T>() where T : CosmosSelector;
    IAsyncEnumerable<string> GetAllFileKeys<T>() where T : CosmosSelector;
    Task<T?> GetBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector;
    Task<T2?> GetBy<T, T2>(Expression<Func<T, bool>> selector, Expression<Func<T, T2>> expr) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector;

    IAsyncEnumerable<T2> GetAllBy<T, T2>(Expression<Func<T, bool>> selector, Expression<Func<T, T2>> expr)
        where T : CosmosSelector;

    Task Delete<T>(T data) where T : CosmosSelector;
}