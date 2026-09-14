using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.TvnzPlus;

public static class TvnzPlusStreamingService
{
    public static readonly StreamingService Service = StreamingService.TvnzPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service);
}
