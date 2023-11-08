using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class FileRepository : IFileRepository
{
    private const string FileExtension = ".json";
    private readonly string _container = ".\\";
    private readonly JsonSerializerOptions _jsonSerialiserOptions;
    private readonly ILogger<IFileRepository> _logger;

    public FileRepository(
        IJsonSerializerOptionsProvider jsonSerialiserOptionsProvider,
        string container,
        ILogger<IFileRepository> logger)
    {
        _jsonSerialiserOptions = jsonSerialiserOptionsProvider.GetJsonSerializerOptions();
        _jsonSerialiserOptions.WriteIndented = true;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(container))
        {
            if (!Directory.Exists(container))
            {
                try
                {
                    Directory.CreateDirectory(container);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not create storage for {nameof(FileRepository)} named '{container}'.");
                    throw;
                }
            }

            if (!container.EndsWith("\\"))
            {
                container += "\\";
            }

            _container = container;
        }
    }

    public async Task Write<T>(T data) where T : CosmosSelector
    {
        if (string.IsNullOrWhiteSpace(data.FileKey))
        {
            throw new ArgumentException($"{nameof(data)} with id '{data.Id}' has a null/empty file-key.");
        }

        var filePath = GetFilePath(data.FileKey);
        await using var createStream = File.Create(filePath);
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

    public async IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector
    {
        var filenames = GetFilenames();
        var keys = filenames.Select(x =>
            x.Substring(_container.Length, x.Length - (FileExtension.Length + _container.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item, partitionKey);
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