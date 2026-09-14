using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.Vimeo;

public static class VimeoStreamingService
{
    public static readonly StreamingService Service = StreamingService.Vimeo;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: VimeoUrlMatcher.TryCompactPayload,
        tryExpand: VimeoUrlMatcher.TryExpandPayload);
}
