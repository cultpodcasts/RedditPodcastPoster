using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public interface IListenNotesSearcher
{
    Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}