﻿using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class SearchResultExtensions
{
    public static Uri ToYouTubeUrl(string videoId)
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

    public static Uri ToYouTubeUrl(this Google.Apis.YouTube.v3.Data.Video video)
    {
        return ToYouTubeUrl(video.Id);
    }
}