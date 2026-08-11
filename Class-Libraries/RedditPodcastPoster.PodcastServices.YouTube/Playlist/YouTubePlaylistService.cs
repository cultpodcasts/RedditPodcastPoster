using System.Net;
using Google;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class YouTubePlaylistService(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubePlaylistService> logger)
    : IYouTubePlaylistService
{
    private const int MaxSearchResults = 5;
    private const string PrivateVideoTitle = "Private video";

    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{nameofGetPlaylistVideoSnippets}' as '{nameofIndexingContextSkipYouTubeUrlResolving}' is set. Playlist-id: '{playlistId}'.",
                nameof(GetPlaylistVideoSnippets), nameof(indexingContext.SkipYouTubeUrlResolving),
                playlistId.PlaylistId);
            return new GetPlaylistVideoSnippetsResponse(null);
        }

        // Curated playlists carry no date information in their positions: new items may appear at
        // either end. Walk at max batch size (1 quota unit per 50 items) with a hard page cap
        // (see ArbitraryYouTubePlaylistWalk.MaxPages) and rely on the ReleasedSince window
        // filter below; skip the head-order probe entirely so the expensive-query flag is never
        // flipped by a meaningless head sample.
        var arbitraryOrder = playlistOrder == PlaylistOrder.Arbitrary;

        var batchSize = MaxSearchResults;
        if (arbitraryOrder)
        {
            batchSize = ArbitraryYouTubePlaylistWalk.BatchSize;
        }
        else if (indexingContext.ReleasedSince.HasValue)
        {
            batchSize = 3;
        }

        var result = new List<PlaylistItem>();
        var nextPageToken = "";
        var firstRun = true;
        var knownToBeInReverseOrder = false;
        bool? isExpensiveQuery = null;
        var pagesFetched = 0;
        // contentDetails is free on playlistItems.list (1 unit per page regardless of parts) and carries
        // videoPublishedAt, which the ReleasedSince filter below needs to window scheduled uploads on
        // their public release rather than the earlier added-to-playlist time.
        var requestScope = "snippet";
        if (withContentDetails || indexingContext.ReleasedSince.HasValue)
        {
            requestScope += ",contentDetails";
        }

        while (
            nextPageToken != null &&
            (firstRun || (knownToBeInReverseOrder && result.Any() && result.Last().Snippet.PublishedAtDateTimeOffset
                .ReleasedSinceDate(indexingContext.ReleasedSince)) || !knownToBeInReverseOrder))
        {
            if (arbitraryOrder &&
                ArbitraryYouTubePlaylistWalk.ShouldTripCircuitBreaker(pagesFetched, nextPageToken))
            {
                logger.LogError(
                    ArbitraryYouTubePlaylistWalk.CircuitBreakerTrippedMessageTemplate,
                    playlistId.PlaylistId,
                    pagesFetched,
                    ArbitraryYouTubePlaylistWalk.MaxPages,
                    indexingContext.ReleasedSince,
                    nextPageToken);
                break;
            }

            var playlistRequest = youTubeServiceWrapper.YouTubeService.PlaylistItems.List(requestScope);
            playlistRequest.PlaylistId = playlistId.PlaylistId;
            playlistRequest.MaxResults = batchSize;
            playlistRequest.PageToken = nextPageToken;

            PlaylistItemListResponse playlistItemsListResponse;
            try
            {
                playlistItemsListResponse = await playlistRequest.ExecuteAsync();
                await quotaUsageTracker.RecordQuotaConsumedAsync(
                    youTubeServiceWrapper.CurrentApplication,
                    youTubeServiceWrapper.Usage,
                    YouTubeQuotaOperation.PlaylistItemsList,
                    YouTubeQuotaCosts.PlaylistItemsList);
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == HttpStatusCode.Forbidden && ex.Message.Contains("exceeded") &&
                    ex.Message.Contains("quota"))
                {
                    logger.LogWarning(ex, "Exceeded Quota occurred.");
                    await quotaUsageTracker.RecordQuotaHitAsync(
                        youTubeServiceWrapper.CurrentApplication,
                        youTubeServiceWrapper.Usage,
                        YouTubeQuotaOperation.PlaylistItemsList);
                    throw new YouTubeQuotaException();
                }

                if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    logger.LogError(ex,
                        "YouTube playlist '{playlistId}' was not found (HTTP NotFound). The playlist may have been deleted or made private — update the podcast YouTubePlaylistId to a current playlist id. Skipping further YouTube URL resolving for this run.",
                        playlistId.PlaylistId);
                    await quotaUsageTracker.RecordNonQuotaErrorAsync();
                    indexingContext.SkipYouTubeUrlResolving = true;
                    return new GetPlaylistVideoSnippetsResponse(null,
                        Failure: YouTubePlaylistFetchFailure.NotFound);
                }

                logger.LogError(ex,
                    "Unrecognised google-api-exception. Failed to use {nameofYouTubeServiceWrapperYouTubeService} to obtain playlist-snippets for playlist-id '{playlistId}'.",
                    nameof(youTubeServiceWrapper.YouTubeService), playlistId.PlaylistId);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                indexingContext.SkipYouTubeUrlResolving = true;
                return new GetPlaylistVideoSnippetsResponse(null,
                    Failure: YouTubePlaylistFetchFailure.ApiError);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to use {nameofYouTubeServiceWrapperYouTubeService} obtaining playlist-video-snippets for playlist-id '{playlistId}'.",
                    nameof(youTubeServiceWrapper.YouTubeService), playlistId.PlaylistId);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                indexingContext.SkipYouTubeUrlResolving = true;
                return new GetPlaylistVideoSnippetsResponse(null,
                    Failure: YouTubePlaylistFetchFailure.ApiError);
            }

            if (firstRun)
            {
                firstRun = false;
                if (arbitraryOrder)
                {
                    logger.LogInformation(
                        "Playlist '{playlistId}' is declared arbitrary-order; walking with batch-size '{batchSize}' capped at '{maxPages}' pages.",
                        playlistId.PlaylistId, batchSize, ArbitraryYouTubePlaylistWalk.MaxPages);
                }
                // Always probe order when a date window is present — even for known-expensive
                // playlists — so a flip back to newest-first can clear the sticky flag.
                else if (indexingContext.ReleasedSince.HasValue)
                {
                    var sample = playlistItemsListResponse.Items?
                        .Where(x => x?.Snippet?.PublishedAtDateTimeOffset != null)
                        .Take(YouTubeExpensiveQueryFlag.MinimumOrderSampleSize + 6)
                        .ToList() ?? [];
                    if (sample.Count >= YouTubeExpensiveQueryFlag.MinimumOrderSampleSize)
                    {
                        knownToBeInReverseOrder = PlaylistItemOrdering.IsReverseDateOrdered(sample);
                        isExpensiveQuery = !knownToBeInReverseOrder;
                        if (knownToBeInReverseOrder)
                        {
                            batchSize = expensivePlaylist ? 10 : 1;
                            logger.LogInformation(
                                "Playlist '{playlistId}' appears to be in reverse-date order. Setting batch-size to '{batchSize}'.",
                                playlistId.PlaylistId, batchSize);
                        }
                        else
                        {
                            batchSize = 10;
                            logger.LogInformation(
                                "Playlist '{playlistId}' is not in reverse-date order. Setting batch-size to '{batchSize}'.",
                                playlistId.PlaylistId, batchSize);
                        }
                    }
                    else if (expensivePlaylist)
                    {
                        batchSize = 10;
                    }
                }
                else if (expensivePlaylist)
                {
                    batchSize = 10;
                }
            }

            result.AddRange((playlistItemsListResponse.Items ?? [])
                .Where(x => x.Snippet.Title != PrivateVideoTitle));
            nextPageToken = playlistItemsListResponse.NextPageToken;
            pagesFetched++;
        }

        if (result.Any() && indexingContext.ReleasedSince != null)
        {
            result = result.Where(x =>
                x.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
        }

        return new GetPlaylistVideoSnippetsResponse(result, isExpensiveQuery);
    }

    public async Task<GetPlaylistInfoResponse> GetPlaylistInfo(IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            throw new InvalidOperationException(
                $"Error obtaining playlist-snippet for playlist-id '{playlistId.PlaylistId}'. {nameof(indexingContext.SkipYouTubeUrlResolving)} is set.");
        }

        var playlistRequest = youTubeServiceWrapper.YouTubeService.Playlists.List("snippet");
        playlistRequest.Id = playlistId.PlaylistId;
        playlistRequest.MaxResults = 1;

        var playlistResponse = await playlistRequest.ExecuteAsync();
        await quotaUsageTracker.RecordQuotaConsumedAsync(
            youTubeServiceWrapper.CurrentApplication,
            youTubeServiceWrapper.Usage,
            YouTubeQuotaOperation.PlaylistsList,
            YouTubeQuotaCosts.PlaylistsList);

        if (playlistResponse == null || !playlistResponse.Items.Any())
        {
            throw new InvalidOperationException(
                $"Error obtaining playlist-snippet for playlist-id '{playlistId.PlaylistId}'. No result.");
        }

        if (playlistResponse.Items.Count > 1)
        {
            throw new InvalidOperationException(
                $"Error obtaining playlist-snippet for playlist-id '{playlistId.PlaylistId}'. Multiple results: '{playlistResponse.Items.Count}'.");
        }

        var snippet = playlistResponse.Items.First().Snippet;
        return new GetPlaylistInfoResponse(snippet.Title, snippet.Description);
    }

}
