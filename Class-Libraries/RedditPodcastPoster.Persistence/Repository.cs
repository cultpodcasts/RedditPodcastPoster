using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class Repository<T>(
    IDataRepository dataRepository,
    ILogger<Repository<CosmosSelector>> logger)
    : IRepository<T>
    where T : CosmosSelector
{
    private readonly ILogger<Repository<CosmosSelector>> _logger = logger;

    public async Task<IEnumerable<T>> GetAll(string partitionKey)
    {
        return await dataRepository.GetAll<T>(partitionKey).ToListAsync();
    }

    public async Task<T?> Get(string key, string partitionKey)
    {
        return await dataRepository.Read<T>(key, partitionKey);
    }

    public async Task Save(T data)
    {
        await dataRepository.Write(data);
    }
}