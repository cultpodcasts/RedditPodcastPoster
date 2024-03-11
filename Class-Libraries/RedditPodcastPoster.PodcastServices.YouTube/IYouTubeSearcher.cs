using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeSearcher
{
    Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}