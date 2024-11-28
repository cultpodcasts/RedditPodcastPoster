using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class TolerantYouTubeChannelVideoSnippetsService(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippets,
    ILogger<TolerantYouTubeChannelVideoSnippetsService> logger) : ITolerantYouTubeChannelVideoSnippetsService
{
    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        IList<SearchResult>? result = null;
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                result = await youTubeChannelVideoSnippets.GetLatestChannelVideoSnippets(youTubeService, channelId,
                    indexingContext);
                success = true;
            }
            catch (YouTubeQuotaException ex)
            {
                logger.LogInformation(
                    "Quota exceeded observed. Rotating api-key .");
                try
                {
                    youTubeService.Rotate();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating api.");
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.SkipYouTubeUrlResolving = true;
            logger.LogError(
                "Unable to obtain latest-channel-video-snippets for channel-id '{channelId}'.", channelId.ChannelId);
        }

        return result;
    }
}