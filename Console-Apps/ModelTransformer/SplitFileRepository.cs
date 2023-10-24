using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace ModelTransformer;

public class SplitFileRepository : ISplitFileRepository
{
    private readonly IDataRepository _inputFileRepository;
    private readonly ILogger<SplitFileRepository> _logger;
    private readonly IDataRepository _outputFileRepository;

    public SplitFileRepository(
        IFileRepositoryFactory fileRepositoryFactory,
        ILogger<SplitFileRepository> logger)
    {
        _logger = logger;
        _inputFileRepository = fileRepositoryFactory.Create("input");
        _outputFileRepository = fileRepositoryFactory.Create("output");
    }

    public async Task Write<T>(string key, T data) where T : CosmosSelector
    {
        await _outputFileRepository.Write(data);
    }

    public async Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector
    {
        var result = await _inputFileRepository.Read<T>(key, partitionKey);
        return result;
    }

    public IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector
    {
        return _inputFileRepository.GetAll<T>(partitionKey);
    }
}