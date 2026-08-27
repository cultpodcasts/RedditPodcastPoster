// pragma: allowlist secret
using System.Text.Json.Serialization; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

/// <summary>
/// Dual-read/write between adjacent <c>services.{key}.{url,image}</c> and legacy
/// <c>urls</c> / <c>images</c> so existing Cosmos documents and indexers keep working.
/// </summary>
public static class EpisodeServicePresence // pragma: allowlist secret
{
    public static void Hydrate(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Urls ??= new ServiceUrls();
        var map = episode.Services is { Count: > 0 }
            ? new Dictionary<string, EpisodeServiceLink>(episode.Services, StringComparer.Ordinal) // pragma: allowlist secret
            : new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal); // pragma: allowlist secret

        Merge(map, ServiceKeys.Spotify, episode.Urls.Spotify, episode.Images?.Spotify);
        Merge(map, ServiceKeys.Apple, episode.Urls.Apple, episode.Images?.Apple);
        Merge(map, ServiceKeys.YouTube, episode.Urls.YouTube, episode.Images?.YouTube);

        var bbcKey = episode.Urls.BBC is { } bbcUrl
            ? ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds
            : null;
        if (bbcKey is ServiceKeys.BbcSounds or ServiceKeys.BbcIplayer)
        {
            Merge(map, bbcKey, episode.Urls.BBC, episode.Images?.Other);
        }

        Merge(
            map,
            ServiceKeys.InternetArchive,
            episode.Urls.InternetArchive,
            bbcKey is null ? episode.Images?.Other : null);

        if (episode.Images?.Other is { } otherImage &&
            !map.Values.Any(link => link.Image == otherImage))
        {
            Merge(map, ServiceKeys.Other, url: null, otherImage);
        }

        episode.Services = map.Count == 0 ? null : map;
    }

    public static void SyncLegacy(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.Services is not { Count: > 0 } services)
        {
            return;
        }

        episode.Urls ??= new ServiceUrls();
        episode.Urls.Spotify = Url(services, ServiceKeys.Spotify);
        episode.Urls.Apple = Url(services, ServiceKeys.Apple);
        episode.Urls.YouTube = Url(services, ServiceKeys.YouTube);
        episode.Urls.InternetArchive = Url(services, ServiceKeys.InternetArchive);
        episode.Urls.BBC =
            Url(services, ServiceKeys.BbcIplayer) ??
            Url(services, ServiceKeys.BbcSounds);

        var youtube = Image(services, ServiceKeys.YouTube);
        var spotify = Image(services, ServiceKeys.Spotify);
        var apple = Image(services, ServiceKeys.Apple);
        var other = Image(services, ServiceKeys.BbcIplayer) ??
                    Image(services, ServiceKeys.BbcSounds) ??
                    Image(services, ServiceKeys.InternetArchive) ??
                    Image(services, ServiceKeys.Vimeo) ??
                    Image(services, ServiceKeys.Netflix) ??
                    Image(services, ServiceKeys.AmazonPrime) ??
                    Image(services, ServiceKeys.Other);

        if (youtube is null && spotify is null && apple is null && other is null)
        {
            if (episode.Images is not null &&
                episode.Images.YouTube is null &&
                episode.Images.Spotify is null &&
                episode.Images.Apple is null &&
                episode.Images.Other is null)
            {
                episode.Images = null;
            }

            return;
        }

        episode.Images ??= new EpisodeImages();
        episode.Images.YouTube = youtube;
        episode.Images.Spotify = spotify;
        episode.Images.Apple = apple;
        episode.Images.Other = other;
    }

    public static Uri? CoalescedImage(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        Hydrate(episode);
        return CoalescedImage(episode.Services, episode.Images);
    }

    public static Uri? CoalescedImage(
        IReadOnlyDictionary<string, EpisodeServiceLink>? services, // pragma: allowlist secret
        EpisodeImages? images)
    {
        if (services is { Count: > 0 })
        {
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
        }

        return images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;
    }

    public static void Upsert(Episode episode, string key, Uri? url, Uri? image)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Hydrate(episode);
        episode.Services ??= new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal); // pragma: allowlist secret
        if (url is null && image is null)
        {
            episode.Services.Remove(key);
            if (episode.Services.Count == 0)
            {
                episode.Services = null;
            }

            SyncLegacy(episode);
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

        SyncLegacy(episode);
    }

    private static void Merge(
        Dictionary<string, EpisodeServiceLink> map, // pragma: allowlist secret
        string key,
        Uri? url,
        Uri? image)
    {
        if (url is null && image is null)
        {
            return;
        }

        if (!map.TryGetValue(key, out var link))
        {
            map[key] = new EpisodeServiceLink { Url = url, Image = image }; // pragma: allowlist secret
            return;
        }

        link.Url ??= url;
        link.Image ??= image;
    }

    private static Uri? Url(Dictionary<string, EpisodeServiceLink> services, string key) => // pragma: allowlist secret
        services.TryGetValue(key, out var link) ? link.Url : null;

    private static Uri? Image(Dictionary<string, EpisodeServiceLink> services, string key) => // pragma: allowlist secret
        services.TryGetValue(key, out var link) ? link.Image : null;
}
