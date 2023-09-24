using System.Text.RegularExpressions;
using System.Web;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeChannelResolver : IYouTubeChannelResolver
{
    private readonly ILogger<YouTubeChannelResolver> _logger;
    private readonly YouTubeService _youTubeService;

    public YouTubeChannelResolver(YouTubeService youTubeService, ILogger<YouTubeChannelResolver> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<SearchResult?> FindChannel(string channelName, string mostRecentlyUploadVideoTitle)
    {
        mostRecentlyUploadVideoTitle = AlphaNumericOnly(mostRecentlyUploadVideoTitle);
        var channelsListRequest = _youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId,items/snippet/channelTitle";
        channelsListRequest.Q = channelName;
        channelsListRequest.MaxResults = 10;
        var channelsListResponse = await channelsListRequest.ExecuteAsync();
        foreach (var searchResult in channelsListResponse.Items)
        {
            var searchListRequest = _youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = 1;
            searchListRequest.ChannelId = searchResult.Snippet.ChannelId;
            searchListRequest.PageToken = " "; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            var searchListResponse = await searchListRequest.ExecuteAsync();
            var lastUpload = searchListResponse.Items.FirstOrDefault();
            if (lastUpload != null && AlphaNumericOnly(lastUpload.Snippet.Title) == mostRecentlyUploadVideoTitle)
            {
                return searchResult;
            }
        }

        return null;
    }

    private string AlphaNumericOnly(string str)
    {
        str = HttpUtility.HtmlDecode(str);
        return Regex.Replace(str, "[^a-zA-Z0-9]+", "", RegexOptions.Compiled).ToLower();
    }
}