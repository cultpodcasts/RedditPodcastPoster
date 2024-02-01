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
    public async Task<Channel?> GetChannelContentDetails(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false)
    {
        logger.LogInformation($"YOUTUBE: GetFullEpisode channel for channel-id {channelId}.");


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
        return channelContentDetails;
    }
}