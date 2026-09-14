using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Netflix;

public static class NetflixStreamingService
{
    public static readonly StreamingService Service = StreamingService.Netflix;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: NetflixUrlMatcher.TryCompactPayload,
        tryExpand: NetflixUrlMatcher.TryExpandPayload);
}
