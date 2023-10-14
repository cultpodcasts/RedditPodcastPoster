using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeChannelVideoSnippetsService : IFlushable
{
    Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext);
}