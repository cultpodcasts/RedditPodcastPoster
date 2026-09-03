using System.Text.Json;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace EpisodeServiceBackfill;

/// <summary>
/// Additive Cosmos patch payload for <c>services</c> and nested <c>ids</c> only.
/// Built from <b>raw</b> JSON so hydrate-on-deserialize cannot skip a persist.
/// Does not include <c>urls</c>, top-level ids, <c>images</c>, <c>lang</c>, title, or description.
/// </summary>
public sealed record EpisodeServiceCatalogPatch(
    Guid PodcastId,
    Guid EpisodeId,
    Dictionary<string, EpisodeServiceLink>? Services,
    EpisodeIds? Ids);

public static class EpisodeServiceCatalogPatchFactory
{
    public static bool TryCreate(JsonElement raw, out EpisodeServiceCatalogPatch? patch)
    {
        patch = null;
        if (!EpisodeServiceDocumentMigration.NeedsBackfill(raw))
        {
            return false;
        }

        if (!EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([raw.GetRawText()]).Any())
        {
            return false;
        }

        Episode? episode;
        try
        {
            episode = JsonSerializer.Deserialize<Episode>(raw.GetRawText(), EpisodeDocumentJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return false;
        }

        if (episode is null || episode.Id == Guid.Empty || episode.PodcastId == Guid.Empty)
        {
            return false;
        }

        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, raw);
        EpisodeServicePresence.NormalizeCatalog(episode);

        if (episode.Services is { Count: 0 })
        {
            episode.Services = null;
        }

        if (episode.Services is null && episode.Ids is null)
        {
            return false;
        }

        patch = new EpisodeServiceCatalogPatch(
            episode.PodcastId,
            episode.Id,
            CloneServices(episode.Services),
            CloneIds(episode.Ids));
        return true;
    }

    public static bool TryCreate(string json, out EpisodeServiceCatalogPatch? patch)
    {
        using var document = JsonDocument.Parse(json);
        return TryCreate(document.RootElement, out patch);
    }

    /// <summary>
    /// Null when <see cref="TryCreate"/> would succeed; otherwise a skip-reason bucket.
    /// </summary>
    public static string? Classify(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return SkipReasons.DeserializeFail;
        }

        using (document)
        {
            return Classify(document.RootElement);
        }
    }

    public static string? Classify(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return SkipReasons.NotAnObject;
        }

        if (!EpisodeServiceDocumentMigration.NeedsBackfill(raw))
        {
            return HasMigratablePayload(raw)
                ? SkipReasons.AlreadyCovered
                : SkipReasons.NoUrlsOrIdsToMigrate;
        }

        if (!EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([raw.GetRawText()]).Any())
        {
            return SkipReasons.MissingIdOrPodcastId;
        }

        Episode? episode;
        try
        {
            episode = JsonSerializer.Deserialize<Episode>(raw.GetRawText(), EpisodeDocumentJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return SkipReasons.DeserializeFail;
        }

        if (episode is null || episode.Id == Guid.Empty || episode.PodcastId == Guid.Empty)
        {
            return SkipReasons.MissingIdOrPodcastId;
        }

        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, raw);
        EpisodeServicePresence.NormalizeCatalog(episode);

        if (episode.Services is { Count: 0 })
        {
            episode.Services = null;
        }

        if (episode.Services is null && episode.Ids is null)
        {
            return SkipReasons.ServicesAndIdsBothNull;
        }

        if (EpisodeServiceCatalogPatchFactory.TryCreate(raw, out _))
        {
            return null;
        }

        return SkipReasons.Other;
    }

    public static class SkipReasons
    {
        public const string AlreadyCovered = "already_covered";
        public const string NoUrlsOrIdsToMigrate = "no_urls_ids_to_migrate";
        public const string MissingIdOrPodcastId = "missing_id_or_podcastId";
        public const string DeserializeFail = "deserialize_fail";
        public const string ServicesAndIdsBothNull = "services_and_ids_both_null";
        public const string NotAnObject = "not_an_object";
        public const string Other = "other";
    }

    private static bool HasMigratablePayload(JsonElement episode)
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

    private static Dictionary<string, EpisodeServiceLink>? CloneServices(
        Dictionary<string, EpisodeServiceLink>? services)
    {
        if (services is not { Count: > 0 })
        {
            return null;
        }

        return services.ToDictionary(
            x => x.Key,
            x => new EpisodeServiceLink { Url = x.Value.Url, Image = x.Value.Image },
            StringComparer.Ordinal);
    }

    private static EpisodeIds? CloneIds(EpisodeIds? ids)
    {
        if (ids is null || ids.IsEmpty)
        {
            return null;
        }

        return new EpisodeIds
        {
            Spotify = ids.Spotify,
            Apple = ids.Apple,
            YouTube = ids.YouTube
        };
    }
}
