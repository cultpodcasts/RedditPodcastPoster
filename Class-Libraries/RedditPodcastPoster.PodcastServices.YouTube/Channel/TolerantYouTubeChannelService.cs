using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

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
        var lookup = await GetChannelLookup(channelId, indexingContext, withSnippets, withContentOwnerDetails,
            withStatistics, withContentDetails);
        return lookup.Channel;
    }

    public async Task<YouTubeChannelLookupResult> GetChannelLookup(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false,
        bool withContentDetails = false)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            // One skip policy, owned by the inner service: a pass-cache hit is still Found, otherwise Skipped.
            // No API call is made, so no call is recorded and key rotation does not apply.
            return await youTubeChannelService.GetChannelLookup(
                channelId,
                indexingContext,
                withSnippets,
                withContentOwnerDetails,
                withStatistics,
                withContentDetails);
        }

        YouTubeChannelLookupResult? result = null;
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                result = await youTubeChannelService.GetChannelLookup(
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
                await quotaUsageTracker.RecordQuotaHitAsync(
                    youTubeService.CurrentApplication,
                    youTubeService.Usage,
                    YouTubeQuotaOperation.ChannelsList);
                try
                {
                    youTubeService.Rotate();
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
            logger.LogError("Unable to obtain channel for channel-id '{channelId}'.", channelId.ChannelId);
            return YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.QuotaExhausted);
        }

        return result ?? YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.ApiError);
    }
}
