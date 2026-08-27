// pragma: allowlist secret
using System.Text.Json; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

/// <summary>
/// Pure Cosmos-document migration for <c>services</c> + <c>ids</c>. // pragma: allowlist secret
/// Selection reads raw JSON (typed <see cref="Episode"/> deserialize already hydrates). // pragma: allowlist secret
/// Apply mutates an in-memory episode the same way serialize dual-write does. // pragma: allowlist secret
/// </summary>
public static class EpisodeServiceDocumentMigration // pragma: allowlist secret
{
    public readonly record struct EpisodeRef(Guid PodcastId, Guid EpisodeId); // pragma: allowlist secret

    public static IReadOnlyList<EpisodeRef> SelectDocumentsToBackfill(IEnumerable<string> jsonDocuments) // pragma: allowlist secret
    {
        ArgumentNullException.ThrowIfNull(jsonDocuments); // pragma: allowlist secret
        var selected = new List<EpisodeRef>(); // pragma: allowlist secret
        foreach (var json in jsonDocuments) // pragma: allowlist secret
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            using var document = JsonDocument.Parse(json);
            if (!NeedsBackfill(document.RootElement))
            {
                continue;
            }

            if (TryReadRef(document.RootElement, out var episodeRef)) // pragma: allowlist secret
            {
                selected.Add(episodeRef); // pragma: allowlist secret
            }
        }

        return selected;
    }

    public static bool NeedsBackfill(JsonElement episode) // pragma: allowlist secret
    {
        if (episode.ValueKind != JsonValueKind.Object) // pragma: allowlist secret
        {
            return false;
        }

        return HasUrlCoverageGaps(episode) || HasIdCoverageGaps(episode); // pragma: allowlist secret
    }

    /// <summary>
    /// Hydrate <c>services</c> / <c>ids</c> and dual-write legacy slots. // pragma: allowlist secret
    /// Returns true when the persisted shape changed and the document should be saved.
    /// </summary>
    public static bool Apply(Episode episode) // pragma: allowlist secret
    {
        ArgumentNullException.ThrowIfNull(episode); // pragma: allowlist secret
        var before = Capture(episode); // pragma: allowlist secret
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret
        EpisodeServicePresence.SyncLegacy(episode); // pragma: allowlist secret
        EpisodeServicePresence.SyncIds(episode); // pragma: allowlist secret
        if (episode.Services is { Count: 0 }) // pragma: allowlist secret
        {
            episode.Services = null; // pragma: allowlist secret
        }

        return !before.Equals(Capture(episode)); // pragma: allowlist secret
    }

    private static bool TryReadRef(JsonElement episode, out EpisodeRef episodeRef) // pragma: allowlist secret
    {
        episodeRef = default; // pragma: allowlist secret
        if (!episode.TryGetProperty("id", out var idEl) || !idEl.TryGetGuid(out var episodeId)) // pragma: allowlist secret
        {
            return false;
        }

        if (!episode.TryGetProperty("podcastId", out var podcastEl) || !podcastEl.TryGetGuid(out var podcastId)) // pragma: allowlist secret
        {
            return false;
        }

        episodeRef = new EpisodeRef(podcastId, episodeId); // pragma: allowlist secret
        return true;
    }

    private static bool HasUrlCoverageGaps(JsonElement episode) // pragma: allowlist secret
    {
        if (!TryGetObject(episode, "urls", out var urls)) // pragma: allowlist secret
        {
            return false;
        }

        TryGetObject(episode, "services", out var services); // pragma: allowlist secret

        if (UrlUncovered(urls, "spotify", services, ServiceKeys.Spotify))
        {
            return true;
        }

        if (UrlUncovered(urls, "apple", services, ServiceKeys.Apple))
        {
            return true;
        }

        if (UrlUncovered(urls, "youtube", services, ServiceKeys.YouTube))
        {
            return true;
        }

        if (UrlUncovered(urls, "internetArchive", services, ServiceKeys.InternetArchive))
        {
            return true;
        }

        if (TryGetUrl(urls, "bbc", out var bbc) &&
            Uri.TryCreate(bbc, UriKind.Absolute, out var bbcUri))
        {
            var key = ServiceCatalog.TryResolveKey(bbcUri) ?? ServiceKeys.BbcSounds;
            if (!HasServiceUrl(services, key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIdCoverageGaps(JsonElement episode) // pragma: allowlist secret
    {
        TryGetObject(episode, "ids", out var ids); // pragma: allowlist secret

        if (TryGetNonEmptyString(episode, "spotifyId", out var spotifyId) && // pragma: allowlist secret
            !HasNonEmptyString(ids, "spotify", spotifyId)) // pragma: allowlist secret
        {
            return true;
        }

        if (TryGetNonEmptyString(episode, "youTubeId", out var youTubeId) && // pragma: allowlist secret
            !HasNonEmptyString(ids, "youtube", youTubeId)) // pragma: allowlist secret
        {
            return true;
        }

        if (episode.TryGetProperty("appleId", out var appleEl) && // pragma: allowlist secret
            appleEl.ValueKind is JsonValueKind.Number &&
            appleEl.TryGetInt64(out var appleId))
        {
            if (!ids.TryGetProperty("apple", out var nested) || // pragma: allowlist secret
                nested.ValueKind != JsonValueKind.Number ||
                !nested.TryGetInt64(out var nestedApple) ||
                nestedApple != appleId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool UrlUncovered(JsonElement urls, string urlsName, JsonElement services, string serviceKey)
    {
        return TryGetUrl(urls, urlsName, out _) && !HasServiceUrl(services, serviceKey);
    }

    private static bool HasServiceUrl(JsonElement services, string key)
    {
        return services.ValueKind == JsonValueKind.Object &&
               services.TryGetProperty(key, out var link) &&
               link.ValueKind == JsonValueKind.Object &&
               TryGetUrl(link, "url", out _);
    }

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetUrl(JsonElement parent, string name, out string url)
    {
        url = "";
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        url = value.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(url);
    }

    private static bool TryGetNonEmptyString(JsonElement parent, string name, out string value)
    {
        value = "";
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = el.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasNonEmptyString(JsonElement ids, string name, string expected) // pragma: allowlist secret
    {
        return ids.ValueKind == JsonValueKind.Object && // pragma: allowlist secret
               ids.TryGetProperty(name, out var el) && // pragma: allowlist secret
               el.ValueKind == JsonValueKind.String &&
               string.Equals(el.GetString(), expected, StringComparison.Ordinal);
    }

    private readonly record struct ShapeSnapshot(
        string? Services,
        string? Ids, // pragma: allowlist secret
        string? SpotifyId,
        long? AppleId,
        string? YouTubeId,
        string? UrlsSpotify,
        string? UrlsApple,
        string? UrlsYouTube,
        string? UrlsBbc,
        string? UrlsInternetArchive);

    private static ShapeSnapshot Capture(Episode episode) => // pragma: allowlist secret
        new(
            SerializeServices(episode.Services), // pragma: allowlist secret
            episode.Ids is null // pragma: allowlist secret
                ? null
                : $"{episode.Ids.Spotify}|{episode.Ids.Apple}|{episode.Ids.YouTube}", // pragma: allowlist secret
            string.IsNullOrWhiteSpace(episode.SpotifyId) ? null : episode.SpotifyId, // pragma: allowlist secret
            episode.AppleId, // pragma: allowlist secret
            string.IsNullOrWhiteSpace(episode.YouTubeId) ? null : episode.YouTubeId, // pragma: allowlist secret
            episode.Urls?.Spotify?.ToString(), // pragma: allowlist secret
            episode.Urls?.Apple?.ToString(), // pragma: allowlist secret
            episode.Urls?.YouTube?.ToString(), // pragma: allowlist secret
            episode.Urls?.BBC?.ToString(), // pragma: allowlist secret
            episode.Urls?.InternetArchive?.ToString()); // pragma: allowlist secret

    private static string? SerializeServices(Dictionary<string, EpisodeServiceLink>? services) // pragma: allowlist secret
    {
        if (services is not { Count: > 0 })
        {
            return null;
        }

        return string.Join("|", services
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}:{x.Value.Url}|{x.Value.Image}"));
    }
}
