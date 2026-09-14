using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.DiscoveryPlus;

public static class DiscoveryPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.DiscoveryPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
