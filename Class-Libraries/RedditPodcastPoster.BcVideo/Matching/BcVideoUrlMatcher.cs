using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.BcVideo.Matching;

public static class BcVideoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "bitchute.com")
        && ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url) != null;

    public static Uri CanonicalUrl(Uri url) =>
        ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BcVideo, url);
}
