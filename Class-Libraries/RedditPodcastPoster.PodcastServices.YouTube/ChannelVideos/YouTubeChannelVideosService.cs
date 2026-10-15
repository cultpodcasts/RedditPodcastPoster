using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;

public class YouTubeChannelVideosService(
    ITolerantYouTubeChannelService youTubeChannelService,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    ILogger<YouTubeChannelVideosService> logger
) : IYouTubeChannelVideosService, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<string, Models.ChannelVideos> _cache = new();

    public void ClearPassCache()
    {
        _cache.Clear();
    }

    public async Task<GetChannelVideosResponse> GetChannelVideos(YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool expensivePlaylist = false)
    {
        if (_cache.TryGetValue(channelId.ChannelId, out var cachedVideos))
        {
            return new GetChannelVideosResponse(cachedVideos.Channel, cachedVideos.PlaylistItems);
        }

        var lookup =
            await youTubeChannelService.GetChannelLookup(channelId, indexingContext, true, true, withContentDetails: true);
        var channel = lookup.Channel;
        if (channel == null)
        {
            switch (lookup.Outcome)
            {
                case YouTubeChannelLookupOutcome.Skipped:
                    // YouTube is bypassed this pass and the channel is not in the pass cache: nothing was looked up.
                    logger.LogDebug(
                        "{GetChannelVideosName}: skipped for channel-id '{ChannelId}' as YouTube is bypassed this pass.",
                        nameof(GetChannelVideos), channelId.ChannelId);
                    return new GetChannelVideosResponse(null, null, YouTubePlaylistFetchFailure.Skipped);
                case YouTubeChannelLookupOutcome.QuotaExhausted:
                    // The tolerant channel service already logged the error and set YouTubeQuotaExhausted.
                    return new GetChannelVideosResponse(null, null, YouTubePlaylistFetchFailure.QuotaExhausted);
                case YouTubeChannelLookupOutcome.NotFound:
                    logger.LogError("{GetChannelVideosName}: Unable to find channel with id '{ChannelIdChannelId}'.",
                        nameof(GetChannelVideos), channelId.ChannelId);
                    return new GetChannelVideosResponse(null, null, YouTubePlaylistFetchFailure.NotFound);
                default:
                    logger.LogError("{GetChannelVideosName}: Failed to look up channel with id '{ChannelIdChannelId}' (outcome '{Outcome}').",
                        nameof(GetChannelVideos), channelId.ChannelId, lookup.Outcome);
                    return new GetChannelVideosResponse(null, null, YouTubePlaylistFetchFailure.ApiError);
            }
        }

        var uploadsChannelId = channel.ContentDetails.RelatedPlaylists.Uploads;
        var response = await youTubePlaylistService.GetPlaylistVideoSnippets(
            new YouTubePlaylistId(uploadsChannelId, YouTubePlaylistIdSource.ChannelUploads, channelId.ChannelId),
            indexingContext, expensivePlaylist: expensivePlaylist);
        if (response.Failure == YouTubePlaylistFetchFailure.Skipped)
        {
            return new GetChannelVideosResponse(channel, null, YouTubePlaylistFetchFailure.Skipped);
        }

        if (response.Result != null)
        {
            var playlistItems = response.Result.ToList();

            if (playlistItems.Count >= 2 && !PlaylistItemOrdering.IsReverseDateOrdered(playlistItems))
            {
                logger.LogWarning(
                    "Uploads playlist '{UploadsChannelId}' for channel-id '{ChannelId}' is not in reverse-date order.",
                    uploadsChannelId, channelId.ChannelId);
            }

            var result = new Models.ChannelVideos(channel, playlistItems);
            _cache[channelId.ChannelId] = result;
            return new GetChannelVideosResponse(channel, playlistItems);
        }

        logger.LogError(
            "{GetChannelVideosName}: Unable to find channel-upload-playlist-items for channel-id '{ChannelIdChannelId}', playlist-id '{UploadsChannelId}'.",
            nameof(GetChannelVideos), channelId.ChannelId, uploadsChannelId);
        return new GetChannelVideosResponse(channel, null, response.Failure ?? YouTubePlaylistFetchFailure.ApiError);
    }
}
