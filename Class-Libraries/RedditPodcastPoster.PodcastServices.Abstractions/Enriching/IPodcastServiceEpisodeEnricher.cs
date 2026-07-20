using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

public interface IPodcastServiceEpisodeEnricher
{
    Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext);
}