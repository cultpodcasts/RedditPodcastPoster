using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class CachedTolerantYouTubeChannelVideoSnippetsService(
    ITolerantYouTubeChannelVideoSnippetsService tolerantYouTubeChannelVideoSnippetsService,
    ILogger<CachedTolerantYouTubeChannelVideoSnippetsService> logger)
    : ICachedTolerantYouTubeChannelVideoSnippetsService, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<string, IList<SearchResult>> _cache = new();

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        if (_cache.TryGetValue(channelId.ChannelId, out var snippets))
        {
            return snippets;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{GetLatestChannelVideoSnippetsName}' as '{IndexingContextSkipYouTubeUrlResolvingName}' is set. Channel-id: '{ChannelIdChannelId}'."
                , nameof(GetLatestChannelVideoSnippets), nameof(indexingContext.SkipYouTubeUrlResolving), channelId
                    .ChannelId);
            return null;
        }

        var result = await tolerantYouTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(channelId,
            indexingContext);

        if (result != null)
        {
            _cache[channelId.ChannelId] = result;
        }

        return result;
    }

    public void ClearPassCache()
    {
        _cache.Clear();
    }
}
