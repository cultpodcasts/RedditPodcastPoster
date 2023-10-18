using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeChannelResolver
{
    Task<SearchResult?> FindChannelsSnippets(
        string channelName, 
        string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext);
}