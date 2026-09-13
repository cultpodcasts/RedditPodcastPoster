using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.DisneyPlus;

public static class DisneyPlusStreamingService
{
    public const string Key = StreamingServiceKeys.DisneyPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Disney+",
        "disney-plus",
        true,
        ["disneyplus.com"]);
}
