using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;

namespace RedditPodcastPoster.ModelTransformer;

public class SplitFileRepository
{
    private const string FileExtension = ".json";
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<SplitFileRepository> _logger;

    public SplitFileRepository(
        JsonSerializerOptions jsonSerializerOptions,
        IFilenameSelector filenameSelector,
        ILogger<SplitFileRepository> logger)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        KeySelector = filenameSelector;
        _logger = logger;
    }

    public IKeySelector KeySelector { get; }

    public async Task Write<T>(string container, string key, T data)
    {
        await using var createStream = File.Create($"{container}\\{key}{FileExtension}");
        await JsonSerializer.SerializeAsync(createStream, data, _jsonSerializerOptions);
    }

    public async Task<T?> Read<T>(string container, string key) where T : class
    {
        try
        {
            await using var readStream = File.OpenRead($"{container}\\{key}{FileExtension}");
            return await JsonSerializer.DeserializeAsync<T>(readStream);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<T> GetAll<T>(string container) where T : class
    {
        var filenames = Directory.GetFiles(container, $"*{FileExtension}");
        var keys = filenames.Select(x =>
            x.Substring(container.Length + 1, x.Length - (FileExtension.Length + container.Length + 1)));
        foreach (var item in keys)
        {
            yield return (await Read<T>(container, item))!;
        }
    }
}