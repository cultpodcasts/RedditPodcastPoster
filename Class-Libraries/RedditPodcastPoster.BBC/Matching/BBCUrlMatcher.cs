using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BBC.Matching;

public static class BBCUrlMatcher
{
    public static bool IsBBCUrl(Uri url) =>
        url.Host.Contains("bbc.co.uk", StringComparison.OrdinalIgnoreCase) ||
        url.Host.Contains("bbc.com", StringComparison.OrdinalIgnoreCase);

    public static bool IsSoundsPlayUrl(Uri url) =>
        IsBBCUrl(url) && url.AbsolutePath.StartsWith("/sounds/play/", StringComparison.OrdinalIgnoreCase);

    public static bool IsIplayerEpisodeUrl(Uri url) =>
        IsBBCUrl(url) && url.AbsolutePath.StartsWith("/iplayer/episode", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sounds play and iPlayer episode pages only — not news or other bbc.co.uk hosts/paths.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsSoundsPlayUrl(url) || IsIplayerEpisodeUrl(url);

    public static bool IsIplayerCatalogUrl(Uri url)
    {
        if (!url.IsAbsoluteUri || !IsBbcHost(url))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.StartsWith("/iplayer/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/news/av-embeds/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSoundsCatalogUrl(Uri url) =>
        url.IsAbsoluteUri && IsBbcHost(url) && !IsIplayerCatalogUrl(url);

    public static string? TrySoundsCompactPayload(Uri url) =>
        StreamingUrlCodecs.TryTrimPrefixHostPath(url, ["/sounds/play/"], allowSlug: false);

    public static Uri? TryExpandSoundsPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://www.bbc.co.uk/sounds/play/{payload}");

    public static string? TryIplayerCompactPayload(Uri url) =>
        StreamingUrlCodecs.TryTrimPrefixHostPath(url, ["/iplayer/episode/"], allowSlug: true);

    public static Uri? TryExpandIplayerPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://www.bbc.co.uk/iplayer/episode/{payload}");

    private static bool IsBbcHost(Uri url)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        return ServiceCatalog.IsHost(host, "bbc.co.uk") || ServiceCatalog.IsHost(host, "bbc.com");
    }
}
