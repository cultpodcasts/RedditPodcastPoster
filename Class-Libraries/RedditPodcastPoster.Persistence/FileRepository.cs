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
    private readonly bool _useEntityFolder;

    public FileRepository(
        IJsonSerializerOptionsProvider jsonSerialiserOptionsProvider,
        string container,
        bool useEntityFolder,
        ILogger<IFileRepository> logger)
    {
        _jsonSerialiserOptions = jsonSerialiserOptionsProvider.GetJsonSerializerOptions();
        _jsonSerialiserOptions.WriteIndented = true;
        _useEntityFolder = useEntityFolder;
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

        var filePath = GetFilePath(data);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ??
                                  throw new InvalidOperationException($"Cannot create directory for '{filePath}'."));
        await using var createStream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(createStream, data, _jsonSerialiserOptions);
    }

    public async Task<T?> Read<T>(string fileKey) where T : CosmosSelector
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<T>().ToString();
        var filePath = GetFilePath<T>(fileKey);
        await using var readStream = File.OpenRead(filePath);
        var item = await JsonSerializer.DeserializeAsync<T>(readStream, _jsonSerialiserOptions);
        if (item != null && item.ModelType.ToString() == partitionKey)
        {
            return item;
        }

        return null;
    }

    public async IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector
    {
        var filenames = GetFilenames<T>();
        var prefix = $"{_container}{GetEntityFolder<T>()}";
        var keys = filenames.Select(x =>
            x.Substring(prefix.Length, x.Length - (FileExtension.Length + prefix.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<T>(item);
            if (cosmosSelector!.IsOfType())
            {
                yield return cosmosSelector!;
            }
        }
    }

    public IAsyncEnumerable<T2> GetAll<T, T2>(Expression<Func<T, T2>> expr) where T : CosmosSelector
    {
        var items = GetAll<T>();
        var reduce = items.Select(expr.Compile());
        return reduce;
    }

    public async IAsyncEnumerable<Guid> GetAllIds<T>() where T : CosmosSelector
    {
        var filenames = GetFilenames<T>();

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

    public async IAsyncEnumerable<string> GetAllFileKeys<T>() where T : CosmosSelector
    {
        var filenames = GetFilenames<T>();
        var keys = filenames.Select(x =>
            x.Substring(_container.Length, x.Length - (FileExtension.Length + _container.Length)));
        foreach (var item in keys)
        {
            var cosmosSelector = await Read<CosmosSelector>(item);
            if (cosmosSelector != null)
            {
                yield return cosmosSelector.FileKey;
            }
        }
    }


    public async Task<T?> GetBy<T>(Expression<Func<T, bool>> selector) where T : CosmosSelector
    {
        var items = await GetAll<T>().ToListAsync();
        var reduce = items.FirstOrDefault(selector.Compile());
        return reduce;
    }

    public async Task<T2?> GetBy<T, T2>(
        Expression<Func<T, bool>> selector,
        Expression<Func<T, T2>> expr) where T : CosmosSelector
    {
        var items = await GetAll<T>().ToListAsync();
        var reduce = items.Where(selector.Compile()).Select(expr.Compile());
        return reduce.FirstOrDefault();
    }


    public IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<T, bool>> selector)
        where T : CosmosSelector
    {
        var items = GetAll<T>();
        var reduce = items.Where(selector.Compile());
        return reduce;
    }

    public IAsyncEnumerable<T2> GetAllBy<T, T2>(
        Expression<Func<T, bool>> selector,
        Expression<Func<T, T2>> expr) where T : CosmosSelector
    {
        var items = GetAll<T>();
        var reduce = items.Where(selector.Compile()).Select(expr.Compile());
        return reduce;
    }

    public Task Delete<T>(T data) where T : CosmosSelector
    {
        var filePath = GetFilePath(data);
        File.Delete(filePath);
        return Task.CompletedTask;
    }

    public string GetFilePath<T>(T data) where T : CosmosSelector
    {
        return GetFilePath<T>(data.FileKey);
    }

    public string GetFilePath<T>(string fileKey) where T : CosmosSelector
    {
        return $"{_container}{GetEntityFolder<T>()}{fileKey}{FileExtension}";
    }

    private string GetEntityFolder<T>() where T : CosmosSelector
    {
        if (!_useEntityFolder)
        {
            return string.Empty;
        }

        return $"{CosmosSelectorExtensions.GetModelType<T>().ToString().ToLowerInvariant()}/";
    }

    private string[] GetFilenames<T>() where T : CosmosSelector
    {
        return Directory.GetFiles($"{_container}{GetEntityFolder<T>()}", $"*{FileExtension}");
    }
}