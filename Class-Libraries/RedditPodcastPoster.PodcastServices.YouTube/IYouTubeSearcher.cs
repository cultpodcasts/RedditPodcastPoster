using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeSearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}