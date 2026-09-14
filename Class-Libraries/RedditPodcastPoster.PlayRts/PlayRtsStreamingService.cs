using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PlayRts;

public static class PlayRtsStreamingService
{
    public static readonly StreamingService Service = StreamingService.PlayRts;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryResolve: url =>
        {
            if (!url.IsAbsoluteUri)
            {
                return null;
            }

            var host = ServiceCatalog.CanonicalHost(url);
            if (!ServiceCatalog.IsHost(host, "rts.ch"))
            {
                return null;
            }

            return url.AbsolutePath.StartsWith("/play/", StringComparison.OrdinalIgnoreCase)
                ? StreamingServiceWire.ToKey(Service)
                : null;
        });
}
