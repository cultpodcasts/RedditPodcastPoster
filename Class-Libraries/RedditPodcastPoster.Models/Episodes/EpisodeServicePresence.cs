// pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

/// <summary>
/// Catalog accessors for <c>services</c> / nested <c>ids</c>.
/// Leftover Cosmos <c>urls</c> / top-level ids / <c>images</c> are not on <see cref="Episode"/>;
/// they wither on full <c>Save()</c>. Application code must not write those leftover members.
/// Cover art coalesces from <c>services.*.image</c> via <see cref="ServiceCatalog.ImageCoalesceOrder"/>.
/// </summary>
public static class EpisodeServicePresence // pragma: allowlist secret
{
    public static readonly string[] SocialPostUrlOrder =
    [
        ServiceKeys.YouTube,
        ServiceKeys.Spotify,
        ServiceKeys.Apple,
        ServiceKeys.InternetArchive,
        ServiceKeys.BbcIplayer,
        ServiceKeys.BbcSounds
    ];

    /// <summary>
    /// Drop the retired <c>other</c> catalog key and keep nested ids aligned.
    /// Does not copy leftover Cosmos JSON onto the catalog (typed <see cref="Episode"/>
    /// has no leftover members).
    /// </summary>
    public static void NormalizeCatalog(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.Services is { Count: > 0 })
        {
            var map = new Dictionary<string, EpisodeServiceLink>(episode.Services, StringComparer.Ordinal); // pragma: allowlist secret
            map.Remove("other");
            episode.Services = map.Count == 0 ? null : map;
        }
        else
        {
            episode.Services = null;
        }

        SyncIds(episode);
    }

    /// <summary>
    /// Nested <c>ids</c> is the only id source of truth. Empty nested objects are dropped.
    /// </summary>
    public static void SyncIds(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.Ids is null || episode.Ids.IsEmpty)
        {
            episode.Ids = null;
        }
    }

    public static Uri? TryGetUrl(Episode episode, string key)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (episode.Services is { Count: > 0 } &&
            episode.Services.TryGetValue(key, out var link) &&
            link.Url is not null)
        {
            return link.Url;
        }

        return null;
    }

    public static Uri? TryGetImage(Episode episode, string key)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (episode.Services is { Count: > 0 } &&
            episode.Services.TryGetValue(key, out var link) &&
            link.Image is not null)
        {
            return link.Image;
        }

        return null;
    }

    public static bool HasUrl(Episode episode, string key) => TryGetUrl(episode, key) is not null;

    public static string? SpotifyEpisodeId(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return string.IsNullOrWhiteSpace(episode.Ids?.Spotify) ? null : episode.Ids.Spotify;
    }

    public static long? AppleEpisodeId(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return episode.Ids?.Apple is > 0 ? episode.Ids.Apple : null;
    }

    public static string? YouTubeEpisodeId(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return string.IsNullOrWhiteSpace(episode.Ids?.YouTube) ? null : episode.Ids.YouTube;
    }

    public static ServiceUrls ToServiceUrls(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return new ServiceUrls
        {
            Spotify = TryGetUrl(episode, ServiceKeys.Spotify),
            Apple = TryGetUrl(episode, ServiceKeys.Apple),
            YouTube = TryGetUrl(episode, ServiceKeys.YouTube),
            InternetArchive = TryGetUrl(episode, ServiceKeys.InternetArchive),
            BBC = TryGetUrl(episode, ServiceKeys.BbcIplayer) ??
                  TryGetUrl(episode, ServiceKeys.BbcSounds)
        };
    }

    public static EpisodeImages? ToEpisodeImages(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var youtube = TryGetImage(episode, ServiceKeys.YouTube);
        var spotify = TryGetImage(episode, ServiceKeys.Spotify);
        var apple = TryGetImage(episode, ServiceKeys.Apple);
        Uri? other = null;
        foreach (var key in ServiceCatalog.ImageCoalesceOrder)
        {
            if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
            {
                continue;
            }

            other = TryGetImage(episode, key);
            if (other is not null)
            {
                break;
            }
        }

        if (youtube is null && spotify is null && apple is null && other is null)
        {
            return null;
        }

        return new EpisodeImages
        {
            YouTube = youtube,
            Spotify = spotify,
            Apple = apple,
            Other = other
        };
    }

    public static Uri? PreferredSocialPostUrl(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        foreach (var key in SocialPostUrlOrder)
        {
            var url = TryGetUrl(episode, key);
            if (url is not null)
            {
                return url;
            }
        }

        return null;
    }

    public static bool TryGetPreferredSocialPostUrl(Episode episode, out Uri url, out Service service)
    {
        ArgumentNullException.ThrowIfNull(episode);
        foreach (var key in SocialPostUrlOrder)
        {
            var found = TryGetUrl(episode, key);
            if (found is null)
            {
                continue;
            }

            url = found;
            service = key switch
            {
                ServiceKeys.YouTube => Service.YouTube,
                ServiceKeys.Spotify => Service.Spotify,
                ServiceKeys.Apple => Service.Apple,
                _ => Service.Other
            };
            return true;
        }

        url = null!;
        service = Service.Other;
        return false;
    }

    public static void SetSpotifyIdentity(Episode episode, string? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds(); // pragma: allowlist secret
        episode.Ids.Spotify = string.IsNullOrWhiteSpace(id) ? null : id;
        SyncIds(episode);
    }

    public static void SetAppleIdentity(Episode episode, long? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds(); // pragma: allowlist secret
        episode.Ids.Apple = id is > 0 ? id : null;
        SyncIds(episode);
    }

    public static void SetYouTubeIdentity(Episode episode, string? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds(); // pragma: allowlist secret
        episode.Ids.YouTube = string.IsNullOrWhiteSpace(id) ? null : id;
        SyncIds(episode);
    }

    public static Uri? CoalescedImage(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        NormalizeCatalog(episode);
        return CoalescedImage(episode.Services);
    }

    public static Uri? CoalescedImage(
        IReadOnlyDictionary<string, EpisodeServiceLink>? services) // pragma: allowlist secret
    {
        if (services is not { Count: > 0 })
        {
            return null;
        }

        foreach (var key in ServiceCatalog.ImageCoalesceOrder)
        {
            if (services.TryGetValue(key, out var link) && link.Image is not null)
            {
                return link.Image;
            }
        }

        foreach (var link in services.Values)
        {
            if (link.Image is not null)
            {
                return link.Image;
            }
        }

        return null;
    }

    public static void Upsert(Episode episode, string key, Uri? url, Uri? image)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        NormalizeCatalog(episode);
        episode.Services ??= new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal); // pragma: allowlist secret
        if (url is null && image is null)
        {
            episode.Services.Remove(key);
            if (episode.Services.Count == 0)
            {
                episode.Services = null;
            }

            return;
        }

        if (!episode.Services.TryGetValue(key, out var link))
        {
            link = new EpisodeServiceLink(); // pragma: allowlist secret
            episode.Services[key] = link;
        }

        if (url is not null)
        {
            link.Url = url;
        }

        if (image is not null)
        {
            link.Image = image;
        }
    }

    public static void SetCatalogImage(Episode episode, string key, Uri? image)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        NormalizeCatalog(episode);
        var url = TryGetUrl(episode, key);
        if (image is null && url is null)
        {
            Upsert(episode, key, null, null);
            return;
        }

        episode.Services ??= new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal);
        if (!episode.Services.TryGetValue(key, out var link))
        {
            link = new EpisodeServiceLink();
            episode.Services[key] = link;
        }

        if (url is not null)
        {
            link.Url = url;
        }

        link.Image = image;
    }

    public static bool TryFillMissing(Episode episode, string key, Uri? url, Uri? image)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var existingUrl = TryGetUrl(episode, key);
        var existingImage = TryGetImage(episode, key);
        var fillUrl = existingUrl is null && url is not null;
        var fillImage = existingImage is null && image is not null;
        if (!fillUrl && !fillImage)
        {
            return false;
        }

        Upsert(episode, key, existingUrl ?? url, existingImage ?? image);
        return true;
    }
}
