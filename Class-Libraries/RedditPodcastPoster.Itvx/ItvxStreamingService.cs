using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Itvx;

public static class ItvxStreamingService
{
    public static readonly StreamingService Service = StreamingService.Itvx;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
