using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.ListenNotes;

public interface IListenNotesSearcher
{
    Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}