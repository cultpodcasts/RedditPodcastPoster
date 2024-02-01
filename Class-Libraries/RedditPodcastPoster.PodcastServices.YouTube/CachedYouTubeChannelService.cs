using System.Collections.Concurrent;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class CachedYouTubeChannelService(
    IYouTubeChannelService youTubeChannelService,
    ILogger<CachedYouTubeChannelService> logger) : ICachedYouTubeChannelService
{
    private const string Snippets = "-snippets";
    private const string ContentOwner = "-contentOwner";

    private readonly ConcurrentDictionary<string, Channel> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

    public async Task<Channel?> GetChannelContentDetails(YouTubeChannelId channelId, IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false)
    {
        var cachedResult = GetCachedChannel(channelId.ChannelId, withSnippets, withContentOwnerDetails);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var result =
            await youTubeChannelService.GetChannelContentDetails(channelId, indexingContext, withSnippets,
                withContentOwnerDetails);

        if (result != null)
        {
            _cache[GetWriteCacheKey(channelId.ChannelId, withSnippets, withContentOwnerDetails)] = result;
        }

        return result;
    }

    private Channel? GetCachedChannel(string channelId, bool withSnippets, bool withContentOwnerDetails)
    {
        var cacheKeys = GetReadCacheKeys(channelId, withSnippets, withContentOwnerDetails);
        foreach (var cacheKey in cacheKeys)
        {
            if (_cache.TryGetValue(cacheKey, out var channel))
            {
                return channel;
            }
        }

        return null;
    }

    private string[] GetReadCacheKeys(string channelId, bool withSnippets, bool withContentOwnerDetails)
    {
        if (!withSnippets && !withContentOwnerDetails)
        {
            return new[]
            {
                channelId,
                $"{channelId}{Snippets}",
                $"{channelId}{ContentOwner}",
                $"{channelId}{Snippets}{ContentOwner}"
            };
        }

        if (withContentOwnerDetails && !withSnippets)
        {
            return new[]
            {
                $"{channelId}{ContentOwner}",
                $"{channelId}{Snippets}{ContentOwner}"
            };
        }

        if (!withContentOwnerDetails && withSnippets)
        {
            return new[]
            {
                $"{channelId}{Snippets}",
                $"{channelId}{Snippets}{ContentOwner}"
            };
        }

        return new[]
        {
            $"{channelId}{Snippets}{ContentOwner}"
        };
    }

    private string GetWriteCacheKey(string channelId, bool withSnippets, bool withContentOwnerDetails)
    {
        if (!withSnippets && !withContentOwnerDetails)
        {
            return channelId;
        }

        if (withContentOwnerDetails && !withSnippets)
        {
            return $"{channelId}{ContentOwner}";
        }

        if (!withContentOwnerDetails && withSnippets)
        {
            return $"{channelId}{Snippets}";
        }

        return $"{channelId}{Snippets}{ContentOwner}";
    }
}