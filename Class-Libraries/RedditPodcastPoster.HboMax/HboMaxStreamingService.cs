using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.HboMax;

public static class HboMaxStreamingService
{
    public static readonly StreamingService Service = StreamingService.HboMax;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
