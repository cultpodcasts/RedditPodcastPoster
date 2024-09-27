using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeChannelVideosService(
    IYouTubeChannelService youTubeChannelService,
    IYouTubePlaylistService youTubePlaylistService,
    ILogger<YouTubeChannelVideosService> logger
) : IYouTubeChannelVideosService
{
    private readonly ConcurrentDictionary<string, ChannelVideos> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

    public async Task<ChannelVideos?> GetChannelVideos(YouTubeChannelId channelId, IndexingContext indexingContext)
    {
        if (_cache.TryGetValue(channelId.ChannelId, out var cachedVideos))
        {
            return cachedVideos;
        }

        var channel =
            await youTubeChannelService.GetChannel(channelId, indexingContext, true, true, withContentDetails: true);
        if (channel == null)
        {
            logger.LogError($"{nameof(GetChannelVideos)}: Unable to find channel with id '{channelId.ChannelId}'.");
            return null;
        }

        var uploadsChannelId = channel.ContentDetails.RelatedPlaylists.Uploads;
        var response =
            await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(uploadsChannelId),
                indexingContext);
        if (response.Result != null)
        {
            var result = new ChannelVideos(channel, response.Result);
            _cache[channelId.ChannelId] = result;
            return result;
        }

        logger.LogError(
            $"{nameof(GetChannelVideos)}: Unable to find channel-upload-playlist-items for channel-id '{channelId.ChannelId}', playlist-id '{uploadsChannelId}'.");
        return null;
    }
}