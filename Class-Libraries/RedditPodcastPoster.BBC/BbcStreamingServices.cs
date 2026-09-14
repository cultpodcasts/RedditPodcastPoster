using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BBC;

public static class BbcSoundsStreamingService
{
    public static readonly StreamingService Service = StreamingService.BbcSounds;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryResolve: url => BBCUrlMatcher.IsSoundsCatalogUrl(url)
            ? StreamingServiceWire.ToKey(Service)
            : null,
        tryCompact: BBCUrlMatcher.TrySoundsCompactPayload,
        tryExpand: BBCUrlMatcher.TryExpandSoundsPayload);
}

public static class BbcIplayerStreamingService
{
    public static readonly StreamingService Service = StreamingService.BbcIplayer;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryResolve: url => BBCUrlMatcher.IsIplayerCatalogUrl(url)
            ? StreamingServiceWire.ToKey(Service)
            : null,
        tryCompact: BBCUrlMatcher.TryIplayerCompactPayload,
        tryExpand: BBCUrlMatcher.TryExpandIplayerPayload);
}
