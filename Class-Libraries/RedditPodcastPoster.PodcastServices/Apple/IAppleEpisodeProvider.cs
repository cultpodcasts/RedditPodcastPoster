using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IAppleEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);
}