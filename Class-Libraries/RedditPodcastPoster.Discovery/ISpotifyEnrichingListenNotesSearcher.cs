using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface ISpotifyEnrichingListenNotesSearcher
{
    Task<IEnumerable<EpisodeResult>> Search(
        string query,
        IndexingContext indexingContext,
        bool enrichFromSpotify);
}