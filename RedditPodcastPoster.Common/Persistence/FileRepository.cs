using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class FileRepository : IFileRepository
{
    private const string FileExtension = ".json";
    private readonly string _container = ".\\";
    private readonly JsonSerializerOptions _jsonSerialiserOptions;
    private readonly ILogger<IFileRepository> _logger;

    public FileRepository(
        JsonSerializerOptions jsonSerialiserOptions,
        IPartitionKeySelector partitionKeySelector,
        string container,
        ILogger<IFileRepository> logger)
    {
        _jsonSerialiserOptions = jsonSerialiserOptions;
        if (!string.IsNullOrWhiteSpace(container))
        {
            if (!container.EndsWith("\\"))
            {
                container += "\\";
            }

            _container = container;
        }

        PartitionKeySelector = partitionKeySelector;
        _logger = logger;
    }

    public IPartitionKeySelector PartitionKeySelector { get; }

    public async Task Write<T>(string fileKey, T data)
    {
        await using var createStream = File.Create(GetFilePath(fileKey));
        await JsonSerializer.SerializeAsync(createStream, data, _jsonSerialiserOptions);
    }

    public async Task<T?> Read<T>(string fileKey, string partitionKey) where T : CosmosSelector
    {
        var file = GetFilePath(fileKey);
        await using var readStream = File.OpenRead(file);
        var item = await JsonSerializer.DeserializeAsync<T>(readStream);
        if (item != null && item.ModelType.ToString() == partitionKey)
        {
            return item;
        }

        return null;
    }

    public async IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector
    {
        var filenames = GetFilenames();
        var keys = filenames.Select(x =>
            x.Substring(_container.Length + 1, x.Length - (FileExtension.Length + _container.Length + 1)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item, string.Empty);
            if (cosmosSelector!.IsOfType<T>())
            {
                yield return cosmosSelector!;
            }
        }
    }

    public async Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector
    {
        var guids = new List<Guid>();
        var filenames = GetFilenames();

        var keys = filenames.Select(x =>
            x.Substring(_container.Length, x.Length - (FileExtension.Length + _container.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item, partitionKey);
            if (cosmosSelector != null)
            {
                guids.Add(cosmosSelector.Id);
            }
        }

        return guids;
    }

    private string GetFilePath(string fileKey)
    {
        return $"{_container}{fileKey}{FileExtension}";
    }

    private string[] GetFilenames()
    {
        return Directory.GetFiles(_container, $"*{FileExtension}");
    }
}