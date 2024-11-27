using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class TolerantYouTubeChannelVideoSnippetsService(
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippets,
    YouTubeServiceWrapper youTubeService,
    IYouTubeServiceFactory youTubeServiceFactory,
    ILogger<TolerantYouTubeChannelVideoSnippetsService> logger) : ITolerantYouTubeChannelVideoSnippetsService
{
    private YouTubeServiceWrapper _youTubeService = youTubeService;

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        IList<SearchResult>? result = null;
        var reattempt = 0;
        var success = false;
        while (reattempt < 2 && !success)
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
                _youTubeService = youTubeServiceFactory.Create(_youTubeService.Usage, _youTubeService.Index, reattempt);
            }
        }

        if (!success)
        {
            logger.LogError(
                "Unable to obtain latest-channel-video-snippets for channel-id '{channelId}'. Attempt: {attempt}.",
                channelId.ChannelId, reattempt);
        }

        return result;
    }
}