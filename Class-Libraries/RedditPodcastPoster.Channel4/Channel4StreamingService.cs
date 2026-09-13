using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Channel4;

public static class Channel4StreamingService
{
    public const string Key = StreamingServiceKeys.Channel4;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Channel 4",
        "channel4",
        true,
        ["channel4.com", "all4.com"]);
}
