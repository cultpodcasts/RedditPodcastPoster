using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class TolerantYouTubePlaylistService(
    IYouTubeServiceWrapper youTubeService,
    IYouTubePlaylistService youTubePlaylistService,
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<TolerantYouTubePlaylistService> logger
) : ITolerantYouTubePlaylistService
{
    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId, IndexingContext indexingContext, bool withContentDetails = false,
        bool expensivePlaylist = false)
    {
        var result = new GetPlaylistVideoSnippetsResponse(null);
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                await quotaUsageTracker.RecordCallAsync(youTubeService.CurrentApplication, youTubeService.Usage);
                result = await youTubePlaylistService.GetPlaylistVideoSnippets(youTubeService, playlistId,
                    indexingContext, withContentDetails, expensivePlaylist);
                success = true;
            }
            catch (YouTubeQuotaException)
            {
                logger.LogInformation(
                    "Quota exceeded observed. Rotating api-key .");
                await quotaUsageTracker.RecordQuotaHitAsync(
                    youTubeService.CurrentApplication,
                    youTubeService.Usage,
                    YouTubeQuotaOperation.PlaylistItemsList);
                try
                {
                    youTubeService.Rotate();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error rotating api.");
                    await quotaUsageTracker.RecordRingExhaustionAsync();
                    rotationExcepted = true;
                }
            }
        }

        if (!success)
        {
            indexingContext.MarkYouTubeQuotaExhausted();
            logger.LogError("Unable to obtain latest-playlist-video-snippets for channel-id '{playlistId}'.",
                playlistId.PlaylistId);
        }

        return result;
    }
}
