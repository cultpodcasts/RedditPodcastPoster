using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IAppleEnricher
{
    Task Enrich(IList<EpisodeResult> results, IndexingContext indexingContext);
}