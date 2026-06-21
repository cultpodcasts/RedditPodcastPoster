using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Channel;

public class TolerantYouTubeChannelService(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<TolerantYouTubeChannelService> logger) : ITolerantYouTubeChannelService
{
    public async Task<Google.Apis.YouTube.v3.Data.Channel?> GetChannel(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false,
        bool withContentDetails = false)
    {
        Google.Apis.YouTube.v3.Data.Channel? result = null;
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                result = await youTubeChannelService.GetChannel(
                    channelId,
                    indexingContext,
                    withSnippets,
                    withContentOwnerDetails,
                    withStatistics,
                    withContentDetails);
                success = true;
            }
            catch (YouTubeQuotaException)
            {
                logger.LogInformation("Quota exceeded observed. Rotating api-key.");
                await quotaUsageTracker.RecordQuotaHitAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                try
                {
                    youTubeService.Rotate();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating youtube-api.");
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.SkipYouTubeUrlResolving = true;
            logger.LogError("Unable to obtain channel for channel-id '{channelId}'.", channelId.ChannelId);
        }

        return result;
    }
}
