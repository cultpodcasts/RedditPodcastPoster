using System.Collections.Concurrent;
using System.Text.Json;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Writers;

namespace CultPodcasts.DatabasePublisher.Tests.Fakes;

/// <summary>
/// Serialises exactly like <c>FileRepository.Write</c> (same options provider, indented) and refuses to
/// overwrite like <c>SafeFileEntityWriter</c>, but keeps the files in memory keyed by file-key.
/// </summary>
internal sealed class InMemorySafeFileEntityWriter : ISafeFileEntityWriter
{
    private readonly JsonSerializerOptions _options = CreateOptions();

    public ConcurrentDictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task Write<T>(T data) where T : CosmosSelector
    {
        if (string.IsNullOrWhiteSpace(data.FileKey))
        {
            throw new InvalidOperationException($"Entity with id '{data.Id}' has empty file-key.");
        }

        var json = JsonSerializer.Serialize(data, _options);
        if (!Files.TryAdd(data.FileKey, json))
        {
            throw new InvalidOperationException(
                $"File '{data.FileKey}' already exists when writing item with id '{data.Id}'.");
        }

        return Task.CompletedTask;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();
        options.WriteIndented = true;
        return options;
    }
}
