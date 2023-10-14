using System.Collections.Concurrent;
using System.Text;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeChannelService : IYouTubeChannelService
{
    private const string Snippets = "-snippets";
    private const string ContentOwner = "-contentOwner";

    private readonly ILogger<YouTubeChannelService> _logger;
    private readonly YouTubeService _youTubeService;

    private readonly ConcurrentDictionary<string, Channel> Cache = new();

    public YouTubeChannelService(YouTubeService youTubeService,
        ILogger<YouTubeChannelService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task FindChannel(string channelName, IndexingContext indexingContext)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindChannel)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Channel-name: '{channelName}'.");
            return;
        }

        var channelsListRequest = _youTubeService.Search.List("snippet");
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
            _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return;
        }

        throw new NotImplementedException("method not fully implemented");
    }

    public async Task<Channel?> GetChannelContentDetails(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false)
    {
        _logger.LogInformation($"YOUTUBE: GetFullEpisode channel for channel-id {channelId}.");

        var cachedResult = GetCachedChannel(channelId.ChannelId, withSnippets, withContentOwnerDetails);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
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

        var listRequest = _youTubeService.Channels.List(requestScope);
        listRequest.Id = channelId.ChannelId;
        ChannelListResponse result;
        try
        {
            result = await listRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
            indexingContext.SkipYouTubeUrlResolving = true;
            return null;
        }

        if (result.Items.Any())
        {
            try
            {
                var sb = new StringBuilder();
                var jsonSerialiser = new JsonSerializer();
                await using var jsonWriter = new JsonTextWriter(new StringWriter(sb));
                jsonSerialiser.Serialize(jsonWriter, result);
                _logger.LogInformation($"YOUTUBE: {nameof(GetChannelContentDetails)} - {sb}");
            }
            catch
            {
                _logger.LogInformation($"YOUTUBE: {nameof(GetChannelContentDetails)} - Could not serialise response.");
            }
        }

        var channelContentDetails = result.Items.SingleOrDefault();
        if (channelContentDetails != null)
        {
            Cache[GetWriteCacheKey(channelId.ChannelId, withSnippets, withContentOwnerDetails)] = channelContentDetails;
        }

        return channelContentDetails;
    }

    public void Flush()
    {
        Cache.Clear();
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
            if (Cache.TryGetValue(cacheKey, out var channel))
            {
                return channel;
            }
        }

        return null;
    }
}