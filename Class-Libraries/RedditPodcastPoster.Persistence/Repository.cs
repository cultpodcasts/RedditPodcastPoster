using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence;

public class Repository<T> : IRepository<T> where T : CosmosSelector
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<Repository<CosmosSelector>> _logger;

    public Repository(
        IDataRepository dataRepository,
        ILogger<Repository<CosmosSelector>> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<T>> GetAll(string partitionKey)
    {
        return await _dataRepository.GetAll<T>(partitionKey).ToListAsync();
    }

    public async Task<T?> Get(string key, string partitionKey)
    {
        return await _dataRepository.Read<T>(key, partitionKey);
    }

    public async Task Save(T data)
    {
        await _dataRepository.Write(data);
    }
}