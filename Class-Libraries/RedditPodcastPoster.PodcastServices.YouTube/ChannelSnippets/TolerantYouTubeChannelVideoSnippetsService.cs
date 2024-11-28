using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class TolerantYouTubeChannelVideoSnippetsService(
    IApplicationUsageProvider applicationUsageProvider,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippets,
    IYouTubeServiceFactory youTubeServiceFactory,
    ILogger<TolerantYouTubeChannelVideoSnippetsService> logger) : ITolerantYouTubeChannelVideoSnippetsService
{
    private YouTubeServiceWrapper _youTubeService =
        youTubeServiceFactory.Create(applicationUsageProvider.GetApplicationUsage());

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        IList<SearchResult>? result = null;
        var reattempt = 0;
        var success = false;
        var rotationExcepted = false;
        while (reattempt < 2 && !success && !rotationExcepted)
        {
            try
            {
                result = await youTubeChannelVideoSnippets.GetLatestChannelVideoSnippets(_youTubeService, channelId,
                    indexingContext);
                success = true;
            }
            catch (YouTubeQuotaException ex)
            {
                reattempt++;
                logger.LogInformation(
                    "Quota exceeded observed. Rotating api-key with reattempt {reattempt} for index {index} and usage '{usage}'."
                    , reattempt, _youTubeService.Index, _youTubeService.Usage.ToString());
                try
                {
                    _youTubeService =
                        youTubeServiceFactory.Create(_youTubeService.Usage, _youTubeService.Index, reattempt);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating api. usage: '{usage}', index: {index}, reattempt: {reattempt}.",
                        _youTubeService.Usage, _youTubeService.Index, reattempt);
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.SkipYouTubeUrlResolving = true;
            logger.LogError(
                "Unable to obtain latest-channel-video-snippets for channel-id '{channelId}'. Attempt: {attempt}.",
                channelId.ChannelId, reattempt);
        }

        return result;
    }
}