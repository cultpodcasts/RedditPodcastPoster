using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeSearchResultFinder
{
    Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(EpisodeModel episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext);
}
