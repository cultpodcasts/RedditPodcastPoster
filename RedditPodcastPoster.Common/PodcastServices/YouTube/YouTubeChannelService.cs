using System.Text;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeChannelService : IYouTubeChannelService
{
    private readonly ILogger<YouTubeChannelService> _logger;
    private readonly YouTubeService _youTubeService;

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

        return result.Items.SingleOrDefault();
    }
}