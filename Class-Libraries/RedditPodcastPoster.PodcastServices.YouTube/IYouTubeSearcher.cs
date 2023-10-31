using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeSearcher
{
    SearchResult? FindMatchingYouTubeVideo(
        Episode episode,
        IList<SearchResult> searchResults, 
        TimeSpan? youTubePublishDelay);
}