using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeVideoService
{
    Task<IList<Video>?> GetVideoContentDetails(
        IEnumerable<string> videoIds,
        IndexingContext options,
        bool withSnippets = false,
        bool withStatistics = false);
}