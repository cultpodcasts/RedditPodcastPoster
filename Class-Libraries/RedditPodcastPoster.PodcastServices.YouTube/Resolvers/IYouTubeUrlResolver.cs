using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public interface IYouTubeItemResolver
{
    Task<FindEpisodeResponse?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext);
}