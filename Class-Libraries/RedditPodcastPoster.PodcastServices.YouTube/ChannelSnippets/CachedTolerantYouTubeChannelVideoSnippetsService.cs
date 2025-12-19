using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class CachedTolerantYouTubeChannelVideoSnippetsService(
    ITolerantYouTubeChannelVideoSnippetsService tolerantYouTubeChannelVideoSnippetsService,
    ILogger<CachedTolerantYouTubeChannelVideoSnippetsService> logger)
    : ICachedTolerantYouTubeChannelVideoSnippetsService
{
    private static readonly ConcurrentDictionary<string, IList<SearchResult>> Cache = new();

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        if (Cache.TryGetValue(channelId.ChannelId, out var snippets))
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
            Cache[channelId.ChannelId] = result;
        }

        return result;
    }

    public void Flush()
    {
        Cache.Clear();
    }
}