using System.Text.Json;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubePlaylistService : IYouTubePlaylistService
{
    private readonly ILogger<YouTubePlaylistService> _logger;

    private readonly YouTubeService _youTubeService;

    public YouTubePlaylistService(
        YouTubeService youTubeService,
        ILogger<YouTubePlaylistService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<IList<PlaylistItem>?> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetPlaylistVideoSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{playlistId.PlaylistId}'.");
            return null;
        }

        var batchSize = IYouTubePlaylistService.MaxSearchResults;
        if (indexingContext.ReleasedSince.HasValue)
        {
            batchSize = 1;
        }

        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        var firstRun = true;
        while (nextPageToken != null && (firstRun || (result.LastOrDefault() != null && result.Last().Snippet
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