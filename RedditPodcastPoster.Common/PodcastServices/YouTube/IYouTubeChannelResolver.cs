using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeChannelResolver
{
    Task<SearchResult?> FindChannel(
        string channelName, 
        string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext);
}