using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Persistence;

internal class FileRepository : IFileRepository
{
    private const string FileExtension = ".json";
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _container;
    private readonly ILogger<IFileRepository> _logger;

    internal FileRepository(
        JsonSerializerOptions jsonSerializerOptions,
        IFilenameSelector filenameSelector,
        string container,
        ILogger<IFileRepository> logger)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _container = container;
        KeySelector = filenameSelector;
        _logger = logger;
    }

    public IKeySelector KeySelector { get; }

    public async Task Write<T>(string key, T data)
    {
        await using var createStream = File.Create($"{_container}\\{key}{FileExtension}");
        await JsonSerializer.SerializeAsync(createStream, data, _jsonSerializerOptions);
    }

    public async Task<T?> Read<T>(string key) where T : class
    {
        try
        {
            await using var readStream = File.OpenRead($"{_container}\\{key}{FileExtension}");
            return await JsonSerializer.DeserializeAsync<T>(readStream);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<T> GetAll<T>() where T : class
    {
        var filenames = Directory.GetFiles(_container, $"*{FileExtension}");
        var keys = filenames.Select(x =>
            x.Substring(_container.Length + 1, x.Length - (FileExtension.Length + _container.Length + 1)));
        foreach (var item in keys)
        {
            yield return (await Read<T>(item))!;
        }
    }
}