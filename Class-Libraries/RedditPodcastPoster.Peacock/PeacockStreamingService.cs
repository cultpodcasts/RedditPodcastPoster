using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Peacock;

public static class PeacockStreamingService
{
    public static readonly StreamingService Service = StreamingService.Peacock;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}