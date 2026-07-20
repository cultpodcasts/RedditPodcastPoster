using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Enrichers;

public interface IApplePodcastEnricher
{
    Task AddId(Podcast podcast);
}