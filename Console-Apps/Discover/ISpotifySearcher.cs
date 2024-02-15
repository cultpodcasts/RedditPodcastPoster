using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public interface ISpotifySearcher
{
    Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}