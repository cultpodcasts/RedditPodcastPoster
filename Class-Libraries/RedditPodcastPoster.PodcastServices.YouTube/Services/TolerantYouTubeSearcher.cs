using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class TolerantYouTubeSearcher(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeSearcher youTubeSearcher,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<TolerantYouTubeSearcher> logger) : ITolerantYouTubeSearcher
{
    public async Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        IList<EpisodeResult> result = new List<EpisodeResult>();
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                result = await youTubeSearcher.Search(query, indexingContext);
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
            logger.LogError("Unable to search episodes for query '{query}'.", query);
        }

        return result;
    }
}
