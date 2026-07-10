using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpisodeGuestHandleRestorer;

public sealed class CachedPatchEntry
{
    [JsonPropertyName("episodeId")]
    public Guid EpisodeId { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("twitterHandles")]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    public string[]? BlueskyHandles { get; set; }

    [JsonPropertyName("patchTwitter")]
    public bool PatchTwitter { get; set; }

    [JsonPropertyName("patchBluesky")]
    public bool PatchBluesky { get; set; }
}

public sealed class GuestHandleRestoreCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("episodes")]
    public List<CachedPatchEntry> Episodes { get; set; } = [];

    public static string ResolveCachePath(string? cachePath, string backupPath) =>
        string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(backupPath, "guest-handle-restore-cache.json")
            : cachePath;

    public static bool BackupPathMatches(GuestHandleRestoreCache cache, string backupPath) =>
        string.Equals(
            Path.GetFullPath(cache.BackupPath),
            Path.GetFullPath(backupPath),
            StringComparison.OrdinalIgnoreCase);

    public static GuestHandleRestoreCache FromPatchPlans(
        string backupPath,
        IReadOnlyList<HandlePatchPlan> toPatch,
        IReadOnlyDictionary<Guid, ProductionEpisodeDocument> production)
    {
        var episodes = new List<CachedPatchEntry>(toPatch.Count);
        foreach (var plan in toPatch)
        {
            if (!production.TryGetValue(plan.EpisodeId, out var prod))
            {
                continue;
            }

            episodes.Add(new CachedPatchEntry
            {
                EpisodeId = plan.EpisodeId,
                PodcastId = prod.PodcastId,
                Title = plan.Title,
                TwitterHandles = plan.BackupTwitterHandles,
                BlueskyHandles = plan.BackupBlueskyHandles,
                PatchTwitter = plan.PatchTwitter,
                PatchBluesky = plan.PatchBluesky
            });
        }

        return new GuestHandleRestoreCache
        {
            BackupPath = backupPath,
            CreatedAt = DateTimeOffset.UtcNow,
            Episodes = episodes
        };
    }

    public static async Task<GuestHandleRestoreCache> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Cache file not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var cache = await JsonSerializer.DeserializeAsync<GuestHandleRestoreCache>(stream, JsonOptions, cancellationToken);
        if (cache == null || cache.Episodes.Count == 0)
        {
            throw new InvalidDataException($"Cache file is empty or invalid: {path}");
        }

        return cache;
    }

    public static async Task WriteAsync(
        string path,
        GuestHandleRestoreCache cache,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, cache, JsonOptions, cancellationToken);
    }
}
