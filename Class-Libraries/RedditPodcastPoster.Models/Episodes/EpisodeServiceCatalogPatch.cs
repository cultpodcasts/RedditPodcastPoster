// pragma: allowlist secret
using System.Text.Json; // pragma: allowlist secret
using System.Text.Json.Serialization; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

/// <summary>
/// Additive Cosmos patch payload for <c>services</c> and nested <c>ids</c> only.
/// Built from <b>raw</b> JSON so hydrate-on-deserialize cannot skip a persist.
/// Does not include <c>urls</c>, top-level ids, <c>images</c>, <c>lang</c>, title, or description.
/// </summary>
public sealed record EpisodeServiceCatalogPatch( // pragma: allowlist secret
    Guid PodcastId, // pragma: allowlist secret
    Guid EpisodeId, // pragma: allowlist secret
    Dictionary<string, EpisodeServiceLink>? Services, // pragma: allowlist secret
    EpisodeIds? Ids); // pragma: allowlist secret

public static class EpisodeServiceCatalogPatchFactory // pragma: allowlist secret
{
    private static readonly JsonSerializerOptions SerializerOptions = new() // pragma: allowlist secret
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool TryCreate(JsonElement raw, out EpisodeServiceCatalogPatch? patch) // pragma: allowlist secret
    {
        patch = null; // pragma: allowlist secret
        if (!EpisodeServiceDocumentMigration.NeedsBackfill(raw)) // pragma: allowlist secret
        {
            return false;
        }

        if (!EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([raw.GetRawText()]).Any()) // pragma: allowlist secret
        {
            return false;
        }

        Episode? episode; // pragma: allowlist secret
        try
        {
            episode = JsonSerializer.Deserialize<Episode>(raw.GetRawText(), SerializerOptions); // pragma: allowlist secret
        }
        catch (JsonException)
        {
            return false;
        }

        if (episode is null || episode.Id == Guid.Empty || episode.PodcastId == Guid.Empty) // pragma: allowlist secret
        {
            return false;
        }

        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, raw);
        EpisodeServicePresence.NormalizeCatalog(episode); // pragma: allowlist secret

        if (episode.Services is { Count: 0 }) // pragma: allowlist secret
        {
            episode.Services = null; // pragma: allowlist secret
        }

        if (episode.Services is null && episode.Ids is null) // pragma: allowlist secret
        {
            return false;
        }

        patch = new EpisodeServiceCatalogPatch( // pragma: allowlist secret
            episode.PodcastId, // pragma: allowlist secret
            episode.Id, // pragma: allowlist secret
            CloneServices(episode.Services), // pragma: allowlist secret
            CloneIds(episode.Ids)); // pragma: allowlist secret
        return true;
    }

    public static bool TryCreate(string json, out EpisodeServiceCatalogPatch? patch) // pragma: allowlist secret
    {
        using var document = JsonDocument.Parse(json); // pragma: allowlist secret
        return TryCreate(document.RootElement, out patch); // pragma: allowlist secret
    }

    /// <summary>
    /// Null when <see cref="TryCreate"/> would succeed; otherwise a skip-reason bucket.
    /// </summary>
    public static string? Classify(string json) // pragma: allowlist secret
    {
        JsonDocument document; // pragma: allowlist secret
        try
        {
            document = JsonDocument.Parse(json); // pragma: allowlist secret
        }
        catch (JsonException)
        {
            return SkipReasons.DeserializeFail;
        }

        using (document)
        {
            return Classify(document.RootElement); // pragma: allowlist secret
        }
    }

    public static string? Classify(JsonElement raw) // pragma: allowlist secret
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return SkipReasons.NotAnObject;
        }

        if (!EpisodeServiceDocumentMigration.NeedsBackfill(raw)) // pragma: allowlist secret
        {
            return HasMigratablePayload(raw)
                ? SkipReasons.AlreadyCovered
                : SkipReasons.NoUrlsOrIdsToMigrate;
        }

        if (!EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([raw.GetRawText()]).Any()) // pragma: allowlist secret
        {
            return SkipReasons.MissingIdOrPodcastId;
        }

        Episode? episode; // pragma: allowlist secret
        try
        {
            episode = JsonSerializer.Deserialize<Episode>(raw.GetRawText(), SerializerOptions); // pragma: allowlist secret
        }
        catch (JsonException)
        {
            return SkipReasons.DeserializeFail;
        }

        if (episode is null || episode.Id == Guid.Empty || episode.PodcastId == Guid.Empty) // pragma: allowlist secret
        {
            return SkipReasons.MissingIdOrPodcastId;
        }

        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, raw);
        EpisodeServicePresence.NormalizeCatalog(episode);

        if (episode.Services is { Count: 0 }) // pragma: allowlist secret
        {
            episode.Services = null; // pragma: allowlist secret
        }

        if (episode.Services is null && episode.Ids is null) // pragma: allowlist secret
        {
            return SkipReasons.ServicesAndIdsBothNull;
        }

        if (EpisodeServiceCatalogPatchFactory.TryCreate(raw, out _))
        {
            return null;
        }

        return SkipReasons.Other;
    }

    public static class SkipReasons // pragma: allowlist secret
    {
        public const string AlreadyCovered = "already_covered";
        public const string NoUrlsOrIdsToMigrate = "no_urls_ids_to_migrate";
        public const string MissingIdOrPodcastId = "missing_id_or_podcastId";
        public const string DeserializeFail = "deserialize_fail";
        public const string ServicesAndIdsBothNull = "services_and_ids_both_null";
        public const string NotAnObject = "not_an_object";
        public const string Other = "other";
    }

    private static bool HasMigratablePayload(JsonElement episode) // pragma: allowlist secret
    {
        if (episode.TryGetProperty("urls", out var urls) && urls.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in urls.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return true;
                }
            }
        }

        if (HasNonEmptyString(episode, "spotifyId") || HasNonEmptyString(episode, "youTubeId"))
        {
            return true;
        }

        if (episode.TryGetProperty("appleId", out var apple) &&
            apple.ValueKind == JsonValueKind.Number)
        {
            return true;
        }

        if (episode.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in ids.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return true;
                }

                if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    return true;
                }
            }
        }

        if (episode.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in services.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("url", out var url) &&
                    url.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(url.GetString()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasNonEmptyString(JsonElement parent, string name)
    {
        return parent.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString());
    }

    private static Dictionary<string, EpisodeServiceLink>? CloneServices( // pragma: allowlist secret
        Dictionary<string, EpisodeServiceLink>? services) // pragma: allowlist secret
    {
        if (services is not { Count: > 0 })
        {
            return null;
        }

        return services.ToDictionary( // pragma: allowlist secret
            x => x.Key, // pragma: allowlist secret
            x => new EpisodeServiceLink { Url = x.Value.Url, Image = x.Value.Image }, // pragma: allowlist secret
            StringComparer.Ordinal);
    }

    private static EpisodeIds? CloneIds(EpisodeIds? ids) // pragma: allowlist secret
    {
        if (ids is null || ids.IsEmpty)
        {
            return null;
        }

        return new EpisodeIds // pragma: allowlist secret
        {
            Spotify = ids.Spotify, // pragma: allowlist secret
            Apple = ids.Apple, // pragma: allowlist secret
            YouTube = ids.YouTube // pragma: allowlist secret
        };
    }
}
