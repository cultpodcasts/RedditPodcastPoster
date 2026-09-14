using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.ParamountPlus;

public static class ParamountPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.ParamountPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
