using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IDataRepository
{
    Task Write<T>(T data) where T : CosmosSelector;
    Task<T?> Read<T>(string key) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector;
    IAsyncEnumerable<Guid> GetAllIds<T>() where T : CosmosSelector;
    Task<T?> GetBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector;
    Task<IEnumerable<T>> GetAllBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector;
    Task<IEnumerable<T2>> GetAllBy<T, T2>(Expression<Func<T, bool>> selector, Expression<Func<T, T2>> expr)
        where T : CosmosSelector;
}