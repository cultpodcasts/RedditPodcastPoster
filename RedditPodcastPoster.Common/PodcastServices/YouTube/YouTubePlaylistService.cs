using System.Text.Json;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Extensions;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubePlaylistService : IYouTubePlaylistService
{
    private const int MaxSearchResults = 5;
    private readonly ILogger<YouTubePlaylistService> _logger;
    private readonly YouTubeService _youTubeService;

    public YouTubePlaylistService(
        YouTubeService youTubeService,
        ILogger<YouTubePlaylistService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetPlaylistVideoSnippets)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{playlistId.PlaylistId}'.");
            return new GetPlaylistVideoSnippetsResponse(null);
        }

        var batchSize = MaxSearchResults;
        if (indexingContext.ReleasedSince.HasValue)
        {
            batchSize = 3;
        }

        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        var firstRun = true;
        var knownToBeInReverseOrder = false;
        while (
            nextPageToken != null &&
            (firstRun ||
             (knownToBeInReverseOrder && result.Last().Snippet.PublishedAtDateTimeOffset
                 .ReleasedSinceDate(indexingContext.ReleasedSince)) ||
             !knownToBeInReverseOrder
            ))
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
                return new GetPlaylistVideoSnippetsResponse(null);
            }

            if (firstRun)
            {
                firstRun = false;
                if (indexingContext.ReleasedSince.HasValue)
                {
                    knownToBeInReverseOrder = IsReverseDateOrdered(playlistItemsListResponse.Items);
                    if (knownToBeInReverseOrder)
                    {
                        batchSize = 1;
                        _logger.LogInformation(
                            $"Playlist '{playlistId.PlaylistId}' appears to be in reverse-date order. Setting batch-size to '{batchSize}'.");
                    }
                    else
                    {
                        batchSize = 10;
                        _logger.LogInformation(
                            $"Playlist '{playlistId.PlaylistId}' is not in reverse-date order. Setting batch-size to '{batchSize}'.");
                    }
                }
            }

            result.AddRange(playlistItemsListResponse.Items);
            nextPageToken = playlistItemsListResponse.NextPageToken;
        }

        if (result.Any())
        {
            _logger.LogInformation($"YOUTUBE: {nameof(GetPlaylistVideoSnippets)} - {JsonSerializer.Serialize(result)}");

            if (indexingContext.ReleasedSince != null)
            {
                result = result.Where(x =>
                    x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
            }
        }

        return new GetPlaylistVideoSnippetsResponse(result, !knownToBeInReverseOrder);
    }

    private static bool IsReverseDateOrdered(IEnumerable<PlaylistItem> source)
    {
        using var iterator = source.GetEnumerator();
        if (!iterator.MoveNext())
        {
            return true;
        }

        var current = iterator.Current.Snippet.PublishedAtDateTimeOffset;

        while (iterator.MoveNext())
        {
            var next = iterator.Current.Snippet.PublishedAtDateTimeOffset;
            if (current < next)
            {
                return false;
            }

            current = next;
        }

        return true;
    }
}