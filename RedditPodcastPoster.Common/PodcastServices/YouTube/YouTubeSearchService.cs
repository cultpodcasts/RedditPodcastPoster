using System.Globalization;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeSearchService : IYouTubeSearchService
{
    private readonly ILogger<YouTubeSearchService> _logger;

    private readonly YouTubeService _youTubeService;

    public YouTubeSearchService(YouTubeService youTubeService,
        ILogger<YouTubeSearchService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<IList<SearchResult>> GetLatestChannelVideos(
        string youTubeChannelId,
        DateTime? publishedSince)
    {
        var result = new List<SearchResult>();
        var nextPageToken = "";
        while (nextPageToken != null)
        {
            var searchListRequest = _youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = IYouTubeSearchService.MaxSearchResults;
            searchListRequest.ChannelId = youTubeChannelId;
            searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
            searchListRequest.Type = "video";
            searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (publishedSince.HasValue)
            {
                searchListRequest.PublishedAfter =
                    string.Concat(publishedSince.Value.ToString("o", CultureInfo.InvariantCulture),
                        "Z");
            }

            var response = await searchListRequest.ExecuteAsync();
            result.AddRange(response.Items);
            nextPageToken = response.NextPageToken;
        }

        return result;
    }

    public async Task<IList<Video>> GetVideoDetails(IEnumerable<string> videoIds)
    {
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
                var response = await request.ExecuteAsync();
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

    public async Task FindChannel(string channelName)
    {
        var channelsListRequest = _youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId";
        channelsListRequest.Q = channelName;
        var channelsListResponse = await channelsListRequest.ExecuteAsync();
        throw new NotImplementedException("method not fully implemented");
    }

    public async Task<Channel?> GetChannel(string channelId)
    {
        var listRequest = _youTubeService.Channels.List("snippet,contentDetails,contentOwnerDetails");
        listRequest.Id = channelId;
        var result = await listRequest.ExecuteAsync();
        return result.Items.SingleOrDefault();
    }

    public async Task<IList<PlaylistItem>> GetPlaylist(string playlistId, DateTime? releasedSince)
    {
        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        while (nextPageToken != null &&
               ReleasedSinceDate(result.LastOrDefault()?.Snippet.PublishedAtDateTimeOffset, releasedSince))
        {
            var playlistRequest = _youTubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = playlistId;
            playlistRequest.MaxResults = 50;
            playlistRequest.PageToken = nextPageToken;
            var playlistItemsListResponse = await playlistRequest.ExecuteAsync();
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