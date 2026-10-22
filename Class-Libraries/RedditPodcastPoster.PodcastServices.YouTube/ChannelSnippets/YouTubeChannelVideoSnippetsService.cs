using System.Net;
using Google;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class YouTubeChannelVideoSnippetsService(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubeChannelVideoSnippetsService> logger)
    : IYouTubeChannelVideoSnippetsService
{
    private const int MaxSearchResults = 5;

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        var result = new List<SearchResult>();
        string? nextPageToken = "";
        var walkGuard = new YouTubePageWalkGuard(MaxSearchResults);

        while (nextPageToken != null)
        {
            var requestedPageToken = nextPageToken;
            SearchListResponse response;
            try
            {
                var searchListRequest = youTubeServiceWrapper.YouTubeService.Search.List("snippet");
                searchListRequest.MaxResults = MaxSearchResults;
                searchListRequest.ChannelId = channelId.ChannelId;
                searchListRequest.Type = "video";
                searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
                searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
                if (indexingContext.ReleasedSince.HasValue)
                {
                    searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
                }

                searchListRequest.PageToken = nextPageToken;
                response = await searchListRequest.ExecuteAsync();
                await quotaUsageTracker.RecordQuotaConsumedAsync(
                    youTubeServiceWrapper.CurrentApplication,
                    youTubeServiceWrapper.Usage,
                    YouTubeQuotaOperation.SearchList,
                    YouTubeQuotaCosts.SearchList);
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
                        YouTubeQuotaOperation.SearchList);
                    throw new YouTubeQuotaException();
                }

                if (IsAccountDelegationForbidden(ex))
                {
                    throw new YouTubeChannelSearchForbiddenException(channelId.ChannelId, ex);
                }

                logger.LogError(ex,
                    "Unrecognised google-api-exception. Failed to use {nameofYouTubeServiceWrapperYouTubeService} to obtain latest-channel-snippets for channel-id '{channelId}'.",
                    nameof(youTubeServiceWrapper.YouTubeService), channelId.ChannelId);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to use {nameofYouTubeServiceWrapperYouTubeService)} to obtain latest-channel-snippets for channel-id '{channelId}'.",
                    nameof(youTubeServiceWrapper.YouTubeService), channelId.ChannelId);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                return result;
            }

            var pageItems = response.Items ?? [];
            var responseItems = pageItems.Where(x =>
                x.Snippet.LiveBroadcastContent != "upcoming" && x.Snippet.LiveBroadcastContent != "live");
            result.AddRange(responseItems);
            nextPageToken = response.NextPageToken;

            // search.list costs 100 units a page, so a repeating or empty-page token is expensive.
            // Its totalResults is an estimate, so the page cap is not applied here.
            var stop = walkGuard.Observe(
                requestedPageToken,
                pageItems.Select(x => x?.Id?.VideoId).ToList(),
                nextPageToken,
                totalResults: null);
            if (stop != YouTubePageWalkStopReason.None)
            {
                // search.list is known to repeat results across pages, and these pages hold only 5
                // items, so a page of nothing but already-seen ids (NoNewItems) can happen without
                // YouTube looping. It is accepted here as a cost stop (another page is 100 units),
                // not a fault, so it logs at Warning rather than Error.
                walkGuard.LogStop(logger, stop, "channel-search", channelId.ChannelId,
                    null, null, result.Count,
                    stop == YouTubePageWalkStopReason.NoNewItems ? LogLevel.Warning : null);
                break;
            }
        }

        return result;
    }

    private static bool IsAccountDelegationForbidden(GoogleApiException ex) =>
        ex.HttpStatusCode == HttpStatusCode.Forbidden &&
        (ex.Error?.Errors?.Any(e => e.Reason == "accountDelegationForbidden") == true ||
         ex.Message.Contains("accountDelegationForbidden", StringComparison.OrdinalIgnoreCase));
}
