using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BcVideo;

public static class BcVideoStreamingService
{
    public static readonly StreamingService Service = StreamingService.BcVideo;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: BcVideoUrlMatcher.TryCompactPayload,
        tryExpand: BcVideoUrlMatcher.TryExpandPayload);
}
