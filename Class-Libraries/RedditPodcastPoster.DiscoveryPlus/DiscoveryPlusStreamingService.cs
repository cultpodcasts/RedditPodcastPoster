using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.DiscoveryPlus;

public static class DiscoveryPlusStreamingService
{
    public const string Key = StreamingServiceKeys.DiscoveryPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "discovery+",
        "discovery-plus",
        true,
        ["discoveryplus.com"]);
}
