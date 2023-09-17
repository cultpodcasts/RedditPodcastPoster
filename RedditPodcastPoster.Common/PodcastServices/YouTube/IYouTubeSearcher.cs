using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearcher
{
    SearchResult? FindMatchingYouTubeVideo(
        Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan youTubePublishingDelay);
}