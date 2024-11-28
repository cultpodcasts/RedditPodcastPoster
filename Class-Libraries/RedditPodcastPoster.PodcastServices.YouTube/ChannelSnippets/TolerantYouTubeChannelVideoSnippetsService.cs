using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class TolerantYouTubeChannelVideoSnippetsService(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippets,
    IYouTubeServiceFactory youTubeServiceFactory,
    ILogger<TolerantYouTubeChannelVideoSnippetsService> logger) : ITolerantYouTubeChannelVideoSnippetsService
{

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        IList<SearchResult>? result = null;
        var reattempt = 0;
        var success = false;
        var rotationExcepted = false;
        while (reattempt <= youTubeService.Reattempts && !success && !rotationExcepted)
        {
            try
            {
                result = await youTubeChannelVideoSnippets.GetLatestChannelVideoSnippets(youTubeService, channelId,
                    indexingContext);
                success = true;
            }
            catch (YouTubeQuotaException ex)
            {
                reattempt++;
                logger.LogInformation(
                    "Quota exceeded observed. Rotating api-key with reattempt {reattempt} for index {index} and usage '{usage}'."
                    , reattempt, youTubeService.Index, youTubeService.ApplicationUsage.ToString());
                try
                {
                    youTubeService.Rotate();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating api. usage: '{usage}', index: {index}, reattempt: {reattempt}.",
                        youTubeService.ApplicationUsage, youTubeService.Index, reattempt);
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.SkipYouTubeUrlResolving = true;
            logger.LogError(
                "Unable to obtain latest-channel-video-snippets for channel-id '{channelId}'. Attempt: {attempt}, Usage: {usage}, reattempts: {reattempts}.",
                channelId.ChannelId, reattempt, youTubeService.ApplicationUsage, youTubeService.Reattempts);
        }

        return result;
    }
}