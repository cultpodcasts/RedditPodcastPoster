using System.Globalization;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeSearchService : IYouTubeSearchService
{
    private readonly ILogger<YouTubeSearchService> _logger;

    private readonly YouTubeService _youTubeService;

    public YouTubeSearchService(YouTubeService youTubeService,
        ILogger<YouTubeSearchService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<SearchListResponse> GetLatestChannelVideos(
        Podcast podcast,
        DateTime? publishedSince,
        int maxResults = IYouTubeSearchService.MaxSearchResults,
        string pageToken = " ")
    {
        var searchListRequest = _youTubeService.Search.List("snippet");
        searchListRequest.MaxResults = maxResults;
        searchListRequest.ChannelId = podcast.YouTubeChannelId;
        searchListRequest.PageToken = pageToken; // or searchListResponse.NextPageToken if paging
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        if (publishedSince.HasValue)
        {
            searchListRequest.PublishedAfter =
                string.Concat(publishedSince.Value.ToString("o", CultureInfo.InvariantCulture),
                    "Z");
        }

        return await searchListRequest.ExecuteAsync();
    }

    public async Task<VideoListResponse> GetVideoDetails(IEnumerable<string> videoIds)
    {
        var request = _youTubeService.Videos.List("snippet, contentDetails");
        request.Id = string.Join(",", videoIds);
        request.MaxResults = videoIds.Count();
        return await request.ExecuteAsync();
    }

    public async Task FindChannel(string channelName)
    {
        var channelsListRequest = _youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId";
        channelsListRequest.Q = channelName;
        var channelsListResponse = await channelsListRequest.ExecuteAsync();
        throw new NotImplementedException("method not fully implemented");
    }
}