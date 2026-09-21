using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Zdf;

public static class ZdfStreamingService
{
    public static readonly StreamingService Service = StreamingService.Zdf;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}