namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastServiceEpisodeEnricher
{
    Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext);
}