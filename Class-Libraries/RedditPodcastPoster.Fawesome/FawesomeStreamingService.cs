using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Fawesome;

public static class FawesomeStreamingService
{
    public const string Key = StreamingServiceKeys.Fawesome;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Fawesome",
        "fawesome",
        true,
        ["fawesome.tv"]);
}
