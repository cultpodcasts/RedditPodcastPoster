using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface IYouTubePlaylistMembership
{
    /// <summary>
    /// Whether <paramref name="videoId"/> is an item of <paramref name="playlistId"/>: one
    /// <c>playlistItems.list</c> call filtered by <c>videoId</c> (1 quota unit). False when YouTube says the
    /// playlist does not exist, rejects its id (404 / 400), or the items are inaccessible
    /// (<c>playlistItemsNotAccessible</c>), so a dead or private playlist reads as "not in it". Null only when it
    /// could not be checked (quota, other 403s, 5xx, network).
    /// </summary>
    Task<bool?> Contains(IYouTubeServiceWrapper youTubeService, string playlistId, string videoId);

    /// <summary>
    /// True when <paramref name="playlistId"/> is blank (channel-wide), the URL's <c>list=</c> matches, or
    /// <see cref="Contains"/> lists the video. False when not in it or the stored id is malformed; null only when
    /// it could not be checked (skip resolving, missing video id, quota, outage).
    /// </summary>
    Task<bool?> IsVideoInPlaylist(
        IYouTubeServiceWrapper youTubeService,
        string? playlistId,
        Uri url,
        bool skipYouTubeUrlResolving);
}

public class YouTubePlaylistMembership(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILogger<YouTubePlaylistMembership> logger) : IYouTubePlaylistMembership
{
    private const string PlaylistItemsNotAccessibleReason = "playlistItemsNotAccessible";

    public Task<bool?> IsVideoInPlaylist(
        IYouTubeServiceWrapper youTubeService,
        string? playlistId,
        Uri url,
        bool skipYouTubeUrlResolving)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return Task.FromResult<bool?>(true);
        }

        if (string.Equals(YouTubePlaylistIdResolver.Extract(url), playlistId, StringComparison.Ordinal))
        {
            return Task.FromResult<bool?>(true);
        }

        if (!YouTubePlaylistIdFormat.IsValid(playlistId))
        {
            logger.LogWarning(
                "Stored youtube-playlist-id '{PlaylistId}' is malformed; treating '{Url}' as not in it. Fix the stored id.",
                playlistId, url);
            return Task.FromResult<bool?>(false);
        }

        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId) || skipYouTubeUrlResolving)
        {
            return Task.FromResult<bool?>(null);
        }

        return Contains(youTubeService, playlistId, videoId);
    }

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
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
                                            HasErrorReason(ex, PlaylistItemsNotAccessibleReason))
        {
            logger.LogWarning(ex,
                "YouTube reports playlist '{PlaylistId}' items as inaccessible ({Reason}); treating video '{VideoId}' as not in it. Fix or remove the stored id.",
                playlistId, PlaylistItemsNotAccessibleReason, videoId);
            await quotaUsageTracker.RecordQuotaConsumedAsync(
                youTubeService.CurrentApplication,
                youTubeService.Usage,
                YouTubeQuotaOperation.PlaylistItemsList,
                YouTubeQuotaCosts.PlaylistItemsList);
            return false;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            logger.LogWarning(ex,
                "YouTube reports playlist '{PlaylistId}' as missing or invalid ({Status}); treating video '{VideoId}' as not in it. Fix the stored id.",
                playlistId, (int)ex.HttpStatusCode, videoId);
            await quotaUsageTracker.RecordQuotaConsumedAsync(
                youTubeService.CurrentApplication,
                youTubeService.Usage,
                YouTubeQuotaOperation.PlaylistItemsList,
                YouTubeQuotaCosts.PlaylistItemsList);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check video '{VideoId}' in playlist '{PlaylistId}'.", videoId, playlistId);
            await quotaUsageTracker.RecordNonQuotaErrorAsync();
            return null;
        }
    }

    private static bool HasErrorReason(GoogleApiException ex, string reason) =>
        ex.Error?.Errors?.Any(e => string.Equals(e.Reason, reason, StringComparison.Ordinal)) == true;
}
