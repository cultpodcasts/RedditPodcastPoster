using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeChannelVideoSnippetsService
{
    Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubeChannelId channelId,
        IndexingContext indexingContext);
}