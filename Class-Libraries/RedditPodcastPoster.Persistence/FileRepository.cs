using System.Linq.Expressions;
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

    public async Task<T?> Read<T>(string fileKey) where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
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
            x.Substring(_container.Length, x.Length - (FileExtension.Length + _container.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item);
            if (cosmosSelector!.IsOfType<T>())
            {
                yield return cosmosSelector!;
            }
        }
    }

    public async IAsyncEnumerable<Guid> GetAllIds<T>() where T : CosmosSelector
    {
        var filenames = GetFilenames();

        var keys = filenames.Select(x =>
            x.Substring(_container.Length, x.Length - (FileExtension.Length + _container.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item);
            if (cosmosSelector != null)
            {
                yield return cosmosSelector.Id;
            }
        }
    }

    public async Task<T?> GetBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector
    {
        var items = await GetAll<T>().ToListAsync();
        var reduce = items.FirstOrDefault(selector.Compile());
        return reduce;
    }

    public IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<T, bool>> selector)
        where T : CosmosSelector
    {
        var items = GetAll<T>();
        var reduce = items.Where(selector.Compile());
        return reduce;
    }

    public IAsyncEnumerable<T2> GetAllBy<T, T2>(Expression<Func<T, bool>> selector,
        Expression<Func<T, T2>> expr) where T : CosmosSelector
    {
        var items = GetAll<T>();
        var reduce = items.Where(selector.Compile()).Select(expr.Compile());
        return reduce;
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