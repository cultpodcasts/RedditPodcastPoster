using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface ISearchResultFinder
{
    Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(RedditPodcastPoster.Models.V2.Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext);
}
