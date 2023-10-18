using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeChannelService : IFlushable
{
    Task FindChannel(string channelName, IndexingContext indexingContext);

    Task<Channel?> GetChannelContentDetails(YouTubeChannelId channelId, IndexingContext indexingContext,
        bool withSnippets = false, bool withContentOwnerDetails = false);
}