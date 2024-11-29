using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;

namespace RedditPodcastPoster.PodcastServices.YouTube.Video;

public interface IYouTubeVideoService
{
    Task<IList<Google.Apis.YouTube.v3.Data.Video>?> GetVideoContentDetails(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        IEnumerable<string> videoIds,
        IndexingContext options,
        bool withSnippets = false,
        bool withStatistics = false);
}