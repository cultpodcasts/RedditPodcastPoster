using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IAppleEpisodeResolver
{
    Task<AppleEpisode?> FindEpisode(FindAppleEpisodeRequest request, IndexingContext indexingContext);
}