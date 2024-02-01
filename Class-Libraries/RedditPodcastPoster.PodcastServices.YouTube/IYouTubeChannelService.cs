using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeChannelService
{
    Task<Channel?> GetChannelContentDetails(YouTubeChannelId channelId, IndexingContext indexingContext,
        bool withSnippets = false, bool withContentOwnerDetails = false);
}