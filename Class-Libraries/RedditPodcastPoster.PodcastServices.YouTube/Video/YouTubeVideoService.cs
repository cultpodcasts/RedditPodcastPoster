using System.Net;
using Google;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Video;

public class YouTubeVideoService(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubeVideoService> logger)
    : IYouTubeVideoService
{
    private const int MaxSearchResults = 5;

    public async Task<IList<Google.Apis.YouTube.v3.Data.Video>?> GetVideoContentDetails(
        IYouTubeServiceWrapper youTubeService,
        IEnumerable<string> videoIds,
        IndexingContext? indexingContext,
        bool withSnippets = false,
        bool withStatistics = false)
    {
        if (indexingContext is {SkipYouTubeUrlResolving: true})
        {
            logger.LogInformation(
                "Skipping '{methodName}' as '{skipYouTubeUrlResolvingName}' is set. Video-ids: '{videoIds}'.",
                nameof(GetVideoContentDetails), nameof(indexingContext.SkipYouTubeUrlResolving),
                string.Join(",", videoIds));
            return null;
        }

        var result = new List<Google.Apis.YouTube.v3.Data.Video>();
        var nextPageToken = "";
        var batch = 0;
        var batchVideoIds = videoIds.Take(MaxSearchResults);
        while (batchVideoIds.Any())
        {
            while (nextPageToken != null)
            {
                var contentDetails = "contentDetails";
                if (withSnippets)
                {
                    contentDetails = "snippet," + contentDetails;
                }

                if (withStatistics)
                {
                    contentDetails = "statistics," + contentDetails;
                }

                var request = youTubeService.YouTubeService.Videos.List(contentDetails);
                request.Id = string.Join(",", batchVideoIds);
                request.MaxResults = MaxSearchResults;
                VideoListResponse response;
                try
                {
                    response = await request.ExecuteAsync();
                    await quotaUsageTracker.RecordQuotaConsumedAsync(
                        youTubeService.CurrentApplication,
                        youTubeService.Usage,
                        YouTubeQuotaOperation.VideosList,
                        YouTubeQuotaCosts.VideosList);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
                                                    ex.Message.Contains("exceeded") &&
                                                    ex.Message.Contains("quota"))
                {
                    logger.LogWarning(ex,
                        "Exceeded quota obtaining videos matching video-ids '{videoIds}'.",
                        string.Join(",", videoIds));
                    await quotaUsageTracker.RecordQuotaHitAsync(
                        youTubeService.CurrentApplication,
                        youTubeService.Usage,
                        YouTubeQuotaOperation.VideosList);
                    indexingContext?.MarkYouTubeQuotaExhausted();
                    return null;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to use {youTubeServiceName} obtaining videos matching video-ids '{videoIds}'.",
                        nameof(youTubeService.YouTubeService), string.Join(",", videoIds));
                    await quotaUsageTracker.RecordNonQuotaErrorAsync();
                    if (indexingContext != null)
                    {
                        indexingContext.SkipYouTubeUrlResolving = true;
                    }

                    return null;
                }

                result.AddRange(response.Items);
                nextPageToken = response.NextPageToken;
            }

            nextPageToken = "";
            batch++;
            batchVideoIds = videoIds
                .Skip(batch * MaxSearchResults)
                .Take(MaxSearchResults);
        }

        return result;
    }
}
