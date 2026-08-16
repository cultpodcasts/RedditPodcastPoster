using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public class TolerantYouTubeChannelResolver(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeChannelResolver youTubeChannelResolver,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<TolerantYouTubeChannelResolver> logger) : ITolerantYouTubeChannelResolver
{
    public async Task<SearchResult?> FindChannelsSnippets(
        string channelName,
        string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext)
    {
        SearchResult? result = null;
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                result = await youTubeChannelResolver.FindChannelsSnippets(channelName, mostRecentlyUploadVideoTitle, indexingContext);
                success = true;
            }
            catch (YouTubeQuotaException)
            {
                logger.LogInformation("Quota exceeded observed. Rotating api-key.");
                await quotaUsageTracker.RecordQuotaHitAsync(
                    youTubeService.CurrentApplication,
                    youTubeService.Usage,
                    YouTubeQuotaOperation.SearchList);
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
            logger.LogError("Unable to resolve channel for channel-name '{channelName}'.", channelName);
        }

        return result;
    }
}
