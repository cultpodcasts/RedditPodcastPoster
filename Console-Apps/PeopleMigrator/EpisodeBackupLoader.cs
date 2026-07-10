using System.Text.Json;

namespace PeopleMigrator;

/// <summary>
/// Loads episode title/description from a CosmosDbDownloader backup folder (read-only).
/// </summary>
internal sealed class EpisodeBackupLoader(string backupPath)
{
    private readonly Dictionary<Guid, EpisodeTextSnapshot> _cache = new();

    public EpisodeTextSnapshot? TryLoad(Guid episodeId)
    {
        if (_cache.TryGetValue(episodeId, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(backupPath, $"{episodeId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var snapshot = new EpisodeTextSnapshot(
                ReadString(root, "title"),
                ReadString(root, "description"));

            _cache[episodeId] = snapshot;
            return snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

internal readonly record struct EpisodeTextSnapshot(string? Title, string? Description);
