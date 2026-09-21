using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.CanalPlus;

public static class CanalPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.CanalPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}