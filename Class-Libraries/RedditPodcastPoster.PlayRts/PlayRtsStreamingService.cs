using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PlayRts;

public static class PlayRtsStreamingService
{
    public const string Key = StreamingServiceKeys.PlayRts;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Play RTS",
        "play-rts",
        true,
        ["rts.ch"],
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
                ? Key
                : null;
        });
}
