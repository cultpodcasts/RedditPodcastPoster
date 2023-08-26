using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastEnricher
{
    Task AddId(Podcast podcast);
}