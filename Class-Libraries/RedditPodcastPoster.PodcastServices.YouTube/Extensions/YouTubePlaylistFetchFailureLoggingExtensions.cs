using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class YouTubePlaylistFetchFailureLoggingExtensions
{
    /// <summary>
    /// Logs a playlist fetch failure for a podcast. <see cref="YouTubePlaylistFetchFailure.Skipped"/> (YouTube
    /// bypassed this pass) and <see cref="YouTubePlaylistFetchFailure.QuotaExhausted"/> (already logged by the
    /// tolerant service) log nothing.
    /// </summary>
    public static void LogPlaylistFetchFailure(
        this ILogger logger,
        Podcast podcast,
        YouTubePlaylistFetchFailure? failure,
        YouTubePlaylistId playlistId)
    {
        switch (failure)
        {
            case null:
            case YouTubePlaylistFetchFailure.Skipped:
            case YouTubePlaylistFetchFailure.QuotaExhausted:
                return;
            case YouTubePlaylistFetchFailure.NotFound:
                logger.LogError(
                    "YouTube playlist '{PlaylistId}' (source: {Source}, identifier: {SourceIdentifier}) for podcast '{PodcastName}' (id '{PodcastId}') was not found. The playlist may have been deleted or made private — find and set a new YouTubePlaylistId.",
                    playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier, podcast.Name, podcast.Id);
                return;
            default:
                logger.LogError(
                    "YouTube playlist fetch failed for podcast '{PodcastName}' (id '{PodcastId}') playlist '{PlaylistId}' (source: {Source}, identifier: {SourceIdentifier}, failure '{Failure}').",
                    podcast.Name, podcast.Id, playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier,
                    failure);
                return;
        }
    }
}
