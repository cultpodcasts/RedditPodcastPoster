using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.Vimeo;

public static class VimeoStreamingService
{
    public const string Key = StreamingServiceKeys.Vimeo;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Vimeo",
        "vimeo",
        true,
        ["vimeo.com"],
        tryCompact: VimeoUrlMatcher.TryCompactPayload,
        tryExpand: VimeoUrlMatcher.TryExpandPayload);
}
