using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

public interface IAppleEpisodeResolver
{
    Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null);
}