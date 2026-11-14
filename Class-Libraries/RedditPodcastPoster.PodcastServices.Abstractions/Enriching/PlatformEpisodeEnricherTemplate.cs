using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

/// <summary>
/// Shared resolve→adapt→apply wiring for indexing platform enrichers.
/// Orchestration concerns (SkipEnrichingFromYouTube, delayed-publishing second pass) stay in the indexing orchestrator.
/// </summary>
public abstract class PlatformEpisodeEnricherTemplate(IPlatformEnrichmentApplicator enrichmentApplicator)
{
    protected IPlatformEnrichmentApplicator EnrichmentApplicator => enrichmentApplicator;

    protected PlatformEnrichmentResult ApplyResolvedCandidate(
        EnrichmentRequest request,
        EpisodeCandidate candidate,
        EnrichmentContext enrichmentContext)
    {
        var result = enrichmentApplicator.Apply(request.Podcast, request.Episode, candidate);
        result.ApplyTo(enrichmentContext);
        return result;
    }
}
