using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace ModelTransformer;

public class SplitFileRepository(
    IFileRepositoryFactory fileRepositoryFactory,
    ILogger<SplitFileRepository> logger)
    : ISplitFileRepository
{
    private readonly IDataRepository _inputFileRepository = fileRepositoryFactory.Create("input", false);
    private readonly IDataRepository _outputFileRepository = fileRepositoryFactory.Create("output", false);

    public async Task Write<T>(string key, T data) where T : CosmosSelector
    {
        await _outputFileRepository.Write(data);
    }

    public async Task<T?> Read<T>(string key) where T : CosmosSelector
    {
        var result = await _inputFileRepository.Read<T>(key);
        return result;
    }

    public IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector
    {
        return _inputFileRepository.GetAll<T>();
    }
}