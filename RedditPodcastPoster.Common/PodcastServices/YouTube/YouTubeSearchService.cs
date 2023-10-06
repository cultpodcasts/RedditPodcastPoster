using System.Diagnostics;
using System.Globalization;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeSearchService : IYouTubeSearchService
{
    private readonly ILogger<YouTubeSearchService> _logger;

    private readonly YouTubeService _youTubeService;

    public YouTubeSearchService(
        YouTubeService youTubeService,
        ILogger<YouTubeSearchService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        _logger.LogInformation($"YOUTUBE: Query for latest {IYouTubeSearchService.MaxSearchResults} videos channel-id {channelId}, published since {indexingContext.ReleasedSince:R}.");
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
            searchListRequest.MaxResults = IYouTubeSearchService.MaxSearchResults;
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
        _logger.LogInformation($"YOUTUBE: {nameof(GetLatestChannelVideoSnippets)} - {System.Text.Json.JsonSerializer.Serialize(result)}");
        return result;
    }

    public async Task<IList<Video>?> GetVideoContentDetails(IEnumerable<string> videoIds, IndexingContext indexingContext)
    {
        _logger.LogInformation($"YOUTUBE: Get Video details for videos {String.Join(",", videoIds)}");

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetVideoContentDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Video-ids: '{string.Join(",", videoIds)}'.");
            return null;
        }

        var result = new List<Video>();
        var nextPageToken = "";
        var batch = 0;
        var batchVideoIds = videoIds.Take(IYouTubeSearchService.MaxSearchResults);
        while (batchVideoIds.Any())
        {
            while (nextPageToken != null)
            {
                VideosResource.ListRequest request;
                request = _youTubeService.Videos.List("contentDetails");
                request.Id = string.Join(",", batchVideoIds);
                request.MaxResults = IYouTubeSearchService.MaxSearchResults;
                VideoListResponse response;
                try
                {
                    response = await request.ExecuteAsync();
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

            nextPageToken = "";
            batch++;
            batchVideoIds = videoIds.Skip(batch * IYouTubeSearchService.MaxSearchResults)
                .Take(IYouTubeSearchService.MaxSearchResults);
        }
        _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - {System.Text.Json.JsonSerializer.Serialize(result)}");
        return result;
    }

    public async Task FindChannel(string channelName, IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindChannel)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-name: '{channelName}'.");
            return;
        }

        var channelsListRequest = _youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId";
        channelsListRequest.Q = channelName;
        SearchListResponse channelsListResponse;
        try
        {
            channelsListResponse = await channelsListRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return;
        }

        throw new NotImplementedException("method not fully implemented");
    }

    public async Task<Channel?> GetChannelSnippetsContentDetailsContentOwnerDetails(YouTubeChannelId channelId, IndexingContext indexingContext)
    {
        _logger.LogInformation($"YOUTUBE: Get channel for channel-id {channelId}.");
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetChannelSnippetsContentDetailsContentOwnerDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
            return null;
        }

        var listRequest = _youTubeService.Channels.List("snippet,contentDetails,contentOwnerDetails");
        listRequest.Id = channelId.ChannelId;
        ChannelListResponse result;
        try
        {
            result = await listRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return null;
        }

        _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - {System.Text.Json.JsonSerializer.Serialize(result)}");
        return result.Items.SingleOrDefault();
    }

    public async Task<IList<PlaylistItem>?> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId, IndexingContext indexingContext)
    {
        _logger.LogInformation($"YOUTUBE: Get playlist for playlist-id {playlistId} - items released since {indexingContext.ReleasedSince:R}");
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetPlaylistVideoSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{playlistId.PlaylistId}'.");
            return null;
        }

        var batchSize = IYouTubeSearchService.MaxSearchResults;
        if (indexingContext.ReleasedSince.HasValue)
        {
            batchSize = 1;
        }

        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        while (nextPageToken != null && result.LastOrDefault()!=null && result.Last().Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince))
        {
            var playlistRequest = _youTubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = playlistId.PlaylistId;
            playlistRequest.MaxResults = batchSize;
            playlistRequest.PageToken = nextPageToken;

            PlaylistItemListResponse playlistItemsListResponse;
            try
            {
                playlistItemsListResponse = await playlistRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return null;
            }

            result.AddRange(playlistItemsListResponse.Items);
            nextPageToken = playlistItemsListResponse.NextPageToken;
        }
        _logger.LogInformation($"YOUTUBE: {nameof(GetPlaylistVideoSnippets)} - {System.Text.Json.JsonSerializer.Serialize(result)}");
        return result;
    }


}

public static class DateTimeOffsetExtensions
{
    public static bool ReleasedSinceDate(this DateTimeOffset? releaseDate, DateTime? date)
    {
        if (releaseDate.HasValue && date.HasValue)
        {
            return releaseDate.Value.ToUniversalTime() >= date.Value.ToUniversalTime();
        }

        return true;
    }
}