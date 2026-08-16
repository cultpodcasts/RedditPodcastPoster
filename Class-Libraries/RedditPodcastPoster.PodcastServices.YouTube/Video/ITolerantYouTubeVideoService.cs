using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;

namespace RedditPodcastPoster.PodcastServices.YouTube.Video;

public interface ITolerantYouTubeVideoService
{
    Task<IList<Google.Apis.YouTube.v3.Data.Video>?> GetVideoContentDetails(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        IEnumerable<string> videoIds,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withStatistics = false,
        bool withStatus = false);
}
