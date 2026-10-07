using System.Net;
using Google;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class YouTubeChannelReleaseBandSearch(
    IYouTubeServiceWrapper youTubeServiceWrapper,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubeChannelReleaseBandSearch> logger) : IYouTubeChannelReleaseBandSearch
{
    public const int PageSize = 50;
    public const int MaxPages = 2;

    public async Task<IList<SearchResult>?> Search(
        string channelId,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore,
        IndexingContext indexingContext)
    {
        var result = new List<SearchResult>();
        var nextPageToken = "";
        var pages = 0;
        while (nextPageToken != null && pages < MaxPages)
        {
            SearchListResponse response;
            try
            {
                var searchListRequest = youTubeServiceWrapper.YouTubeService.Search.List("snippet");
                searchListRequest.MaxResults = PageSize;
                searchListRequest.ChannelId = channelId;
                searchListRequest.Type = "video";
                searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
                searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
                searchListRequest.PublishedAfterDateTimeOffset = publishedAfter;
                searchListRequest.PublishedBeforeDateTimeOffset = publishedBefore;
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

                logger.LogError(ex,
                    "Failed to search channel '{ChannelId}' for videos published between {PublishedAfter:u} and {PublishedBefore:u}.",
                    channelId, publishedAfter, publishedBefore);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to search channel '{ChannelId}' for videos published between {PublishedAfter:u} and {PublishedBefore:u}.",
                    channelId, publishedAfter, publishedBefore);
                await quotaUsageTracker.RecordNonQuotaErrorAsync();
                return result;
            }

            pages++;
            if (response.Items != null)
            {
                result.AddRange(response.Items.Where(x =>
                    x.Snippet?.LiveBroadcastContent is not ("upcoming" or "live")));
            }

            nextPageToken = response.NextPageToken;
        }

        return result;
    }
}
