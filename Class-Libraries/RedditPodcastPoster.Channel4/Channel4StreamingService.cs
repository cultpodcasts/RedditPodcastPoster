using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Channel4;

public static class Channel4StreamingService
{
    public static readonly StreamingService Service = StreamingService.Channel4;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
