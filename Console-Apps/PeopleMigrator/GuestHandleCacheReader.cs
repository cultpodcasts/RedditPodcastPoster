using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeopleMigrator;

/// <summary>
/// Reads guest-handle-restore-cache.json produced by EpisodeGuestHandleRestorer.
/// </summary>
internal static class GuestHandleCacheReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<GuestHandleEpisode>> ReadAsync(
        string cachePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(cachePath))
        {
            throw new FileNotFoundException($"Cache file not found: {cachePath}");
        }

        await using var stream = File.OpenRead(cachePath);
        var cache = await JsonSerializer.DeserializeAsync<GuestHandleRestoreCacheDocument>(
            stream,
            JsonOptions,
            cancellationToken);

        if (cache?.Episodes is not { Count: > 0 })
        {
            throw new InvalidDataException($"Cache file is empty or invalid: {cachePath}");
        }

        return cache.Episodes
            .Select(x => new GuestHandleEpisode(
                x.TwitterHandles,
                x.BlueskyHandles,
                ParseEpisodeId(x.EpisodeId)))
            .ToList();
    }

    public static string? ReadBackupPath(string cachePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        using var stream = File.OpenRead(cachePath);
        var cache = JsonSerializer.Deserialize<GuestHandleRestoreCacheDocument>(
            stream,
            JsonOptions);

        return string.IsNullOrWhiteSpace(cache?.BackupPath) ? null : cache.BackupPath.Trim();
    }

    private static Guid? ParseEpisodeId(string? episodeId)
    {
        return Guid.TryParse(episodeId, out var parsed) ? parsed : null;
    }

    private sealed class GuestHandleRestoreCacheDocument
    {
        [JsonPropertyName("backupPath")]
        public string? BackupPath { get; set; }

        [JsonPropertyName("episodes")]
        public List<GuestHandleCacheEntry>? Episodes { get; set; }
    }

    private sealed class GuestHandleCacheEntry
    {
        [JsonPropertyName("episodeId")]
        public string? EpisodeId { get; set; }

        [JsonPropertyName("twitterHandles")]
        public string[]? TwitterHandles { get; set; }

        [JsonPropertyName("blueskyHandles")]
        public string[]? BlueskyHandles { get; set; }
    }
}

internal readonly record struct GuestHandleEpisode(
    string[]? TwitterHandles,
    string[]? BlueskyHandles,
    Guid? EpisodeId = null);
