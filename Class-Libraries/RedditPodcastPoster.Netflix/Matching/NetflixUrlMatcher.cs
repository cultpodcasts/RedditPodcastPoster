using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Netflix.Matching;

public static class NetflixUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "netflix.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Contains("/title/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/watch/", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryCompactPayload(Uri url) =>
        StreamingUrlCodecs.TryTrimPrefixHostPath(url, ["/title/"], allowSlug: false, hosts: ["netflix.com"]);

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://www.netflix.com/title/{payload}");
}
