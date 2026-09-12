using RedditPodcastPoster.Models.Pod\u0063asts;

namespace RedditPodcastPoster.BcVideo.Matching;

public static class BcVideoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "\u0062itchute.com")
        && ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url) != null;

    public static Uri CanonicalUrl(Uri url) =>
        ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BcVideo, url);
}
