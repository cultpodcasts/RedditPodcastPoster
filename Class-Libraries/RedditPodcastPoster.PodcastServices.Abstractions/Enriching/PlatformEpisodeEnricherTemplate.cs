using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Podcasts;
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

    protected bool IsBypassedByDelayedYouTubePublishing(
        EnrichmentRequest request,
        string platformName,
        ILogger logger)
    {
        if (!request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            return false;
        }

        var timeSpan = request.Podcast.YouTubePublishingDelay().ToString("g");
        logger.LogInformation(
            "'{method}': Bypassing enriching of '{requestEpisodeTitle}' with release-date of '{requestEpisodeRelease:R}' " +
            "from {platformName} as it is within the {delayProperty} which is '{timeSpan}'.",
            nameof(IsBypassedByDelayedYouTubePublishing),
            request.Episode.Title,
            request.Episode.Release,
            platformName,
            nameof(Podcast.YouTubePublicationOffset),
            timeSpan);
        return true;
    }

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
