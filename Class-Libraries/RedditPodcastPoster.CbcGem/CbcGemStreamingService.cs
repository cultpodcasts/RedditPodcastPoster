using RedditPodcastPoster.CbcGem.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.CbcGem;

public static class CbcGemStreamingService
{
    public static readonly StreamingService Service = StreamingService.CbcGem;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: CbcGemUrlMatcher.TryCompactPayload,
        tryExpand: CbcGemUrlMatcher.TryExpandPayload);
}
