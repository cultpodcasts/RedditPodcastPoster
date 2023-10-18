using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);
}