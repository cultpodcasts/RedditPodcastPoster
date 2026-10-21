using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface IYouTubePlaylistMembership
{
    /// <summary>
    /// Whether <paramref name="videoId"/> is an item of <paramref name="playlistId"/>: one
    /// <c>playlistItems.list</c> call filtered by <c>videoId</c> (1 quota unit). Null when it could not be checked.
    /// </summary>
    Task<bool?> Contains(IYouTubeServiceWrapper youTubeService, string playlistId, string videoId);
}

public class YouTubePlaylistMembership(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubePlaylistMembership> logger) : IYouTubePlaylistMembership
{
    public async Task<bool?> Contains(IYouTubeServiceWrapper youTubeService, string playlistId, string videoId)
    {
        try
        {
            var request = youTubeService.YouTubeService.PlaylistItems.List("id");
            request.PlaylistId = playlistId;
            request.VideoId = videoId;
            request.MaxResults = 1;
            var response = await request.ExecuteAsync();
            await quotaUsageTracker.RecordQuotaConsumedAsync(
                youTubeService.CurrentApplication,
                youTubeService.Usage,
                YouTubeQuotaOperation.PlaylistItemsList,
                YouTubeQuotaCosts.PlaylistItemsList);
            return response.Items is { Count: > 0 };
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
                                            ex.Message.Contains("quota"))
        {
            logger.LogWarning(ex, "Quota exceeded checking video '{VideoId}' in playlist '{PlaylistId}'.", videoId,
                playlistId);
            await quotaUsageTracker.RecordQuotaHitAsync(
                youTubeService.CurrentApplication,
                youTubeService.Usage,
                YouTubeQuotaOperation.PlaylistItemsList);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check video '{VideoId}' in playlist '{PlaylistId}'.", videoId, playlistId);
            await quotaUsageTracker.RecordNonQuotaErrorAsync();
            return null;
        }
    }
}
