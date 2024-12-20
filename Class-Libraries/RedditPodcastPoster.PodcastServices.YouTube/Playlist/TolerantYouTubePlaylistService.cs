using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class TolerantYouTubePlaylistService(
    IYouTubeServiceWrapper youTubeService,
    IYouTubePlaylistService youTubePlaylistService,
    ILogger<TolerantYouTubePlaylistService> logger
    ) : ITolerantYouTubePlaylistService
{
    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId, IndexingContext indexingContext)
    {
        GetPlaylistVideoSnippetsResponse result = new GetPlaylistVideoSnippetsResponse(null);
        var success = false;
        var rotationExcepted = false;
        while (youTubeService.CanRotate && !success && !rotationExcepted)
        {
            try
            {
                result = await youTubePlaylistService.GetPlaylistVideoSnippets(youTubeService, playlistId,
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
            logger.LogError("Unable to obtain latest-playlist-video-snippets for channel-id '{playlistId}'.",
                playlistId.PlaylistId);
        }

        return result;
    }
}
