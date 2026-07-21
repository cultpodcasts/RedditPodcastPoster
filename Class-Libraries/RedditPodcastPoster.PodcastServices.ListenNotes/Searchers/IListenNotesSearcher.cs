using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Searchers;

public interface IListenNotesSearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}
