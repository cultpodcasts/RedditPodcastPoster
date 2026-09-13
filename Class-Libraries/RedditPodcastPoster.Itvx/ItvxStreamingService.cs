using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Itvx;

public static class ItvxStreamingService
{
    public const string Key = StreamingServiceKeys.Itvx;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "ITVX",
        "itvx",
        true,
        ["itv.com"]);
}
