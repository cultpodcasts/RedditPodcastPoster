using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
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
) : IYouTubeChannelVideosService
{
    private readonly ConcurrentDictionary<string, Models.ChannelVideos> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

    public async Task<Models.ChannelVideos?> GetChannelVideos(YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool expensivePlaylist = false)
    {
        if (_cache.TryGetValue(channelId.ChannelId, out var cachedVideos))
        {
            return cachedVideos;
        }

        var channel =
            await youTubeChannelService.GetChannel(channelId, indexingContext, true, true, withContentDetails: true);
        if (channel == null)
        {
            logger.LogError("{GetChannelVideosName}: Unable to find channel with id '{ChannelIdChannelId}'.",
                nameof(GetChannelVideos), channelId.ChannelId);
            return null;
        }

        var uploadsChannelId = channel.ContentDetails.RelatedPlaylists.Uploads;
        var response = await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(uploadsChannelId),
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
            return result;
        }

        logger.LogError(
            "{GetChannelVideosName}: Unable to find channel-upload-playlist-items for channel-id '{ChannelIdChannelId}', playlist-id '{UploadsChannelId}'.",
            nameof(GetChannelVideos), channelId.ChannelId, uploadsChannelId);
        return null;
    }
}
