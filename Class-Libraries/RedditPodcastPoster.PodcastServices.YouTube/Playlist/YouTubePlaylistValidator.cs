using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// One playlists.list (part=snippet, 1 quota unit) through the api-key ring. A failed lookup is reported as
/// <see cref="YouTubePlaylistValidationStatus.LookupFailed"/> so the caller refuses the save with a retryable error,
/// rather than letting an unchecked id through. The owner channel is returned for information only: a playlist owned
/// by any channel is accepted.
/// </summary>
public class YouTubePlaylistValidator(
    ITolerantYouTubePlaylistService youTubePlaylistService,
    ILogger<YouTubePlaylistValidator> logger) : IYouTubePlaylistValidator
{
    public async Task<YouTubePlaylistValidation> Validate(string? youTubePlaylistId,
        CancellationToken cancellationToken = default)
    {
        var playlistId = youTubePlaylistId?.Trim() ?? string.Empty;
        if (playlistId.Length == 0)
        {
            return new YouTubePlaylistValidation(YouTubePlaylistValidationStatus.NotApplicable, playlistId);
        }

        if (!YouTubePlaylistIdFormat.IsValid(playlistId))
        {
            logger.LogWarning("YouTubePlaylistValidation playlist-id='{PlaylistId}' status='{Status}'.",
                playlistId, YouTubePlaylistValidationStatus.InvalidPlaylistId);
            return new YouTubePlaylistValidation(YouTubePlaylistValidationStatus.InvalidPlaylistId, playlistId);
        }

        GetPlaylistInfoResponse info;
        try
        {
            info = await youTubePlaylistService.GetPlaylistInfo(
                new YouTubePlaylistId(playlistId, YouTubePlaylistIdSource.Unknown, "playlist-validator"),
                new IndexingContext { CancellationToken = cancellationToken });
        }
        catch (Exception ex) when (ex is YouTubePlaylistNotFoundException ||
                                   ex is GoogleApiException
                                   {
                                       HttpStatusCode: HttpStatusCode.NotFound or HttpStatusCode.BadRequest
                                   })
        {
            // Empty response, 404 playlistNotFound, or 400 for an id YouTube rejects: the curator must fix the id;
            // retrying will not help.
            logger.LogWarning("YouTubePlaylistValidation playlist-id='{PlaylistId}' status='{Status}'.",
                playlistId, YouTubePlaylistValidationStatus.PlaylistNotFound);
            return new YouTubePlaylistValidation(YouTubePlaylistValidationStatus.PlaylistNotFound, playlistId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "YouTubePlaylistValidation playlist-id='{PlaylistId}' status='{Status}'.",
                playlistId, YouTubePlaylistValidationStatus.LookupFailed);
            return new YouTubePlaylistValidation(YouTubePlaylistValidationStatus.LookupFailed, playlistId);
        }

        logger.LogInformation(
            "YouTubePlaylistValidation playlist-id='{PlaylistId}' status='{Status}' owner-channel-id='{OwnerChannelId}' owner-channel-title='{OwnerChannelTitle}'.",
            playlistId, YouTubePlaylistValidationStatus.Valid, info.ChannelId, info.ChannelTitle);
        return new YouTubePlaylistValidation(YouTubePlaylistValidationStatus.Valid, playlistId, info.ChannelId,
            info.ChannelTitle);
    }
}
