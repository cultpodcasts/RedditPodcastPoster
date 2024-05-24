using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.ListenNotes;

public interface IListenNotesSearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}