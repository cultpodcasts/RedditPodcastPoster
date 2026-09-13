using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

/// <summary>
/// Shared BBC → Internet Archive → catalogue / unknown-host key resolution for
/// non-podcast enrichers (fill-missing and refresh-meta).
/// </summary>
internal static class NonPodcastServiceKeys
{
    public static string? Resolve(ResolvedNonPodcastServiceItem item)
    {
        if (item.BBCUrl is { } bbc)
        {
            return StreamingServiceCatalog.TryResolveKey(bbc) ?? StreamingServiceKeys.BbcSounds;
        }

        if (item.InternetArchiveUrl != null)
        {
            return StreamingServiceKeys.InternetArchive;
        }

        if (item.Url is { } url)
        {
            return StreamingServiceCatalog.TryResolveKey(url) ?? ServiceCatalog.KeyFromUnknownHost(url);
        }

        return null;
    }
}
