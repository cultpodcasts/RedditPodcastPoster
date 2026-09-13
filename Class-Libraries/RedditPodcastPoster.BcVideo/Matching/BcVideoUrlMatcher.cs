using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BcVideo.Matching;

public static class BcVideoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) => TryCompactPayload(url) != null;

    public static Uri CanonicalUrl(Uri url)
    {
        var compact = TryCompactPayload(url);
        return compact is null ? url : TryExpandPayload(compact) ?? url;
    }

    public static string? TryCompactPayload(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "bitchute.com"))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        if (!parts[0].Equals("video", StringComparison.OrdinalIgnoreCase) &&
            !parts[0].Equals("embed", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return IsVideoId(parts[1]) ? parts[1] : null;
    }

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://www.bitchute.com/video/{payload}");

    internal static bool IsVideoId(string part) =>
        part.Length >= 6 && part.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}
