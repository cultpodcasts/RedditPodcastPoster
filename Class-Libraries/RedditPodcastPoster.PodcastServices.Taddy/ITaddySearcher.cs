using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public interface ITaddySearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}