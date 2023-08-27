using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public static class SearchResultExtensions
{
    private static Uri ToYouTubeUrl(string videoId)
    {
        return new Uri($"https://www.youtube.com/watch?v={videoId}");
    }

    public static Uri ToYouTubeUrl(this SearchResult matchedYouTubeVideo)
    {
        return ToYouTubeUrl(matchedYouTubeVideo.Id.VideoId);
    }

    public static Uri ToYouTubeUrl(this PlaylistItemSnippet matchedYouTubeVideo)
    {
        return ToYouTubeUrl(matchedYouTubeVideo.ResourceId.VideoId);
    }
}