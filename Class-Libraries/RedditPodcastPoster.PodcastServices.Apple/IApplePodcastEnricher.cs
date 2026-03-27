using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastEnricher
{
    Task AddId(Podcast podcast);
}