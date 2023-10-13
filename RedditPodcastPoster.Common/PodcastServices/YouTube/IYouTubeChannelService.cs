using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeChannelService
{
    Task FindChannel(string channelName, IndexingContext indexingContext);

    Task<Channel?> GetChannelContentDetails(YouTubeChannelId channelId, IndexingContext indexingContext,
        bool withSnippets = false, bool withContentOwnerDetails = false);
}