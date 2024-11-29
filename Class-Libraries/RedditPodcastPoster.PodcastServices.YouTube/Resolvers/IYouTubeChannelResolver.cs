using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public interface IYouTubeChannelResolver
{
    Task<SearchResult?> FindChannelsSnippets(
        string channelName,
        string mostRecentlyUploadVideoTitle,
        IndexingContext indexingContext);
}