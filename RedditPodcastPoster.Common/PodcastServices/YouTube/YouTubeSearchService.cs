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

    public async Task<IList<SearchResult>?> GetLatestChannelVideos(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetLatestChannelVideos)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
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

        return result;
    }

    public async Task<IList<Video>?> GetVideoDetails(IEnumerable<string> videoIds, IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetVideoDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Video-ids: '{string.Join(",", videoIds)}'.");
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
                var request = _youTubeService.Videos.List("snippet,contentDetails");
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

    public async Task<Channel?> GetChannel(YouTubeChannelId channelId, IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetChannel)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
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

        return result.Items.SingleOrDefault();
    }

    public async Task<IList<PlaylistItem>?> GetPlaylist(YouTubePlaylistId playlistId, IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetPlaylist)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{playlistId.PlaylistId}'.");
            return null;
        }

        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        while (nextPageToken != null &&
               ReleasedSinceDate(result.LastOrDefault()?.Snippet.PublishedAtDateTimeOffset,
                   indexingContext.ReleasedSince))
        {
            var playlistRequest = _youTubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = playlistId.PlaylistId;
            playlistRequest.MaxResults = IYouTubeSearchService.MaxSearchResults;
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

        return result;
    }

    private bool ReleasedSinceDate(DateTimeOffset? releaseDate, DateTime? date)
    {
        if (releaseDate.HasValue && date.HasValue)
        {
            return releaseDate.Value.ToUniversalTime() > date.Value.ToUniversalTime();
        }

        return true;
    }
}