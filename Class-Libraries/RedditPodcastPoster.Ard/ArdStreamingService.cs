using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Ard;

public static class ArdStreamingService
{
    public static readonly StreamingService Service = StreamingService.Ard;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}