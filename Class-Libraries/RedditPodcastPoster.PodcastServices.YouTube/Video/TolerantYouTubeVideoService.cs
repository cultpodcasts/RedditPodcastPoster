using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Video;

public class TolerantYouTubeVideoService(
    IYouTubeVideoService youTubeVideoService,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<TolerantYouTubeVideoService> logger) : ITolerantYouTubeVideoService
{
    public async Task<IList<Google.Apis.YouTube.v3.Data.Video>?> GetVideoContentDetails(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        IEnumerable<string> videoIds,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withStatistics = false,
        bool withStatus = false)
    {
        IList<Google.Apis.YouTube.v3.Data.Video>? result = null;
        var success = false;
        var rotationExcepted = false;
        while (youTubeServiceWrapper.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeServiceWrapper.CurrentApplication, youTubeServiceWrapper.Usage);
                result = await youTubeVideoService.GetVideoContentDetails(
                    youTubeServiceWrapper,
                    videoIds,
                    indexingContext,
                    withSnippets,
                    withStatistics,
                    withStatus);
                success = true;
            }
            catch (YouTubeQuotaException)
            {
                logger.LogInformation("Quota exceeded observed. Rotating api-key.");
                await quotaUsageTracker.RecordQuotaHitAsync(
                    youTubeServiceWrapper.CurrentApplication,
                    youTubeServiceWrapper.Usage,
                    YouTubeQuotaOperation.VideosList);
                try
                {
                    youTubeServiceWrapper.Rotate();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating youtube-api.");
                    await quotaUsageTracker.RecordRingExhaustionAsync();
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.MarkYouTubeQuotaExhausted();
            logger.LogError("Unable to obtain video-content-details for video-ids '{videoIds}'.", string.Join(",", videoIds));
        }

        return result;
    }
}
