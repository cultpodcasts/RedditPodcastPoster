using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeItemResolver
{
    Task<SearchResult?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext);
}