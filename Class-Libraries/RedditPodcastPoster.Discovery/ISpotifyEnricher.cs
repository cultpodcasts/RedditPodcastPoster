using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface ISpotifyEnricher
{
    Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext);
}