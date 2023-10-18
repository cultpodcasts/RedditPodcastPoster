using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastServiceEpisodeEnricher
{
    Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext);
}