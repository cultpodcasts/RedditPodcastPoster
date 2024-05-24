using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IAppleEnricher
{
    Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext);
}