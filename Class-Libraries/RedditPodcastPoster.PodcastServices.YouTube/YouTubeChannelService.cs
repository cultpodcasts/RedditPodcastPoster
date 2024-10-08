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
    private const string Statistics = "-statistics";

    private readonly ConcurrentDictionary<string, Channel> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

    public async Task<Channel?> GetChannel(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false,
        bool withContentDetails = false)
    {
        var cachedResult = GetCachedChannel(channelId.ChannelId, withSnippets, withContentOwnerDetails, withStatistics);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetChannel)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-id: '{channelId.ChannelId}'.");
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

        if (withStatistics)
        {
            requestScope += ",statistics";
        }

        if (withContentDetails)
        {
            requestScope += ",contentDetails";
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
            _cache[GetWriteCacheKey(channelId.ChannelId, withSnippets, withContentOwnerDetails, withStatistics)] =
                channelContentDetails;
        }

        return channelContentDetails;
    }

    public async Task FindChannel(string channelName, IndexingContext indexingContext)
    {
        throw new NotImplementedException("method not fully implemented");
#pragma warning disable CS0162 // Unreachable code detected
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindChannel)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-name: '{channelName}'.");
            return;
        }

        var channelsListRequest = youTubeService.Search.List("snippet");
        channelsListRequest.Type = "channel";
        channelsListRequest.Fields = "items/snippet/channelId";
        channelsListRequest.Q = channelName;
        SearchListResponse channelsListResponse;
        try
        {
            channelsListResponse = await channelsListRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return;
        }
#pragma warning restore CS0162 // Unreachable code detected
    }

    private string GetWriteCacheKey(
        string channelId,
        bool withSnippets,
        bool withContentOwnerDetails,
        bool withStatistics)
    {
        var key = channelId;
        if (withSnippets)
        {
            key += Snippets;
        }

        if (withContentOwnerDetails)
        {
            key += ContentOwner;
        }

        if (withStatistics)
        {
            key += Statistics;
        }

        return key;
    }

    private string[] GetReadCacheKeys(
        string channelId,
        bool withSnippets,
        bool withContentOwnerDetails,
        bool withStatistics)
    {
        // 000
        if (!withSnippets && !withContentOwnerDetails && !withStatistics)
        {
            return new[]
            {
                channelId,
                $"{channelId}{Snippets}",
                $"{channelId}{ContentOwner}",
                $"{channelId}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}",
                $"{channelId}{Snippets}{Statistics}",
                $"{channelId}{ContentOwner}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 001
        if (!withContentOwnerDetails && !withSnippets && withStatistics)
        {
            return new[]
            {
                $"{channelId}{Statistics}",
                $"{channelId}{Snippets}{Statistics}",
                $"{channelId}{ContentOwner}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 010
        if (!withContentOwnerDetails && withSnippets && !withStatistics)
        {
            return new[]
            {
                $"{channelId}{Snippets}",
                $"{channelId}{Snippets}{ContentOwner}",
                $"{channelId}{Snippets}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 011
        if (!withContentOwnerDetails && withSnippets && withStatistics)
        {
            return new[]
            {
                $"{channelId}{Snippets}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 100
        if (withContentOwnerDetails && !withSnippets && !withStatistics)
        {
            return new[]
            {
                $"{channelId}{ContentOwner}",
                $"{channelId}{Snippets}{ContentOwner}",
                $"{channelId}{ContentOwner}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 101
        if (withContentOwnerDetails && !withSnippets && withStatistics)
        {
            return new[]
            {
                $"{channelId}{ContentOwner}{Statistics}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 110
        if (withContentOwnerDetails && withSnippets && !withStatistics)
        {
            return new[]
            {
                $"{channelId}{Snippets}{ContentOwner}",
                $"{channelId}{Snippets}{ContentOwner}{Statistics}"
            };
        }

        // 111
        return new[]
        {
            $"{channelId}{Snippets}{ContentOwner}{Statistics}"
        };
    }

    private Channel? GetCachedChannel(
        string channelId,
        bool withSnippets,
        bool withContentOwnerDetails,
        bool withStatistics)
    {
        var cacheKeys = GetReadCacheKeys(channelId, withSnippets, withContentOwnerDetails, withStatistics);
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