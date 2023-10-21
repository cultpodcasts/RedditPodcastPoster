using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace TextClassifierTraining;

public class Repository<T> : IRepository<T> where T : CosmosSelector
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<Repository<CosmosSelector>> _logger;

    public Repository(IDataRepository dataRepository, ILogger<Repository<CosmosSelector>> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<T>> GetAll(string partitionKey)
    {
        return await _dataRepository.GetAll<T>(partitionKey).ToListAsync();
    }

    public async Task Save(string key, T data)
    {
        await _dataRepository.Write(key, data);
    }
}