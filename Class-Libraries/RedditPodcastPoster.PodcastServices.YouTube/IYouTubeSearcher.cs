using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeSearcher
{
    Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext);
}