using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Hulu;

public static class HuluStreamingService
{
    public static readonly StreamingService Service = StreamingService.Hulu;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}