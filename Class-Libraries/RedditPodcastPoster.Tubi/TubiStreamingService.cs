using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Tubi.Matching;

namespace RedditPodcastPoster.Tubi;

public static class TubiStreamingService
{
    public const string Key = StreamingServiceKeys.Tubi;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Tubi",
        "tubi",
        true,
        ["tubitv.com"],
        tryCompact: TubiUrlMatcher.TryCompactPayload,
        tryExpand: TubiUrlMatcher.TryExpandPayload);
}
