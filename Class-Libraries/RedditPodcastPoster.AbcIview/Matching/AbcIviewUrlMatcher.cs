using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AbcIview.Matching;

public static class AbcIviewUrlMatcher
{
    private const string Host = "iview.abc.net.au";

    /// <summary>
    /// Catalogue item: <c>/video/{id}</c> (episode) or <c>/show/{slug}</c> (series hub).
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        TryCompactPayload(url) is not null || IsShowHub(url);

    public static string? TryCompactPayload(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), Host))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].Equals("video", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[1]))
        {
            return null;
        }

        return parts[1];
    }

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://iview.abc.net.au/video/{payload}");

    private static bool IsShowHub(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), Host))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 &&
               parts[0].Equals("show", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(parts[1]);
    }
}
