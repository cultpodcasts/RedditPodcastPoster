using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Vimeo.Matching;

public static class VimeoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) => TryCompactPayload(url) != null;

    public static string? TryCompactPayload(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "vimeo.com"))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var candidate = parts[0] == "video" && parts.Length > 1 ? parts[1] : parts[0];
        return candidate.Length > 0 && candidate.All(char.IsDigit) ? candidate : null;
    }

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://vimeo.com/{payload}");
}
