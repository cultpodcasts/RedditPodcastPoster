using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Arte;

public static class ArteStreamingService
{
    public static readonly StreamingService Service = StreamingService.Arte;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}