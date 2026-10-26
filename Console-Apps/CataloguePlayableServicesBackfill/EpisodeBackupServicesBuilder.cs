using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace CataloguePlayableServicesBackfill;

/// <summary>
/// Builds a backup services map from episode JSON: prefer <c>services</c>, then leftover
/// <c>urls</c>/<c>images</c> for still-empty fields. Does not copy episode <c>ids</c>.
/// </summary>
public static class EpisodeBackupServicesBuilder
{
    public static Dictionary<string, ServiceLink> Build(EpisodeBackupDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var map = PlayableServicesDiscrepancyCarry.CloneServices(document.Services)
                  ?? new Dictionary<string, ServiceLink>(StringComparer.Ordinal);

        ApplyLeftoverUrls(map, document.Urls);
        ApplyLeftoverImages(map, document.Images);

        // Drop empty links so we do not carry null/null keys as discrepancies.
        foreach (var key in map.Keys.ToArray())
        {
            if (map[key].IsEmpty)
            {
                map.Remove(key);
            }
        }

        return map;
    }

    private static void ApplyLeftoverUrls(Dictionary<string, ServiceLink> map, ServiceUrls? urls)
    {
        if (urls is null)
        {
            return;
        }

        FillUrl(map, ServiceKeys.Spotify, urls.Spotify);
        FillUrl(map, ServiceKeys.Apple, urls.Apple);
        FillUrl(map, ServiceKeys.YouTube, urls.YouTube);
        FillUrl(map, StreamingServiceWire.ToKey(StreamingService.InternetArchive), urls.InternetArchive);
        if (urls.BBC is not null)
        {
            var key = StreamingServiceCatalog.TryResolveKey(urls.BBC)
                      ?? StreamingServiceWire.ToKey(StreamingService.BbcSounds);
            FillUrl(map, key, urls.BBC);
        }
    }

    private static void ApplyLeftoverImages(Dictionary<string, ServiceLink> map, EpisodeImages? images)
    {
        if (images is null)
        {
            return;
        }

        FillImage(map, ServiceKeys.Spotify, images.Spotify);
        FillImage(map, ServiceKeys.Apple, images.Apple);
        FillImage(map, ServiceKeys.YouTube, images.YouTube);

        if (images.Other is null)
        {
            return;
        }

        foreach (var key in StreamingServiceCatalog.ImageCoalesceOrder)
        {
            if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
            {
                continue;
            }

            if (map.TryGetValue(key, out var link) &&
                link.Url is not null &&
                link.Image is null)
            {
                link.Image = images.Other;
                return;
            }
        }
    }

    private static void FillUrl(Dictionary<string, ServiceLink> map, string key, Uri? url)
    {
        if (url is null)
        {
            return;
        }

        if (!map.TryGetValue(key, out var link))
        {
            map[key] = new ServiceLink { Url = url };
            return;
        }

        if (link.Url is null)
        {
            link.Url = url;
        }
    }

    private static void FillImage(Dictionary<string, ServiceLink> map, string key, Uri? image)
    {
        if (image is null)
        {
            return;
        }

        if (!map.TryGetValue(key, out var link))
        {
            map[key] = new ServiceLink { Image = image };
            return;
        }

        if (link.Image is null)
        {
            link.Image = image;
        }
    }
}
