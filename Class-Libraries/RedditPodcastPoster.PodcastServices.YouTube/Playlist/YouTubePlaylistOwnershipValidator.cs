using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// One playlists.list (part=snippet, 1 quota unit) through the api-key ring. A failed lookup is reported as
/// <see cref="YouTubePlaylistOwnershipStatus.LookupFailed"/> so the caller refuses the save with a retryable error,
/// rather than letting an unchecked pair through.
/// </summary>
public class YouTubePlaylistOwnershipValidator(
    ITolerantYouTubePlaylistService youTubePlaylistService,
    ILogger<YouTubePlaylistOwnershipValidator> logger) : IYouTubePlaylistOwnershipValidator
{
    public async Task<YouTubePlaylistOwnership> Validate(string? youTubeChannelId, string? youTubePlaylistId,
        CancellationToken cancellationToken = default)
    {
        var channelId = youTubeChannelId?.Trim() ?? string.Empty;
        var playlistId = youTubePlaylistId?.Trim() ?? string.Empty;
        if (channelId.Length == 0 || playlistId.Length == 0)
        {
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.NotApplicable, channelId, playlistId);
        }

        if (!YouTubePlaylistIdFormat.IsValid(playlistId))
        {
            logger.LogWarning(
                "YouTubePlaylistOwnership playlist-id='{PlaylistId}' channel-id='{ChannelId}' status='{Status}'.",
                playlistId, channelId, YouTubePlaylistOwnershipStatus.InvalidPlaylistId);
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.InvalidPlaylistId, channelId, playlistId);
        }

        GetPlaylistInfoResponse info;
        try
        {
            info = await youTubePlaylistService.GetPlaylistInfo(
                new YouTubePlaylistId(playlistId, YouTubePlaylistIdSource.Unknown, "ownership-validator"),
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
            logger.LogWarning(
                "YouTubePlaylistOwnership playlist-id='{PlaylistId}' channel-id='{ChannelId}' status='{Status}'.",
                playlistId, channelId, YouTubePlaylistOwnershipStatus.PlaylistNotFound);
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.PlaylistNotFound, channelId, playlistId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "YouTubePlaylistOwnership playlist-id='{PlaylistId}' channel-id='{ChannelId}' status='{Status}'.",
                playlistId, channelId, YouTubePlaylistOwnershipStatus.LookupFailed);
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.LookupFailed, channelId, playlistId);
        }

        if (string.IsNullOrWhiteSpace(info.ChannelId))
        {
            logger.LogError(
                "YouTubePlaylistOwnership playlist-id='{PlaylistId}' channel-id='{ChannelId}' status='{Status}': no owner channel in the response.",
                playlistId, channelId, YouTubePlaylistOwnershipStatus.LookupFailed);
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.LookupFailed, channelId, playlistId);
        }

        if (string.Equals(info.ChannelId, channelId, StringComparison.Ordinal))
        {
            return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.Matches, channelId, playlistId,
                info.ChannelId, info.ChannelTitle);
        }

        logger.LogWarning(
            "YouTubePlaylistOwnership playlist-id='{PlaylistId}' channel-id='{ChannelId}' status='{Status}' owner-channel-id='{OwnerChannelId}' owner-channel-title='{OwnerChannelTitle}'.",
            playlistId, channelId, YouTubePlaylistOwnershipStatus.Mismatch, info.ChannelId, info.ChannelTitle);
        return new YouTubePlaylistOwnership(YouTubePlaylistOwnershipStatus.Mismatch, channelId, playlistId,
            info.ChannelId, info.ChannelTitle);
    }
}
