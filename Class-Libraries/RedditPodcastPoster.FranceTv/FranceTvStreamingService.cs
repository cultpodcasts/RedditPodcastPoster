using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.FranceTv;

public static class FranceTvStreamingService
{
    public const string Key = StreamingServiceKeys.FranceTv;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "France TV",
        "france-tv",
        true,
        ["france.tv"]);
}