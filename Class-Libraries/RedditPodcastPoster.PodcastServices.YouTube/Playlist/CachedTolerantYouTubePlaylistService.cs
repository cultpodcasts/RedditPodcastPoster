using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class CachedTolerantYouTubePlaylistService(
    ITolerantYouTubePlaylistService tolerantYouTubePlaylistService,
    ILogger<CachedTolerantYouTubePlaylistService> logger
) : ICachedTolerantYouTubePlaylistService, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<string, IList<PlaylistItem>> _cache = new();


    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        bool expensivePlaylist = false)
    {
        if (_cache.TryGetValue(playlistId.PlaylistId + withContentDetails, out var playlistItems))
        {
            return new GetPlaylistVideoSnippetsResponse(playlistItems);
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation("Skipping '{method}' as '{property}' is set. Channel-id: '{playlistId}'.",
                nameof(GetPlaylistVideoSnippets), nameof(indexingContext.SkipYouTubeUrlResolving),
                playlistId.PlaylistId);
            return new GetPlaylistVideoSnippetsResponse(null);
        }

        var result = await tolerantYouTubePlaylistService.GetPlaylistVideoSnippets(
            playlistId, indexingContext, withContentDetails, expensivePlaylist);

        if (result?.Result != null)
        {
            _cache[playlistId.PlaylistId + withContentDetails] = result.Result;
        }

        return result ?? new GetPlaylistVideoSnippetsResponse(null);
    }

    public void ClearPassCache()
    {
        _cache.Clear();
    }
}
