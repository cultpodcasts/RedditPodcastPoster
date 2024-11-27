using System.Collections.Concurrent;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeChannelVideoSnippetsService(
    YouTubeServiceWrapper youTubeService,
    ILogger<YouTubeChannelVideoSnippetsService> logger)
    : IYouTubeChannelVideoSnippetsService
{
    private const int MaxSearchResults = 5;
    private static readonly ConcurrentDictionary<string, IList<SearchResult>> Cache = new();

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        if (Cache.TryGetValue(channelId.ChannelId, out var snippets))
        {
            return snippets;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetLatestChannelVideoSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
            return null;
        }

        var result = new List<SearchResult>();
        var nextPageToken = "";
        var searchListRequest = youTubeService.YouTubeService.Search.List("snippet");
        searchListRequest.MaxResults = MaxSearchResults;
        searchListRequest.ChannelId = channelId.ChannelId;
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        if (indexingContext.ReleasedSince.HasValue)
        {
            searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
        }

        //upcoming
        while (nextPageToken != null)
        {
            searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging

            SearchListResponse response;
            try
            {
                response = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    $"Failed to use {nameof(youTubeService.YouTubeService)} with api-key-name '{youTubeService.ApiKeyName}' to obtain latest-channel-snippets for channel-id '{channelId.ChannelId}'.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return result;
            }

            var responseItems = response.Items.Where(x =>
                x.Snippet.LiveBroadcastContent != "upcoming" && x.Snippet.LiveBroadcastContent != "live");
            result.AddRange(responseItems);
            nextPageToken = response.NextPageToken;
        }

        Cache[channelId.ChannelId] = result;

        return result;
    }

    public void Flush()
    {
        Cache.Clear();
    }
}