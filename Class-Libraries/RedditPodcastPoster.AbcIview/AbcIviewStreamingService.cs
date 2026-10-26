using RedditPodcastPoster.AbcIview.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AbcIview;

public static class AbcIviewStreamingService
{
    public static readonly StreamingService Service = StreamingService.AbcIview;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: AbcIviewUrlMatcher.TryCompactPayload,
        tryExpand: AbcIviewUrlMatcher.TryExpandPayload);
}
