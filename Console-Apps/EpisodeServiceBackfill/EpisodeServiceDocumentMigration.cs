using System.Text.Json;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace EpisodeServiceBackfill;

/// <summary>
/// Pure Cosmos-document migration for <c>services</c> + <c>ids</c>.
/// Selection and <see cref="NeedsBackfill"/> read leftover from raw JSON (typed
/// <see cref="Episode"/> deserialize ignores leftover members).
/// Apply normalizes catalog and nested ids only; it does not dual-write leftover JSON.
/// </summary>
public static class EpisodeServiceDocumentMigration
{
    public readonly record struct EpisodeRef(Guid PodcastId, Guid EpisodeId);

    public static IReadOnlyList<EpisodeRef> SelectDocumentsToBackfill(IEnumerable<string> jsonDocuments)
    {
        ArgumentNullException.ThrowIfNull(jsonDocuments);
        var selected = new List<EpisodeRef>();
        foreach (var json in jsonDocuments)
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

            if (TryReadRef(document.RootElement, out var episodeRef))
            {
                selected.Add(episodeRef);
            }
        }

        return selected;
    }

    public static bool NeedsBackfill(JsonElement episode)
    {
        if (episode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasUrlCoverageGaps(episode) || HasIdCoverageGaps(episode);
    }

    /// <summary>
    /// One-line gap kind when <see cref="NeedsBackfill"/> is true; otherwise null.
    /// </summary>
    public static string? DescribeNeed(JsonElement episode)
    {
        if (episode.ValueKind != JsonValueKind.Object)
        {
            return "unreadable";
        }

        var urlGap = HasUrlCoverageGaps(episode);
        var idGap = HasIdCoverageGaps(episode);
        if (urlGap && idGap)
        {
            return "url gap / id gap";
        }

        if (urlGap)
        {
            return "url gap";
        }

        if (idGap)
        {
            return "id gap";
        }

        return null;
    }

    /// <summary>
    /// Keep nested <c>ids</c> aligned after leftover JSON has been copied onto catalog by
    /// <see cref="MergeRawLeftoverIntoCatalog"/>. Returns true when the in-memory catalog
    /// shape changed. Does not write leftover DTO members.
    /// </summary>
    public static bool Apply(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var before = Capture(episode);
        EpisodeServicePresence.NormalizeCatalog(episode);
        EpisodeServicePresence.SyncIds(episode);
        if (episode.Services is { Count: 0 })
        {
            episode.Services = null;
        }

        return !before.Equals(Capture(episode));
    }

    /// <summary>
    /// Copy leftover <c>urls</c> / top-level ids / <c>images</c> from raw Cosmos JSON into
    /// catalog <c>services</c> and nested <c>ids</c>. Typed <see cref="Episode"/> no longer
    /// has leftover members, so backfill must use this instead of deserialize-then-merge.
    /// </summary>
    public static void MergeRawLeftoverIntoCatalog(Episode episode, JsonElement raw)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetObject(raw, "urls", out var urls))
        {
            MergeRawUrl(episode, urls, "spotify", ServiceKeys.Spotify);
            MergeRawUrl(episode, urls, "apple", ServiceKeys.Apple);
            MergeRawUrl(episode, urls, "youtube", ServiceKeys.YouTube);
            MergeRawUrl(episode, urls, "internetArchive", ServiceKeys.InternetArchive);
            if (TryGetUrl(urls, "bbc", out var bbc) &&
                Uri.TryCreate(bbc, UriKind.Absolute, out var bbcUri))
            {
                var key = ServiceCatalog.TryResolveKey(bbcUri) ?? ServiceKeys.BbcSounds;
                EpisodeServicePresence.TryFillMissing(episode, key, bbcUri, null);
            }
        }

        if (TryGetNonEmptyString(raw, "spotifyId", out var spotifyId) &&
            string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)))
        {
            EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
        }

        if (TryGetNonEmptyString(raw, "youTubeId", out var youTubeId) &&
            string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)))
        {
            EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeId);
        }

        if (raw.TryGetProperty("appleId", out var appleEl) &&
            appleEl.ValueKind is JsonValueKind.Number &&
            appleEl.TryGetInt64(out var appleId) &&
            EpisodeServicePresence.AppleEpisodeId(episode) is null)
        {
            EpisodeServicePresence.SetAppleIdentity(episode, appleId);
        }

        if (TryGetObject(raw, "images", out var images))
        {
            MergeRawImage(episode, images, "spotify", ServiceKeys.Spotify);
            MergeRawImage(episode, images, "apple", ServiceKeys.Apple);
            MergeRawImage(episode, images, "youtube", ServiceKeys.YouTube);
            if (TryGetUrl(images, "other", out var other) &&
                Uri.TryCreate(other, UriKind.Absolute, out var otherUri))
            {
                foreach (var key in ServiceCatalog.ImageCoalesceOrder)
                {
                    if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
                    {
                        continue;
                    }

                    if (EpisodeServicePresence.HasUrl(episode, key) &&
                        EpisodeServicePresence.TryGetImage(episode, key) is null)
                    {
                        EpisodeServicePresence.SetCatalogImage(episode, key, otherUri);
                        break;
                    }
                }
            }
        }
    }

    private static void MergeRawUrl(Episode episode, JsonElement urls, string urlsName, string serviceKey)
    {
        if (TryGetUrl(urls, urlsName, out var value) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            EpisodeServicePresence.TryFillMissing(episode, serviceKey, uri, null);
        }
    }

    private static void MergeRawImage(Episode episode, JsonElement images, string imagesName, string serviceKey)
    {
        if (TryGetUrl(images, imagesName, out var value) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            EpisodeServicePresence.TryGetImage(episode, serviceKey) is null)
        {
            EpisodeServicePresence.SetCatalogImage(episode, serviceKey, uri);
        }
    }

    private static bool TryReadRef(JsonElement episode, out EpisodeRef episodeRef)
    {
        episodeRef = default;
        if (!episode.TryGetProperty("id", out var idEl) || !idEl.TryGetGuid(out var episodeId))
        {
            return false;
        }

        if (!episode.TryGetProperty("podcastId", out var podcastEl) || !podcastEl.TryGetGuid(out var podcastId))
        {
            return false;
        }

        episodeRef = new EpisodeRef(podcastId, episodeId);
        return true;
    }

    private static bool HasUrlCoverageGaps(JsonElement episode)
    {
        if (!TryGetObject(episode, "urls", out var urls))
        {
            return false;
        }

        TryGetObject(episode, "services", out var services);

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

    private static bool HasIdCoverageGaps(JsonElement episode)
    {
        TryGetObject(episode, "ids", out var ids);

        if (TryGetNonEmptyString(episode, "spotifyId", out var spotifyId) &&
            !HasNonEmptyString(ids, "spotify", spotifyId))
        {
            return true;
        }

        if (TryGetNonEmptyString(episode, "youTubeId", out var youTubeId) &&
            !HasNonEmptyString(ids, "youtube", youTubeId))
        {
            return true;
        }

        if (episode.TryGetProperty("appleId", out var appleEl) &&
            appleEl.ValueKind is JsonValueKind.Number &&
            appleEl.TryGetInt64(out var appleId))
        {
            if (!ids.TryGetProperty("apple", out var nested) ||
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

    private static bool HasNonEmptyString(JsonElement ids, string name, string expected)
    {
        return ids.ValueKind == JsonValueKind.Object &&
               ids.TryGetProperty(name, out var el) &&
               el.ValueKind == JsonValueKind.String &&
               string.Equals(el.GetString(), expected, StringComparison.Ordinal);
    }

    private readonly record struct ShapeSnapshot(
        string? Services,
        string? Ids);

    private static ShapeSnapshot Capture(Episode episode) =>
        new(
            SerializeServices(episode.Services),
            episode.Ids is null
                ? null
                : $"{episode.Ids.Spotify}|{episode.Ids.Apple}|{episode.Ids.YouTube}");

    private static string? SerializeServices(Dictionary<string, EpisodeServiceLink>? services)
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
