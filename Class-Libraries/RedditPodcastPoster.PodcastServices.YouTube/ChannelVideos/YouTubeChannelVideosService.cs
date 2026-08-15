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

        var channel =
            await youTubeChannelService.GetChannel(channelId, indexingContext, true, true, withContentDetails: true);
        if (channel == null)
        {
            logger.LogError("{GetChannelVideosName}: Unable to find channel with id '{ChannelIdChannelId}'.",
                nameof(GetChannelVideos), channelId.ChannelId);
            return new GetChannelVideosResponse(null, null, YouTubePlaylistFetchFailure.NotFound);
        }

        var uploadsChannelId = channel.ContentDetails.RelatedPlaylists.Uploads;
        var response = await youTubePlaylistService.GetPlaylistVideoSnippets(
            new YouTubePlaylistId(uploadsChannelId, YouTubePlaylistIdSource.ChannelUploads, channelId.ChannelId),
            indexingContext, expensivePlaylist: expensivePlaylist);
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
