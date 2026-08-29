using System.Text;
using System.Text.Json;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Episodes;

/// <summary>
/// Thread-safe JSONL log of catalog-patch identities (not full episode documents). Overwrites the file on construct.
/// </summary>
public sealed class EpisodeServiceBackfillPatchLogWriter : IDisposable
{
    public const string DefaultFileName = "episode-service-backfill-patches.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public EpisodeServiceBackfillPatchLogWriter(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, Encoding.UTF8);
    }

    public string Path { get; }

    public void Write(EpisodeServiceCatalogPatch patch, bool applied)
    {
        ArgumentNullException.ThrowIfNull(patch);
        Write(patch.EpisodeId, patch.PodcastId, applied, ServiceKeys(patch), IdSlots(patch));
    }

    public void Write(
        Guid episodeId,
        Guid podcastId,
        bool applied,
        IReadOnlyList<string>? serviceKeys = null,
        IReadOnlyList<string>? idSlots = null)
    {
        var line = JsonSerializer.Serialize(
            new PatchLogLine(
                episodeId,
                podcastId,
                applied,
                DateTime.UtcNow,
                serviceKeys is { Count: > 0 } ? serviceKeys : null,
                idSlots is { Count: > 0 } ? idSlots : null),
            JsonOptions);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }
    }

    private static IReadOnlyList<string>? ServiceKeys(EpisodeServiceCatalogPatch patch)
    {
        if (patch.Services is not { Count: > 0 })
        {
            return null;
        }

        return patch.Services.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string>? IdSlots(EpisodeServiceCatalogPatch patch)
    {
        if (patch.Ids is null || patch.Ids.IsEmpty)
        {
            return null;
        }

        var slots = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(patch.Ids.Spotify))
        {
            slots.Add("spotify");
        }

        if (patch.Ids.Apple is not null)
        {
            slots.Add("apple");
        }

        if (!string.IsNullOrWhiteSpace(patch.Ids.YouTube))
        {
            slots.Add("youtube");
        }

        return slots;
    }

    private sealed record PatchLogLine(
        Guid EpisodeId,
        Guid PodcastId,
        bool Applied,
        DateTime Utc,
        IReadOnlyList<string>? ServiceKeys,
        IReadOnlyList<string>? IdSlots);
}
