using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeItemResolver
{
    Task<FindEpisodeResponse?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext);
}

public record FindEpisodeResponse(SearchResult SearchResult, Video? Video= null);