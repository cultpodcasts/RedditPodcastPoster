using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.ParamountPlus;

public static class ParamountPlusStreamingService
{
    public const string Key = StreamingServiceKeys.ParamountPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Paramount+",
        "paramount-plus",
        true,
        ["paramountplus.com"]);
}
