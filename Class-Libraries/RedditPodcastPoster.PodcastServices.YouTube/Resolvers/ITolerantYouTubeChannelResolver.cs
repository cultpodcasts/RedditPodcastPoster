using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public interface ITolerantYouTubeChannelResolver
{
    Task<SearchResult?> FindChannelsSnippets(
        string channelName,
        string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext);
}
