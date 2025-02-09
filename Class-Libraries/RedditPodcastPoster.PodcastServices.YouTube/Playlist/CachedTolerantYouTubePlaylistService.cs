using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public class CachedTolerantYouTubePlaylistService(
    ITolerantYouTubePlaylistService tolerantYouTubePlaylistService,
    ILogger<CachedTolerantYouTubePlaylistService> logger
) : ICachedTolerantYouTubePlaylistService
{
    private static readonly ConcurrentDictionary<string, IList<PlaylistItem>> Cache = new();


    public async Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false)
    {
        if (Cache.TryGetValue(playlistId.PlaylistId + withContentDetails, out var playlistItems))
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
            playlistId, indexingContext, withContentDetails);

        if (result?.Result != null)
        {
            Cache[playlistId.PlaylistId + withContentDetails] = result.Result;
        }

        return result ?? new GetPlaylistVideoSnippetsResponse(null);
    }

    public void Flush()
    {
        Cache.Clear();
    }
}