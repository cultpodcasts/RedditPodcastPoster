using System.Globalization;
using System.Text;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

        if (result.Any())
        {
            _logger.LogInformation(
                $"YOUTUBE: {nameof(GetLatestChannelVideoSnippets)} - {JsonSerializer.Serialize(result)}");
        }

        return result;
    }

    public async Task<IList<Video>?> GetVideoContentDetails(
        IEnumerable<string> videoIds,
        IndexingContext indexingContext,
        bool withSnippets = false)
    {
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
                var contentdetails = "contentDetails";
                if (withSnippets)
                {
                    contentdetails = "snippet," + contentdetails;
                }

                request = _youTubeService.Videos.List(contentdetails);
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

        if (result.Any())
        {
            _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - {JsonSerializer.Serialize(result)}");
        }

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

    public async Task<Channel?> GetChannelContentDetails(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false)
    {
        _logger.LogInformation($"YOUTUBE: Get channel for channel-id {channelId}.");
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetChannelContentDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
            return null;
        }

        var requestScope = "contentDetails";
        if (withSnippets)
        {
            requestScope = "snippet," + requestScope;
        }

        if (withContentOwnerDetails)
        {
            requestScope += ",contentOwnerDetails";
        }

        var listRequest = _youTubeService.Channels.List(requestScope);
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

        if (result.Items.Any())
        {
            try
            {
                var sb = new StringBuilder();
                var jsonSerialiser = new Newtonsoft.Json.JsonSerializer();
                await using var jsonWriter = new JsonTextWriter(new StringWriter(sb));
                jsonSerialiser.Serialize(jsonWriter, result);
                _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - {sb}");
            }
            catch
            {
                _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - Could not serialise response.");
            }
        }

        return result.Items.SingleOrDefault();
    }

    public async Task<IList<PlaylistItem>?> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId,
        IndexingContext indexingContext)
    {
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
        var firstRun = true;
        while (nextPageToken != null && (firstRun || ( result.LastOrDefault() != null && result.Last().Snippet
                   .PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince))))
        {
            firstRun = false;
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

        if (result.Any())
        {
            _logger.LogInformation($"YOUTUBE: {nameof(GetPlaylistVideoSnippets)} - {JsonSerializer.Serialize(result)}");
        }

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