using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AppleTvPlus;

public static class AppleTvPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.AppleTvPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}