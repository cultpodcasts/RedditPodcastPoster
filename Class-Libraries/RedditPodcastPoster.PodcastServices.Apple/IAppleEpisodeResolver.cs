using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IAppleEpisodeResolver
{
    Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null);
}