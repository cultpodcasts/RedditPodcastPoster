using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery;

public interface ISpotifyEnricher
{
    Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext);
}
