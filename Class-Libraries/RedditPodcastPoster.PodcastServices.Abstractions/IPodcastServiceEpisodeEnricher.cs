using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastServiceEpisodeEnricher
{
    Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext);
}