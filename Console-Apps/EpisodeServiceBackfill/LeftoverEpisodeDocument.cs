using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
namespace EpisodeServiceBackfill;

/// <summary>
/// Read-only Cosmos snapshot for catalog backfill. Inherits catalog fields from
/// <see cref="Episode"/> and adds retired leftover members (<c>urls</c>, top-level ids,
/// <c>images</c>) so the CLI can deserialize them. Never persist this type; patches
/// write only <c>services</c> and nested <c>ids</c>.
/// </summary>
public sealed class LeftoverEpisodeDocument : Episode
{
    [JsonPropertyName("urls")]
    public ServiceUrls? Urls { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeId")]
    public string? YouTubeId { get; set; }

    [JsonPropertyName("images")]
    public EpisodeImages? Images { get; set; }

    public static bool TryParse(string json, out LeftoverEpisodeDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            document = JsonSerializer.Deserialize<LeftoverEpisodeDocument>(json, EpisodeDocumentJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return false;
        }

        return document is not null;
    }

    public bool NeedsBackfill() => HasUrlCoverageGaps() || HasIdCoverageGaps();

    public string? DescribeNeed()
    {
        if (!NeedsBackfill())
        {
            return null;
        }

        var urlGap = HasUrlCoverageGaps();
        var idGap = HasIdCoverageGaps();
        if (urlGap && idGap)
        {
            return "url gap / id gap";
        }

        if (urlGap)
        {
            return "url gap";
        }

        return "id gap";
    }

    public bool TryCreateCatalogPatch(out EpisodeServiceCatalogPatch? patch)
    {
        patch = null;
        if (!NeedsBackfill() || Id == Guid.Empty || PodcastId == Guid.Empty)
        {
            return false;
        }

        ApplyLeftoverToCatalog();
        EpisodeServicePresence.NormalizeCatalog(this);
        if (Services is { Count: 0 })
        {
            Services = null;
        }

        if (Services is null && Ids is null)
        {
            return false;
        }

        patch = new EpisodeServiceCatalogPatch(
            PodcastId,
            Id,
            CloneServices(Services),
            CloneIds(Ids));
        return true;
    }

    public static string? Classify(string json)
    {
        if (!TryParse(json, out var leftover) || leftover is null)
        {
            return EpisodeServiceCatalogPatchFactory.SkipReasons.DeserializeFail;
        }

        if (!leftover.NeedsBackfill())
        {
            return leftover.HasMigratablePayload()
                ? EpisodeServiceCatalogPatchFactory.SkipReasons.AlreadyCovered
                : EpisodeServiceCatalogPatchFactory.SkipReasons.NoUrlsOrIdsToMigrate;
        }

        if (leftover.Id == Guid.Empty || leftover.PodcastId == Guid.Empty)
        {
            return EpisodeServiceCatalogPatchFactory.SkipReasons.MissingIdOrPodcastId;
        }

        return leftover.TryCreateCatalogPatch(out _)
            ? null
            : EpisodeServiceCatalogPatchFactory.SkipReasons.ServicesAndIdsBothNull;
    }

    private void ApplyLeftoverToCatalog()
    {
        if (Urls is not null)
        {
            EpisodeServicePresence.TryFillMissing(this, ServiceKeys.Spotify, Urls.Spotify, null);
            EpisodeServicePresence.TryFillMissing(this, ServiceKeys.Apple, Urls.Apple, null);
            EpisodeServicePresence.TryFillMissing(this, ServiceKeys.YouTube, Urls.YouTube, null);
            EpisodeServicePresence.TryFillMissing(this, ServiceKeys.InternetArchive, Urls.InternetArchive, null);
            if (Urls.BBC is not null)
            {
                var key = ServiceCatalog.TryResolveKey(Urls.BBC) ?? ServiceKeys.BbcSounds;
                EpisodeServicePresence.TryFillMissing(this, key, Urls.BBC, null);
            }
        }

        if (!string.IsNullOrWhiteSpace(SpotifyId) &&
            string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(this)))
        {
            EpisodeServicePresence.SetSpotifyIdentity(this, SpotifyId);
        }

        if (!string.IsNullOrWhiteSpace(YouTubeId) &&
            string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(this)))
        {
            EpisodeServicePresence.SetYouTubeIdentity(this, YouTubeId);
        }

        if (AppleId is > 0 && EpisodeServicePresence.AppleEpisodeId(this) is null)
        {
            EpisodeServicePresence.SetAppleIdentity(this, AppleId);
        }

        if (Images is null)
        {
            return;
        }

        if (Images.Spotify is not null &&
            EpisodeServicePresence.TryGetImage(this, ServiceKeys.Spotify) is null)
        {
            EpisodeServicePresence.SetCatalogImage(this, ServiceKeys.Spotify, Images.Spotify);
        }

        if (Images.Apple is not null &&
            EpisodeServicePresence.TryGetImage(this, ServiceKeys.Apple) is null)
        {
            EpisodeServicePresence.SetCatalogImage(this, ServiceKeys.Apple, Images.Apple);
        }

        if (Images.YouTube is not null &&
            EpisodeServicePresence.TryGetImage(this, ServiceKeys.YouTube) is null)
        {
            EpisodeServicePresence.SetCatalogImage(this, ServiceKeys.YouTube, Images.YouTube);
        }

        if (Images.Other is null)
        {
            return;
        }

        foreach (var key in ServiceCatalog.ImageCoalesceOrder)
        {
            if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
            {
                continue;
            }

            if (EpisodeServicePresence.HasUrl(this, key) &&
                EpisodeServicePresence.TryGetImage(this, key) is null)
            {
                EpisodeServicePresence.SetCatalogImage(this, key, Images.Other);
                return;
            }
        }
    }

    private bool HasUrlCoverageGaps()
    {
        if (Urls is null)
        {
            return false;
        }

        if (Urls.Spotify is not null && !HasServiceUrl(ServiceKeys.Spotify))
        {
            return true;
        }

        if (Urls.Apple is not null && !HasServiceUrl(ServiceKeys.Apple))
        {
            return true;
        }

        if (Urls.YouTube is not null && !HasServiceUrl(ServiceKeys.YouTube))
        {
            return true;
        }

        if (Urls.InternetArchive is not null && !HasServiceUrl(ServiceKeys.InternetArchive))
        {
            return true;
        }

        if (Urls.BBC is not null)
        {
            var key = ServiceCatalog.TryResolveKey(Urls.BBC) ?? ServiceKeys.BbcSounds;
            if (!HasServiceUrl(key))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasIdCoverageGaps()
    {
        if (!string.IsNullOrWhiteSpace(SpotifyId) &&
            !string.Equals(Ids?.Spotify, SpotifyId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(YouTubeId) &&
            !string.Equals(Ids?.YouTube, YouTubeId, StringComparison.Ordinal))
        {
            return true;
        }

        if (AppleId is > 0 && Ids?.Apple != AppleId)
        {
            return true;
        }

        return false;
    }

    private bool HasServiceUrl(string key) =>
        Services is { Count: > 0 } &&
        Services.TryGetValue(key, out var link) &&
        link.Url is not null;

    private bool HasMigratablePayload()
    {
        if (Urls is not null &&
            (Urls.Spotify is not null ||
             Urls.Apple is not null ||
             Urls.YouTube is not null ||
             Urls.InternetArchive is not null ||
             Urls.BBC is not null))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(SpotifyId) || !string.IsNullOrWhiteSpace(YouTubeId) || AppleId is > 0)
        {
            return true;
        }

        if (Ids is not null && !Ids.IsEmpty)
        {
            return true;
        }

        return Services is { Count: > 0 } &&
               Services.Values.Any(link => link.Url is not null);
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
