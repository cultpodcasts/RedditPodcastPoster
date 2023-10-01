﻿using System.Globalization;
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

    public async Task<SearchResult?> FindChannel(string channelName, string mostRecentlyUploadVideoTitle,
        IndexOptions indexOptions)
    {
        if (indexOptions.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindChannel)}' as '{nameof(indexOptions.SkipYouTubeUrlResolving)}' is set. Channel-name: '{channelName}'.");
            return null;
        }

        mostRecentlyUploadVideoTitle = AlphaNumericOnly(mostRecentlyUploadVideoTitle);
        var channelsListRequest = _youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId,items/snippet/channelTitle";
        channelsListRequest.Q = channelName;
        channelsListRequest.MaxResults = 10;
        SearchListResponse channelsListResponse;
        try
        {
            channelsListResponse = await channelsListRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
            indexOptions.SkipYouTubeUrlResolving = true;
            return null;
        }

        foreach (var searchResult in channelsListResponse.Items)
        {
            var searchListRequest = _youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = 1;
            searchListRequest.ChannelId = searchResult.Snippet.ChannelId;
            searchListRequest.PageToken = " "; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (indexOptions.ReleasedSince.HasValue)
            {
                searchListRequest.PublishedAfter =
                    string.Concat(indexOptions.ReleasedSince.Value.ToString("o", CultureInfo.InvariantCulture),
                        "Z");
            }

            SearchListResponse searchListResponse;
            try
            {
                searchListResponse = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
                indexOptions.SkipYouTubeUrlResolving = true;
                return null;
            }

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