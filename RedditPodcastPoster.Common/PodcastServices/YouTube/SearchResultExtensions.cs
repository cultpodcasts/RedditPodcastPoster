using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public static class SearchResultExtensions
{
    public static Uri ToYouTubeUrl(this SearchResult matchedYouTubeVideo)
    {
        return new Uri($"https://www.youtube.com/watch?v={matchedYouTubeVideo.Id.VideoId}");
    }
}