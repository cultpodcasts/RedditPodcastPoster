using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeSearchResultFinder
{
    Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext);
}
