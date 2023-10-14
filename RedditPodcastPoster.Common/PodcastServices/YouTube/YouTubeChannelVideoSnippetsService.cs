using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeChannelVideoSnippetsService : IYouTubeChannelVideoSnippetsService
{
    private static readonly ConcurrentDictionary<string, IList<SearchResult>> Cache = new();

    private readonly ILogger<YouTubeChannelVideoSnippetsService> _logger;
    private readonly YouTubeService _youTubeService;

    public YouTubeChannelVideoSnippetsService(YouTubeService youTubeService,
        ILogger<YouTubeChannelVideoSnippetsService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        _logger.LogInformation($"YOUTUBE: {nameof(GetLatestChannelVideoSnippets)} channelId: '{channelId.ChannelId}'.");

        if (Cache.TryGetValue(channelId.ChannelId, out var snippets))
        {
            return snippets;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetLatestChannelVideoSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
            return null;
        }

        var result = new List<SearchResult>();
        var nextPageToken = "";
        while (nextPageToken != null)
        {
            var searchListRequest = _youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = IYouTubePlaylistService.MaxSearchResults;
            searchListRequest.ChannelId = channelId.ChannelId;
            searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (indexingContext.ReleasedSince.HasValue)
            {
                searchListRequest.PublishedAfter =
                    string.Concat(indexingContext.ReleasedSince.Value.ToString("o", CultureInfo.InvariantCulture),
                        "Z");
            }

            SearchListResponse response;
            try
            {
                response = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return null;
            }

            result.AddRange(response.Items);
            nextPageToken = response.NextPageToken;
        }

        if (result.Any())
        {
            _logger.LogInformation(
                $"YOUTUBE: {nameof(GetLatestChannelVideoSnippets)} - {JsonSerializer.Serialize(result)}");
        }

        Cache[channelId.ChannelId] = result;

        return result;
    }

    public void Flush()
    {
        Cache.Clear();
    }
}