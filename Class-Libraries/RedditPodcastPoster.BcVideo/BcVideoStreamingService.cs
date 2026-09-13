using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BcVideo;

public static class BcVideoStreamingService
{
    public const string Key = StreamingServiceKeys.BcVideo;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "BitChute",
        "bitchute",
        true,
        ["bitchute.com"],
        tryCompact: BcVideoUrlMatcher.TryCompactPayload,
        tryExpand: BcVideoUrlMatcher.TryExpandPayload);
}
