using System.Collections.Concurrent;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeChannelService(
    YouTubeService youTubeService,
    ILogger<YouTubeChannelService> logger)
    : IYouTubeChannelService
{
    private const string Snippets = "-snippets";
    private const string ContentOwner = "-contentOwner";

    private readonly ConcurrentDictionary<string, Channel> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

  

    public async Task<Channel?> GetChannelContentDetails(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false)
    {
        logger.LogInformation($"YOUTUBE: GetFullEpisode channel for channel-id {channelId}.");

        var cachedResult = GetCachedChannel(channelId.ChannelId, withSnippets, withContentOwnerDetails);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetChannelContentDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
            return null;
        }

        var requestScope = "contentDetails";
        if (withSnippets)
        {
            requestScope = "snippet," + requestScope;
        }

        if (withContentOwnerDetails)
        {
            requestScope += ",contentOwnerDetails";
        }

        var listRequest = youTubeService.Channels.List(requestScope);
        listRequest.Id = channelId.ChannelId;
        ChannelListResponse result;
        try
        {
            result = await listRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return null;
        }

        var channelContentDetails = result.Items.SingleOrDefault();
        if (channelContentDetails != null)
        {
            _cache[GetWriteCacheKey(channelId.ChannelId, withSnippets, withContentOwnerDetails)] = channelContentDetails;
        }

        return channelContentDetails;
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
}