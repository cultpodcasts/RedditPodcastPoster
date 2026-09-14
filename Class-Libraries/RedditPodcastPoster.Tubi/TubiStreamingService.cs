using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Tubi.Matching;

namespace RedditPodcastPoster.Tubi;

public static class TubiStreamingService
{
    public static readonly StreamingService Service = StreamingService.Tubi;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: TubiUrlMatcher.TryCompactPayload,
        tryExpand: TubiUrlMatcher.TryExpandPayload);
}
