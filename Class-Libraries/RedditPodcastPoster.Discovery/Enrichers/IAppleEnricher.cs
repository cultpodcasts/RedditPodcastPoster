using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Enrichers;

public interface IAppleEnricher
{
    Task Enrich(IList<EpisodeResult> results, IndexingContext indexingContext);
}
