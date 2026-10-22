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
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class YouTubePlaylistService(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubePlaylistService> logger)
    : IYouTubePlaylistService
{
    /// <summary>
    /// Page size for every playlistItems.list walk: the YouTube maximum. The call costs 1 quota unit per
    /// page whatever maxResults is, so smaller pages only add calls.
    /// </summary>
    public const int PlaylistPageSize = 50;

    /// <summary>
    /// Leading dated items used for the newest-first probe. Kept at the old first-page size (3) so the
    /// move to 50-item pages does not change how playlists are classified (and so flip stored flags).
    /// </summary>
    public const int OrderProbeSampleSize = 3;

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
            return new GetPlaylistVideoSnippetsResponse(null, Failure: YouTubePlaylistFetchFailure.Skipped);
        }

        // Curated playlists carry no date information in their positions: new items may appear at
        // either end. Walk with a hard page cap (see ArbitraryYouTubePlaylistWalk.MaxPages) and rely
        // on the ReleasedSince window filter below; skip the head-order probe entirely so the
        // expensive-query flag is never flipped by a meaningless head sample.
        var arbitraryOrder = playlistOrder == PlaylistOrder.Arbitrary;

        // Every walk pages at the YouTube maximum. `expensivePlaylist` used to pick 10 vs 1 items per
        // page after a newest-first probe; playlistItems.list is 1 unit per page regardless of size, so
        // 50 is never more calls than 10 or 1 and the flag no longer changes the request (removal: #1104).
        const int batchSize = PlaylistPageSize;

        var result = new List<PlaylistItem>();
        string? nextPageToken = "";
        var firstRun = true;
        var knownToBeInReverseOrder = false;
        bool? isExpensiveQuery = null;
        var walkGuard = new YouTubePageWalkGuard(batchSize);
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
                ArbitraryYouTubePlaylistWalk.ShouldTripCircuitBreaker(walkGuard.PagesFetched, nextPageToken))
            {
                logger.LogError(
                    ArbitraryYouTubePlaylistWalk.CircuitBreakerTrippedMessageTemplate,
                    playlistId.PlaylistId,
                    walkGuard.PagesFetched,
                    ArbitraryYouTubePlaylistWalk.MaxPages,
                    indexingContext.ReleasedSince,
                    nextPageToken);
                break;
            }

            var requestedPageToken = nextPageToken;
            PlaylistItemListResponse playlistItemsListResponse;
            try
            {
                var playlistRequest = youTubeServiceWrapper.YouTubeService.PlaylistItems.List(requestScope);
                playlistRequest.PlaylistId = playlistId.PlaylistId;
                playlistRequest.MaxResults = batchSize;
                playlistRequest.PageToken = nextPageToken;

                indexingContext.ReportStage("youtube-playlist-page",
                    $"playlist-id={playlistId.PlaylistId} page={walkGuard.PagesFetched + 1} batch-size={batchSize}");
                playlistItemsListResponse = await playlistRequest.ExecuteAsync(indexingContext.CancellationToken);
                await quotaUsageTracker.RecordQuotaConsumedAsync(
                    youTubeServiceWrapper.CurrentApplication,
                    youTubeServiceWrapper.Usage,
                    YouTubeQuotaOperation.PlaylistItemsList,
                    YouTubeQuotaCosts.PlaylistItemsList);
            }
            catch (OperationCanceledException) when (indexingContext.CancellationToken.IsCancellationRequested)
            {
                // The podcast's time budget elapsed; let the indexer loop log the stage and move on.
                throw;
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
                        "YouTube playlist '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}) was not found (HTTP NotFound). The playlist may have been deleted or made private — update the podcast YouTubePlaylistId to a current playlist id.",
                        playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier);
                    await quotaUsageTracker.RecordNonQuotaErrorAsync();
                    return new GetPlaylistVideoSnippetsResponse(null,
                        Failure: YouTubePlaylistFetchFailure.NotFound);
                }

                logger.LogError(ex,
                    "Unrecognised google-api-exception. Failed to use {nameofYouTubeServiceWrapperYouTubeService} to obtain playlist-snippets for playlist-id '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}).",
                    nameof(youTubeServiceWrapper.YouTubeService), playlistId.PlaylistId, playlistId.Source,
                    playlistId.SourceIdentifier);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                return new GetPlaylistVideoSnippetsResponse(null,
                    Failure: YouTubePlaylistFetchFailure.ApiError);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to use {nameofYouTubeServiceWrapperYouTubeService} obtaining playlist-video-snippets for playlist-id '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}).",
                    nameof(youTubeServiceWrapper.YouTubeService), playlistId.PlaylistId, playlistId.Source,
                    playlistId.SourceIdentifier);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
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
                        .Take(OrderProbeSampleSize)
                        .Where(x => x?.Snippet?.PublishedAtDateTimeOffset != null)
                        .ToList() ?? [];
                    if (sample.Count >= YouTubeExpensiveQueryFlag.MinimumOrderSampleSize)
                    {
                        knownToBeInReverseOrder = PlaylistItemOrdering.IsReverseDateOrdered(sample);
                        isExpensiveQuery = !knownToBeInReverseOrder;
                        logger.LogInformation(
                            knownToBeInReverseOrder
                                ? "Playlist '{playlistId}' appears to be in reverse-date order; stopping once items leave the window. Batch-size '{batchSize}'."
                                : "Playlist '{playlistId}' is not in reverse-date order; walking to the end. Batch-size '{batchSize}'.",
                            playlistId.PlaylistId, batchSize);
                    }
                }
            }

            var pageItems = playlistItemsListResponse.Items ?? [];
            result.AddRange(pageItems.Where(x => x.Snippet.Title != PrivateVideoTitle));
            nextPageToken = playlistItemsListResponse.NextPageToken;

            var stop = walkGuard.Observe(
                requestedPageToken,
                pageItems.Select(x => x?.Snippet?.ResourceId?.VideoId ?? x?.ContentDetails?.VideoId).ToList(),
                nextPageToken,
                playlistItemsListResponse.PageInfo?.TotalResults);
            if (stop != YouTubePageWalkStopReason.None)
            {
                walkGuard.LogStop(logger, stop, "playlist-items", playlistId.PlaylistId,
                    playlistId.Source.ToString(), playlistId.SourceIdentifier, result.Count);
                break;
            }
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

        PlaylistListResponse? playlistResponse;
        try
        {
            var response = await playlistRequest.ExecuteAsync();
            await quotaUsageTracker.RecordQuotaConsumedAsync(
                youTubeServiceWrapper.CurrentApplication,
                youTubeServiceWrapper.Usage,
                YouTubeQuotaOperation.PlaylistsList,
                YouTubeQuotaCosts.PlaylistsList);
            playlistResponse = response;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
                                            ex.Message.Contains("exceeded") &&
                                            ex.Message.Contains("quota"))
        {
            logger.LogWarning(ex, "Exceeded Quota occurred.");
            await quotaUsageTracker.RecordQuotaHitAsync(
                youTubeServiceWrapper.CurrentApplication,
                youTubeServiceWrapper.Usage,
                YouTubeQuotaOperation.PlaylistsList);
            throw new YouTubeQuotaException();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to use {nameofYouTubeServiceWrapperYouTubeService} obtaining playlist-info for playlist-id '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}).",
                nameof(youTubeServiceWrapper.YouTubeService), playlistId.PlaylistId, playlistId.Source,
                playlistId.SourceIdentifier);
            throw;
        }

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
