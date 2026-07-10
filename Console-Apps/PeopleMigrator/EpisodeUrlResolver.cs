using System.Text.Json;

namespace PeopleMigrator;

/// <summary>
/// Builds production cultpodcasts.com episode URLs from CosmosDbDownloader backup JSON (read-only).
/// </summary>
internal sealed class EpisodeUrlResolver(string? backupPath)
{
    private const string SiteBase = "https://cultpodcasts.com";

    private readonly Dictionary<Guid, string?> _cache = new();

    public IReadOnlyDictionary<string, string?> ResolveMany(IEnumerable<Guid> episodeIds)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var episodeId in episodeIds.Distinct())
        {
            result[episodeId.ToString()] = TryResolve(episodeId);
        }

        return result;
    }

    public string? TryResolve(Guid episodeId)
    {
        if (_cache.TryGetValue(episodeId, out var cached))
        {
            return cached;
        }

        var url = ReadUrlFromBackup(episodeId);
        _cache[episodeId] = url;
        return url;
    }

    private string? ReadUrlFromBackup(Guid episodeId)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return null;
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

            if (!root.TryGetProperty("podcastName", out var podcastNameProperty) ||
                podcastNameProperty.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var podcastName = podcastNameProperty.GetString();
            if (string.IsNullOrWhiteSpace(podcastName))
            {
                return null;
            }

            return BuildEpisodeUrl(podcastName, episodeId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string BuildEpisodeUrl(string podcastName, Guid episodeId)
    {
        var safePodcastName = PodcastNameInSafeUrlForm(podcastName);
        return $"{SiteBase}/podcast/{safePodcastName}/{episodeId}";
    }

    private static string PodcastNameInSafeUrlForm(string podcastName)
    {
        var escapedPodcastName = Uri.EscapeDataString(podcastName);
        return escapedPodcastName.Replace("(", "%28").Replace(")", "%29");
    }
}
