using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface ITolerantYouTubeSearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}
