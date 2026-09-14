using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.DisneyPlus;

public static class DisneyPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.DisneyPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
