using System.Collections.Concurrent;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeChannelVideoSnippetsService(
    YouTubeService youTubeService,
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
        while (nextPageToken != null)
        {
            var searchListRequest = youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = MaxSearchResults;
            searchListRequest.ChannelId = channelId.ChannelId;
            searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (indexingContext.ReleasedSince.HasValue)
            {
                searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
            }

            SearchListResponse response;
            try
            {
                response = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return result;
            }

            result.AddRange(response.Items);
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