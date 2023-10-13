using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeVideoService
{
    Task<IList<Video>?> GetVideoContentDetails(IEnumerable<string> videoIds, IndexingContext options,
        bool withSnippets = false);
}