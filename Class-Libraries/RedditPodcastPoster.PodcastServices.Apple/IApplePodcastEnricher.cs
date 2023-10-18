using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastEnricher
{
    Task AddId(Podcast podcast);
}