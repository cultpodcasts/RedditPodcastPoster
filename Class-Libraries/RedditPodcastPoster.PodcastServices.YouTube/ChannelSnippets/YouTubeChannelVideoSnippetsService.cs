using System.Net;
using Google;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
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
        var nextPageToken = "";
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

        //upcoming
        while (nextPageToken != null)
        {
            searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging

            SearchListResponse response;
            try
            {
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
                indexingContext.SkipYouTubeUrlResolving = true;
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to use {nameofYouTubeServiceWrapperYouTubeService)} to obtain latest-channel-snippets for channel-id '{channelId}'.",
                    nameof(youTubeServiceWrapper.YouTubeService), channelId.ChannelId);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                indexingContext.SkipYouTubeUrlResolving = true;
                return result;
            }

            var responseItems = response.Items.Where(x =>
                x.Snippet.LiveBroadcastContent != "upcoming" && x.Snippet.LiveBroadcastContent != "live");
            result.AddRange(responseItems);
            nextPageToken = response.NextPageToken;
        }

        return result;
    }

    private static bool IsAccountDelegationForbidden(GoogleApiException ex) =>
        ex.HttpStatusCode == HttpStatusCode.Forbidden &&
        (ex.Error?.Errors?.Any(e => e.Reason == "accountDelegationForbidden") == true ||
         ex.Message.Contains("accountDelegationForbidden", StringComparison.OrdinalIgnoreCase));
}
