using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Itvx.Matching;

public static class ItvxUrlMatcher
{
    /// <summary>
    /// Path-only ITVX catalogue shape: <c>/watch/{brandSlug}/{programmeId}</c>
    /// with optional episode segment. Excludes news. Distinct from Play Suisse
    /// bare <c>/watch/{id}</c>.
    /// </summary>
    public static bool IsWatchCataloguePath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts[1].Equals("news", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return parts[1].Length > 0 && parts[2].Length > 0;
    }

    /// <summary>
    /// Brand/programme hub only: <c>/watch/{brandSlug}/{programmeId}</c>
    /// (exactly three path segments). Episode pages have a fourth segment.
    /// </summary>
    public static bool IsWatchBrandHubPath(Uri url)
    {
        if (!IsWatchCataloguePath(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3;
    }

    public static bool IsSubmitUrl(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "itv.com") &&
        IsWatchCataloguePath(url);
}
