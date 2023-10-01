using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeItemResolver
{
    Task<SearchResult?> FindEpisode(EnrichmentRequest request, IndexOptions indexOptions);
}