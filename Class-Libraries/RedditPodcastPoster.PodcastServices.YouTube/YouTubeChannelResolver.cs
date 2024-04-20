using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeChannelResolver(
    YouTubeService youTubeService,
    ILogger<YouTubeChannelResolver> logger)
    : IYouTubeChannelResolver
{
    public async Task<SearchResult?> FindChannelsSnippets(string channelName, string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext)
    {
        logger.LogInformation($"YOUTUBE: Find-Channel for channel-name '{channelName}'.");
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindChannelsSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-name: '{channelName}'.");
            return null;
        }

        mostRecentlyUploadVideoTitle = AlphaNumericOnly(mostRecentlyUploadVideoTitle);
        var channelsListRequest = youTubeService.Search.List("snippet");
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
            logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return null;
        }

        foreach (var searchResult in channelsListResponse.Items)
        {
            var searchListRequest = youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = 1;
            searchListRequest.ChannelId = searchResult.Snippet.ChannelId;
            searchListRequest.PageToken = " "; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (indexingContext.ReleasedSince.HasValue)
            {
                searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
            }

            SearchListResponse searchListResponse;
            try
            {
                searchListResponse = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return null;
            }

            var lastUpload = searchListResponse.Items.FirstOrDefault();
            if (lastUpload != null)
            {
                var alphaNumericOnly = AlphaNumericOnly(lastUpload.Snippet.Title);
                if (alphaNumericOnly == mostRecentlyUploadVideoTitle)
                {
                    logger.LogInformation(
                        $"YOUTUBE: {nameof(FindChannelsSnippets)} - {JsonSerializer.Serialize(searchResult)}");
                    return searchResult;
                }
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