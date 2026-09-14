using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PlaySuisse;

public static class PlaySuisseStreamingService
{
    public static readonly StreamingService Service = StreamingService.PlaySuisse;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
