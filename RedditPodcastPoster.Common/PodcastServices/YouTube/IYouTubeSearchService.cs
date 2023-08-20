using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 20;

    Task<SearchListResponse> GetLatestChannelVideos(Podcast podcast, DateTime? publishedSince,
        int maxResults = MaxSearchResults, string pageToken = " ");

    Task FindChannel(string channelName);
    Task<VideoListResponse> GetVideoDetails(IEnumerable<string> videoIds);
}