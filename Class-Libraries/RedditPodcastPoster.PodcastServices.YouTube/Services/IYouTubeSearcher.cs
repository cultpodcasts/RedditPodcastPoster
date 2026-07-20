using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeSearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}
