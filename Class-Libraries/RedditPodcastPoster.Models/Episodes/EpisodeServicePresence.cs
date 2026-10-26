using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Models.YouTube;

namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Catalog accessors for <c>services</c> / nested <c>ids</c>.
/// Leftover Cosmos <c>urls</c> / top-level ids / <c>images</c> are not bound on
/// <see cref="Episode"/> (including top-level <c>youTubeId</c>, which withers via an override);
/// they wither on full <c>Save()</c>. Application code must not write those leftover members.
/// Catalogue playables (TvShowEpisode / NewsReport / Film) keep a compact <c>youTubeId</c>
/// synced from <c>services.youtube.url</c> for Azure Search — distinct from episode leftovers.
/// Cover art coalesces from <c>services.*.image</c> in
/// <see cref="ServiceCatalog.IndexIdImageOrder"/> then remaining service keys.
/// Streaming destinations register above Models; leftover BBC/IA DTO slots use JSON key literals.
/// </summary>
public static class EpisodeServicePresence
{
    /// <summary>
    /// Index-id listen URL preference (YouTube, then Spotify, then Apple).
    /// Production Tweet/Bluesky callers pass <c>StreamingServiceCatalog.ImageCoalesceOrder</c>
    /// so streaming destinations keep a deterministic share order (iPlayer before Sounds).
    /// Remaining catalog keys not in the supplied list are walked after it so a Netflix
    /// (or other streaming) URL can still be posted when it is the only one.
    /// </summary>
    public static readonly string[] SocialPostUrlOrder = ServiceCatalog.IndexIdImageOrder;

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
            var map = new Dictionary<string, ServiceLink>(episode.Services, StringComparer.Ordinal);
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

    public static Uri? TryGetUrl(Episode episode, StreamingService service) =>
        TryGetUrl(episode, StreamingServiceWire.ToKey(service));

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

    public static Uri? TryGetImage(Episode episode, StreamingService service) =>
        TryGetImage(episode, StreamingServiceWire.ToKey(service));

    public static bool HasUrl(Episode episode, string key) => TryGetUrl(episode, key) is not null;

    public static bool HasUrl(Episode episode, StreamingService service) =>
        HasUrl(episode, StreamingServiceWire.ToKey(service));

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
        if (!string.IsNullOrWhiteSpace(episode.Ids?.YouTube))
        {
            return episode.Ids.YouTube;
        }

        // Catalogue post-projections and pre-backfill rows: extract from services.youtube.url.
        // Podcast Episode has no top-level youTubeId member — leftover Cosmos JSON is ignored.
        var url = TryGetUrl(episode, ServiceKeys.YouTube);
        return url is null ? null : YouTubeIdResolver.Extract(url);
    }

    /// <summary>
    /// TvShowEpisode / NewsReport: set compact <c>youTubeId</c> from <c>services.youtube.url</c>
    /// (or clear it). No-op for podcast <see cref="Episode"/> (nested <c>ids.youtube</c> only).
    /// Call after any catalogue services mutation before Save — not from search-document helpers.
    /// </summary>
    public static void SyncCatalogueYouTubeId(Playable playable)
    {
        ArgumentNullException.ThrowIfNull(playable);
        switch (playable)
        {
            case TvShowEpisode tv:
                tv.YouTubeId = ExtractYouTubeIdFromServices(tv.Services);
                return;
            case NewsReport news:
                news.YouTubeId = ExtractYouTubeIdFromServices(news.Services);
                return;
            default:
                // Episode and any other Playable: no catalogue youTubeId member.
                return;
        }
    }

    /// <summary>
    /// Film: set <see cref="Films.Film.YouTubeId"/> from <c>services.youtube.url</c> (or clear it).
    /// Call after any film services mutation before Save / search push.
    /// </summary>
    public static void SyncCatalogueYouTubeId(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        film.YouTubeId = ExtractYouTubeIdFromServices(film.Services);
    }

    private static string? ExtractYouTubeIdFromServices(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        if (services is null ||
            !services.TryGetValue(ServiceKeys.YouTube, out var link) ||
            link.Url is null ||
            string.IsNullOrWhiteSpace(link.Url.ToString()))
        {
            return null;
        }

        var id = YouTubeIdResolver.Extract(link.Url);
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// <summary>
    /// Service keys (Spotify / Apple / YouTube) where the episode has a nested <c>ids.*</c> id but no
    /// <c>services.{key}.url</c>. Catalogue playables (TV / film / news) keep Services only, so these are the
    /// slots to materialise (client lookup first, <see cref="EnsureServiceUrlsFromIds"/> as the fallback)
    /// before mapping, or that platform is dropped from the catalogue.
    /// </summary>
    public static IReadOnlyList<string> KeysWithIdButNoUrl(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var keys = new List<string>(3);
        if (SpotifyEpisodeId(episode) is not null && TryGetUrl(episode, ServiceKeys.Spotify) is null)
        {
            keys.Add(ServiceKeys.Spotify);
        }

        if (AppleEpisodeId(episode) is not null && TryGetUrl(episode, ServiceKeys.Apple) is null)
        {
            keys.Add(ServiceKeys.Apple);
        }

        if (!string.IsNullOrWhiteSpace(episode.Ids?.YouTube) && TryGetUrl(episode, ServiceKeys.YouTube) is null)
        {
            keys.Add(ServiceKeys.YouTube);
        }

        return keys;
    }

    /// <summary>
    /// Deterministic fallback for <see cref="KeysWithIdButNoUrl"/> when no client could supply the real link:
    /// fills <c>services.{key}.url</c> from the nested id (Spotify <c>open.spotify.com/episode/</c>, YouTube
    /// <c>watch?v=</c>, Apple <c>podcast/id{show}?i={episode}</c> which needs <paramref name="applePodcastId"/>).
    /// An existing <c>services.{key}.image</c> is kept; an existing url is never replaced. Returns true when any
    /// url was added.
    /// </summary>
    public static bool EnsureServiceUrlsFromIds(Episode episode, long? applePodcastId = null)
    {
        ArgumentNullException.ThrowIfNull(episode);
        NormalizeCatalog(episode);
        var filled = false;
        foreach (var key in KeysWithIdButNoUrl(episode))
        {
            var url = key switch
            {
                ServiceKeys.Spotify => new Uri($"https://open.spotify.com/episode/{SpotifyEpisodeId(episode)}"),
                ServiceKeys.YouTube => new Uri($"https://www.youtube.com/watch?v={episode.Ids!.YouTube}"),
                ServiceKeys.Apple when applePodcastId is > 0 =>
                    new Uri($"https://podcasts.apple.com/podcast/id{applePodcastId}?i={AppleEpisodeId(episode)}"),
                _ => null
            };
            if (url is null)
            {
                continue;
            }

            Upsert(episode, key, url, null);
            filled = true;
        }

        return filled;
    }

    public static ServiceUrls ToServiceUrls(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return new ServiceUrls
        {
            Spotify = TryGetUrl(episode, ServiceKeys.Spotify),
            Apple = TryGetUrl(episode, ServiceKeys.Apple),
            YouTube = TryGetUrl(episode, ServiceKeys.YouTube),
            InternetArchive = TryGetUrl(episode, "internetArchive"),
            BBC = TryGetUrl(episode, "bbcIplayer") ??
                  TryGetUrl(episode, "bbcSounds")
        };
    }

    public static EpisodeImages? ToEpisodeImages(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var youtube = TryGetImage(episode, ServiceKeys.YouTube);
        var spotify = TryGetImage(episode, ServiceKeys.Spotify);
        var apple = TryGetImage(episode, ServiceKeys.Apple);
        if (youtube is null && spotify is null && apple is null)
        {
            return null;
        }

        return new EpisodeImages
        {
            YouTube = youtube,
            Spotify = spotify,
            Apple = apple
        };
    }

    public static Uri? PreferredSocialPostUrl(Episode episode) =>
        PreferredSocialPostUrl(episode, SocialPostUrlOrder);

    public static Uri? PreferredSocialPostUrl(Episode episode, IReadOnlyList<string> keyOrder)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (TryGetPreferredSocialPost(episode, keyOrder, out var url, out _, out _))
        {
            return url;
        }

        return null;
    }

    public static bool TryGetPreferredSocialPostUrl(Episode episode, out Uri url, out Service service) =>
        TryGetPreferredSocialPostUrl(episode, SocialPostUrlOrder, out url, out service);

    public static bool TryGetPreferredSocialPostUrl(
        Episode episode,
        IReadOnlyList<string> keyOrder,
        out Uri url,
        out Service service) =>
        TryGetPreferredSocialPost(episode, keyOrder, out url, out _, out service);

    public static bool TryGetPreferredSocialPost(
        Episode episode,
        out Uri url,
        out string serviceKey,
        out Service service) =>
        TryGetPreferredSocialPost(episode, SocialPostUrlOrder, out url, out serviceKey, out service);

    public static bool TryGetPreferredSocialPost(
        Episode episode,
        IReadOnlyList<string> keyOrder,
        out Uri url,
        out string serviceKey,
        out Service service)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(keyOrder);
        foreach (var key in keyOrder)
        {
            var found = TryGetUrl(episode, key);
            if (found is null)
            {
                continue;
            }

            url = found;
            serviceKey = key;
            service = MapSocialPostService(key);
            return true;
        }

        var seen = new HashSet<string>(keyOrder, StringComparer.Ordinal);
        foreach (var (key, link) in episode.Services ?? [])
        {
            if (link.Url is null || !seen.Add(key))
            {
                continue;
            }

            url = link.Url;
            serviceKey = key;
            service = MapSocialPostService(key);
            return true;
        }

        url = null!;
        serviceKey = "";
        service = Service.Other;
        return false;
    }

    /// <summary>
    /// Catalog listen URLs that are present, <see cref="SocialPostUrlOrder"/> first then remaining keys.
    /// </summary>
    public static string FormatCatalogUrlsForLog(Episode episode) =>
        FormatCatalogUrlsForLog(episode, SocialPostUrlOrder);

    /// <summary>
    /// Catalog listen URLs that are present, <paramref name="keyOrder"/> first then remaining keys.
    /// </summary>
    public static string FormatCatalogUrlsForLog(Episode episode, IReadOnlyList<string> keyOrder)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(keyOrder);
        if (episode.Services is not { Count: > 0 })
        {
            return "";
        }

        var parts = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keyOrder)
        {
            var found = TryGetUrl(episode, key);
            if (found is null)
            {
                continue;
            }

            parts.Add($"{key}={found}");
            seen.Add(key);
        }

        foreach (var (key, link) in episode.Services)
        {
            if (!seen.Add(key) || link.Url is null)
            {
                continue;
            }

            parts.Add($"{key}={link.Url}");
        }

        return string.Join(";", parts);
    }

    private static Service MapSocialPostService(string key) =>
        key switch
        {
            ServiceKeys.YouTube => Service.YouTube,
            ServiceKeys.Spotify => Service.Spotify,
            ServiceKeys.Apple => Service.Apple,
            _ => Service.Other
        };

    public static void SetSpotifyIdentity(Episode episode, string? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds();
        episode.Ids.Spotify = string.IsNullOrWhiteSpace(id) ? null : id;
        SyncIds(episode);
    }

    public static void SetAppleIdentity(Episode episode, long? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds();
        episode.Ids.Apple = id is > 0 ? id : null;
        SyncIds(episode);
    }

    public static void SetYouTubeIdentity(Episode episode, string? id)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.Ids ??= new EpisodeIds();
        episode.Ids.YouTube = string.IsNullOrWhiteSpace(id) ? null : id;
        SyncIds(episode);
    }

    /// <summary>
    /// Cover art for search/share only: YouTube, then Spotify, then Apple, then remaining catalog keys.
    /// Extra service art stays on <c>services.{key}.image</c> and is not collapsed into leftover <c>images.other</c>.
    /// </summary>
    public static Uri? CoalescedImage(Episode episode) =>
        CoalescedImage(episode, ServiceCatalog.IndexIdImageOrder);

    public static Uri? CoalescedImage(Episode episode, IReadOnlyList<string> keyOrder)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(keyOrder);
        NormalizeCatalog(episode);
        return CoalescedImage(episode.Services, keyOrder);
    }

    public static Uri? CoalescedImage(
        IReadOnlyDictionary<string, ServiceLink>? services) =>
        CoalescedImage(services, ServiceCatalog.IndexIdImageOrder);

    public static Uri? CoalescedImage(
        IReadOnlyDictionary<string, ServiceLink>? services,
        IReadOnlyList<string> keyOrder)
    {
        ArgumentNullException.ThrowIfNull(keyOrder);
        if (services is not { Count: > 0 })
        {
            return null;
        }

        foreach (var key in keyOrder)
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
        episode.Services ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
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
            link = new ServiceLink();
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

        episode.Services ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        if (!episode.Services.TryGetValue(key, out var link))
        {
            link = new ServiceLink();
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

    public static void Upsert(Episode episode, StreamingService service, Uri? url, Uri? image) =>
        Upsert(episode, StreamingServiceWire.ToKey(service), url, image);

    public static void SetCatalogImage(Episode episode, StreamingService service, Uri? image) =>
        SetCatalogImage(episode, StreamingServiceWire.ToKey(service), image);

    public static bool TryFillMissing(Episode episode, StreamingService service, Uri? url, Uri? image) =>
        TryFillMissing(episode, StreamingServiceWire.ToKey(service), url, image);
}
