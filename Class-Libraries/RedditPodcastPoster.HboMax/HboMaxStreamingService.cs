using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.HboMax;

public static class HboMaxStreamingService
{
    public const string Key = StreamingServiceKeys.HboMax;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "HBO Max",
        "hbo-max",
        true,
        ["max.com", "hbomax.com"]);
}
