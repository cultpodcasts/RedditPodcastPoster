using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

public interface IAppleEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);
}