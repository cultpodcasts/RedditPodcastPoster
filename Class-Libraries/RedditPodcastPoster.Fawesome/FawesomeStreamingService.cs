using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Fawesome;

public static class FawesomeStreamingService
{
    public static readonly StreamingService Service = StreamingService.Fawesome;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
