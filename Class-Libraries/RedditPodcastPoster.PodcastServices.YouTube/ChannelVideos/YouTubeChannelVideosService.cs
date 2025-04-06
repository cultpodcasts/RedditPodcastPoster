using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;

public class YouTubeChannelVideosService(
    IYouTubeChannelService youTubeChannelService,
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
        IndexingContext indexingContext)
    {
        if (_cache.TryGetValue(channelId.ChannelId, out var cachedVideos))
        {
            return cachedVideos;
        }

        var channel =
            await youTubeChannelService.GetChannel(channelId, indexingContext, true, true, withContentDetails: true);
        if (channel == null)
        {
            logger.LogError("{method}: Unable to find channel with id '{channelId}'.",
                nameof(GetChannelVideos), channelId.ChannelId);
            return null;
        }

        var uploadsChannelId = channel.ContentDetails.RelatedPlaylists.Uploads;
        var response =
            await youTubePlaylistService.GetPlaylistVideoSnippets(
                new YouTubePlaylistId(uploadsChannelId),
                indexingContext);
        if (response.Result != null)
        {
            var result = new Models.ChannelVideos(channel, response.Result);
            _cache[channelId.ChannelId] = result;
            return result;
        }

        logger.LogError(
            "{method}: Unable to find channel-upload-playlist-items for channel-id '{channelId}', playlist-id '{uploadsChannelId}'.",
            nameof(GetChannelVideos), channelId.ChannelId, uploadsChannelId);
        return null;
    }

    public async Task<Models.ChannelVideos?> GetPlaylistVideos(YouTubeChannelId channelId,
        YouTubePlaylistId youTubePlaylistId,
        IndexingContext indexingContext)
    {
        var channel =
            await youTubeChannelService.GetChannel(channelId, indexingContext, true, true, withContentDetails: true);
        if (channel == null)
        {
            logger.LogError("{method}: Unable to find channel with id '{channelId}'.",
                nameof(GetPlaylistVideos), channelId.ChannelId);
            return null;
        }

        var response =
            await youTubePlaylistService.GetPlaylistVideoSnippets(
                youTubePlaylistId,
                indexingContext);
        if (response.Result != null)
        {
            var result = new Models.ChannelVideos(channel, response.Result);
            return result;
        }

        logger.LogError(
            "{method}: Unable to find channel-upload-playlist-items for channel-id '{channelId}', playlist-id '{playlistId}'.",
            nameof(GetChannelVideos), channelId.ChannelId, youTubePlaylistId.PlaylistId);
        return null;
    }
}