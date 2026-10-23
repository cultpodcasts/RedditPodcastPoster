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

    /// <summary>
    /// Extra <c>playlistItems.list</c> pages (1 quota unit each) a newest-first walk may read after the last
    /// item's added-at has left <see cref="IndexingContext.ReleasedSince"/>. The walk continues only while the
    /// page just read still has an item inside the indexing window (later of added-at and video-published-at),
    /// and never more than this many pages past that added-at stop. See docs/youtube-playlist-order.md.
    /// </summary>
    public const int PublishedWindowExtraPages = 2;

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
        var stopReason = YouTubePageWalkStopReason.None;
        var circuitBreakerTripped = false;
        var publishedWindowExtraPagesUsed = 0;
        List<PlaylistItem> lastPagePublicItems = [];
        // contentDetails is free on playlistItems.list (1 unit per page regardless of parts) and carries
        // videoPublishedAt, which the ReleasedSince filter below needs to window scheduled uploads on
        // their public release rather than the earlier added-to-playlist time.
        var requestScope = "snippet";
        if (withContentDetails || indexingContext.ReleasedSince.HasValue)
        {
            requestScope += ",contentDetails";
        }

        // Newest-first playlists are ordered by added-at. Stopping on the last item's added-at misses a video
        // that was added before the window (so it sits on a later page) but published inside it. After added-at
        // leaves the window, keep reading while the page just read still has an indexing-window date inside the
        // window, and stop after PublishedWindowExtraPages of those pages.
        bool ShouldContinueAfterPage()
        {
            if (!knownToBeInReverseOrder)
            {
                return true;
            }

            if (!result.Any())
            {
                return false;
            }

            var lastAddedInWindow = result.Last().Snippet.PublishedAtDateTimeOffset
                .ReleasedSinceDate(indexingContext.ReleasedSince);
            if (lastAddedInWindow)
            {
                return true;
            }

            if (publishedWindowExtraPagesUsed >= PublishedWindowExtraPages ||
                !lastPagePublicItems.Any(item =>
                    item.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince)))
            {
                return false;
            }

            publishedWindowExtraPagesUsed++;
            if (publishedWindowExtraPagesUsed == 1)
            {
                logger.LogInformation(
                    "Playlist '{playlistId}' added-at left the indexing window; reading up to {extraPages} further page(s) for items published inside the window.",
                    playlistId.PlaylistId, PublishedWindowExtraPages);
            }

            return true;
        }

        while (nextPageToken != null && (firstRun || ShouldContinueAfterPage()))
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
                circuitBreakerTripped = true;
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
                        if (knownToBeInReverseOrder)
                        {
                            logger.LogInformation(
                                "Playlist '{playlistId}' appears to be in reverse-date order; stopping once added-at leaves the window, then at most {extraPages} further page(s) while an item is still inside the indexing window. Batch-size '{batchSize}'.",
                                playlistId.PlaylistId, PublishedWindowExtraPages, batchSize);
                        }
                        else
                        {
                            logger.LogInformation(
                                "Playlist '{playlistId}' is not in reverse-date order; walking to the end. Batch-size '{batchSize}'.",
                                playlistId.PlaylistId, batchSize);
                        }
                    }
                }
            }

            var pageItems = playlistItemsListResponse.Items ?? [];
            lastPagePublicItems = pageItems.Where(x => x.Snippet.Title != PrivateVideoTitle).ToList();
            result.AddRange(lastPagePublicItems);
            nextPageToken = playlistItemsListResponse.NextPageToken;

            var stop = walkGuard.Observe(
                requestedPageToken,
                pageItems.Select(x => x?.Snippet?.ResourceId?.VideoId ?? x?.ContentDetails?.VideoId).ToList(),
                nextPageToken,
                playlistItemsListResponse.PageInfo?.TotalResults);
            if (stop != YouTubePageWalkStopReason.None)
            {
                stopReason = stop;
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

        var walkOutcome = WalkOutcome(walkGuard, stopReason, circuitBreakerTripped, nextPageToken);
        return new GetPlaylistVideoSnippetsResponse(result, isExpensiveQuery,
            TotalResults: walkGuard.TotalResults, WalkOutcome: walkOutcome);
    }

    /// <summary>
    /// Complete when YouTube stopped returning a next-page token, or the guard stopped on an empty page after the walk
    /// had read (nearly) every item (<see cref="YouTubePageWalkGuard.ReadToEnd"/>): an empty-page stop, or the
    /// repeated-token empty tail. Any other guard stop and the arbitrary-order page cap are Incomplete. A loop that
    /// ended with a token still in hand stopped because a newest-first playlist left the date window.
    /// </summary>
    internal static YouTubePlaylistWalkOutcome WalkOutcome(
        YouTubePageWalkGuard walkGuard,
        YouTubePageWalkStopReason stopReason,
        bool circuitBreakerTripped,
        string? nextPageToken)
    {
        if (circuitBreakerTripped)
        {
            return YouTubePlaylistWalkOutcome.Incomplete;
        }

        if (stopReason != YouTubePageWalkStopReason.None)
        {
            // The guard logs a finished walk's tidy tail below Error (EmptyPage: Information; the repeated-token empty
            // tail after ReadToEnd: Warning). Every Error stop may have missed items.
            return walkGuard.LevelFor(stopReason) < LogLevel.Error
                ? YouTubePlaylistWalkOutcome.Complete
                : YouTubePlaylistWalkOutcome.Incomplete;
        }

        return string.IsNullOrEmpty(nextPageToken)
            ? YouTubePlaylistWalkOutcome.Complete
            : YouTubePlaylistWalkOutcome.StoppedAtWindow;
    }

    public async Task<GetPlaylistHeadResponse> GetPlaylistHead(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        int maxResults,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            return new GetPlaylistHeadResponse(null, Failure: YouTubePlaylistFetchFailure.Skipped);
        }

        try
        {
            // contentDetails is free and carries videoPublishedAt, which the indexing window reads.
            var request = youTubeServiceWrapper.YouTubeService.PlaylistItems.List("snippet,contentDetails");
            request.PlaylistId = playlistId.PlaylistId;
            request.MaxResults = Math.Clamp(maxResults, 1, PlaylistPageSize);
            indexingContext.ReportStage("youtube-playlist-head",
                $"playlist-id={playlistId.PlaylistId} max-results={request.MaxResults}");
            var response = await request.ExecuteAsync(indexingContext.CancellationToken);
            await quotaUsageTracker.RecordQuotaConsumedAsync(
                youTubeServiceWrapper.CurrentApplication,
                youTubeServiceWrapper.Usage,
                YouTubeQuotaOperation.PlaylistItemsList,
                YouTubeQuotaCosts.PlaylistItemsList);
            var items = (response.Items ?? [])
                .Where(x => x?.Snippet?.ResourceId?.VideoId != null && x.Snippet.Title != PrivateVideoTitle)
                .ToList();
            return new GetPlaylistHeadResponse(items, response.PageInfo?.TotalResults);
        }
        catch (OperationCanceledException) when (indexingContext.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
                                            ex.Message.Contains("exceeded") && ex.Message.Contains("quota"))
        {
            logger.LogWarning(ex, "Exceeded Quota occurred.");
            await quotaUsageTracker.RecordQuotaHitAsync(
                youTubeServiceWrapper.CurrentApplication,
                youTubeServiceWrapper.Usage,
                YouTubeQuotaOperation.PlaylistItemsList);
            throw new YouTubeQuotaException();
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            logger.LogError(ex,
                "YouTube playlist '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}) was not found (HTTP NotFound) reading its head.",
                playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier);
            await quotaUsageTracker.RecordNonQuotaErrorAsync();
            return new GetPlaylistHeadResponse(null, Failure: YouTubePlaylistFetchFailure.NotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to read the head of playlist-id '{playlistId}' (source: {Source}, identifier: {SourceIdentifier}).",
                playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier);
            await quotaUsageTracker.RecordNonQuotaErrorAsync();
            return new GetPlaylistHeadResponse(null, Failure: YouTubePlaylistFetchFailure.ApiError);
        }
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
