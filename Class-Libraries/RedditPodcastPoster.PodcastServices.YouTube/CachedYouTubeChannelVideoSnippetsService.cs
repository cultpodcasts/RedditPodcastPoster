using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class CachedYouTubeChannelVideoSnippetsService(
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    ILogger<CachedYouTubeChannelVideoSnippetsService> logger)
    : ICachedYouTubeChannelVideoSnippetsService
{
    private static readonly ConcurrentDictionary<string, IList<SearchResult>> Cache = new();

    public void Flush()
    {
        Cache.Clear();
    }

    public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext)
    {
        if (Cache.TryGetValue(channelId.ChannelId, out var snippets))
        {
            return snippets;
        }

        var result = await youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(channelId, indexingContext);

        Cache[channelId.ChannelId] = result;

        return result;
    }
}