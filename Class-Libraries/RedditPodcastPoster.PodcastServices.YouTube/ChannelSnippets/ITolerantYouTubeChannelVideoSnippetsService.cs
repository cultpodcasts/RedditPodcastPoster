using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public interface ITolerantYouTubeChannelVideoSnippetsService
{
    Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(
        YouTubeChannelId channelId,
        IndexingContext indexingContext);
}
