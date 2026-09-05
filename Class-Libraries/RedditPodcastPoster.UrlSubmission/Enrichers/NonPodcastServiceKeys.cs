using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

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
            return ServiceCatalog.TryResolveKey(bbc) ?? ServiceKeys.BbcSounds;
        }

        if (item.InternetArchiveUrl != null)
        {
            return ServiceKeys.InternetArchive;
        }

        if (item.Url is { } url)
        {
            return ServiceCatalog.TryResolveKey(url) ?? ServiceCatalog.KeyFromUnknownHost(url);
        }

        return null;
    }
}
