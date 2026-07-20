using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public interface ITaddySearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}
