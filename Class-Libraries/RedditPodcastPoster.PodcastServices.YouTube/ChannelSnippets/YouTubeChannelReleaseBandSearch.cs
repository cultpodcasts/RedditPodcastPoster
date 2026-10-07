using System.Net;
using Google;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class YouTubeChannelReleaseBandSearch(
    IYouTubeServiceWrapper youTubeServiceWrapper,
    IYouTubeSearchListExecutor youTubeSearchListExecutor,
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
                response = await youTubeSearchListExecutor.ExecuteAsync(
                    new YouTubeChannelSearchPageRequest(
                        channelId,
                        publishedAfter,
                        publishedBefore,
                        nextPageToken,
                        PageSize));
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
                    throw new YouTubeChannelSearchForbiddenException(channelId, ex);
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

    private static bool IsAccountDelegationForbidden(GoogleApiException ex) =>
        ex.HttpStatusCode == HttpStatusCode.Forbidden &&
        (ex.Error?.Errors?.Any(e => e.Reason == "accountDelegationForbidden") == true ||
         ex.Message.Contains("accountDelegationForbidden", StringComparison.OrdinalIgnoreCase));
}
