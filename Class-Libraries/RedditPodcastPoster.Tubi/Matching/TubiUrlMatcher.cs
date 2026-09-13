using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Tubi.Matching;

public static class TubiUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "tubitv.com")
        && ServiceCatalog.TryCompactUrl(ServiceKeys.Tubi, url) != null;

    public static Uri CanonicalUrl(Uri url) =>
        ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.Tubi, url);
}
