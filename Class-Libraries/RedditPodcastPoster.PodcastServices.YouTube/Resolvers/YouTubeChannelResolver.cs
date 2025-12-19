using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public class YouTubeChannelResolver(
    IYouTubeServiceWrapper youTubeService,
    ILogger<YouTubeChannelResolver> logger)
    : IYouTubeChannelResolver
{
    public async Task<SearchResult?> FindChannelsSnippets(string channelName, string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext)
    {
        logger.LogInformation("YOUTUBE: Find-Channel for channel-name '{ChannelName}'.", channelName);
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{FindChannelsSnippetsName}' as '{IndexingContextSkipYouTubeUrlResolvingName}' is set. Channel-name: '{ChannelName}'.", nameof(FindChannelsSnippets), nameof(indexingContext.SkipYouTubeUrlResolving), channelName);
            return null;
        }

        mostRecentlyUploadVideoTitle = AlphaNumericOnly(mostRecentlyUploadVideoTitle);
        var channelsListRequest = youTubeService.YouTubeService.Search.List("snippet");
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
            logger.LogError(ex,
                "Failed to use {YouTubeServiceName} to obtain channel-snippets for channel-name '{ChannelName}'.", nameof(youTubeService.YouTubeService), channelName);
            indexingContext.SkipYouTubeUrlResolving = true;
            return null;
        }

        foreach (var searchResult in channelsListResponse.Items)
        {
            var searchListRequest = youTubeService.YouTubeService.Search.List("snippet");
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
                logger.LogError(ex,
                    "Failed to use {YouTubeServiceName} to obtain channel-snippets for channel-id '{SnippetChannelId}' obtained when searching for channel with name '{ChannelName}'.", nameof(youTubeService.YouTubeService), searchResult.Snippet.ChannelId, channelName);
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
                        "YOUTUBE: {FindChannelsSnippetsName} - {Serialize}", nameof(FindChannelsSnippets), JsonSerializer.Serialize(searchResult));
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