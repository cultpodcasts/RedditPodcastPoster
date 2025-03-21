using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class YouTubePlaylistService(
    ILogger<YouTubePlaylistService> logger)
    : IYouTubePlaylistService
{
    private const int MaxSearchResults = 5;
    private const string PrivateVideoTitle = "Private video";

    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        IYouTubeServiceWrapper youTubeService,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext, bool withContentDetails = false)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
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
        var requestScope = "snippet";
        if (withContentDetails)
        {
            requestScope += ",contentDetails";
        }

        while (
            nextPageToken != null &&
            (firstRun ||
             (knownToBeInReverseOrder && result.Last().Snippet.PublishedAtDateTimeOffset
                 .ReleasedSinceDate(indexingContext.ReleasedSince)) ||
             !knownToBeInReverseOrder
            ))
        {
            var playlistRequest = youTubeService.YouTubeService.PlaylistItems.List(requestScope);
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
                logger.LogError(ex,
                    $"Failed to use {nameof(youTubeService.YouTubeService)} obtaining playlist-video-snippets for playlist-id '{playlistId}'.");
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
                        logger.LogInformation(
                            $"Playlist '{playlistId.PlaylistId}' appears to be in reverse-date order. Setting batch-size to '{batchSize}'.");
                    }
                    else
                    {
                        batchSize = 10;
                        logger.LogInformation(
                            $"Playlist '{playlistId.PlaylistId}' is not in reverse-date order. Setting batch-size to '{batchSize}'.");
                    }
                }
            }

            result.AddRange(playlistItemsListResponse.Items.Where(x => x.Snippet.Title != PrivateVideoTitle));
            nextPageToken = playlistItemsListResponse.NextPageToken;
        }

        if (result.Any())
        {
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