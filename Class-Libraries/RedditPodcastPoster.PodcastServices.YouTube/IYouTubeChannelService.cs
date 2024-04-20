using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeChannelService : IFlushable
{
    Task FindChannel(string channelName, IndexingContext indexingContext);

    Task<Channel?> GetChannel(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false);
}