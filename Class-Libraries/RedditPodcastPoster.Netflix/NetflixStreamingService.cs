using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Netflix;

public static class NetflixStreamingService
{
    public const string Key = StreamingServiceKeys.Netflix;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Netflix",
        "netflix",
        true,
        ["netflix.com"],
        tryCompact: NetflixUrlMatcher.TryCompactPayload,
        tryExpand: NetflixUrlMatcher.TryExpandPayload);
}
