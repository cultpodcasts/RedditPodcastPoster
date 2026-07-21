using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Apple.Enrichers;

public interface IApplePodcastEnricher
{
    Task AddId(Podcast podcast);
}
