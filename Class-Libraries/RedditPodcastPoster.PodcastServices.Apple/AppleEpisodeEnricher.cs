using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeEnricher(
    IApplePodcastEnricher applePodcastEnricher,
    IAppleEpisodeResolver appleEpisodeResolver,
    IEpisodePlatformMatcher platformMatcher,
    IEpisodeCatalogueAdapter<AppleCatalogueInput> appleAdapter,
    IPlatformEnrichmentApplicator enrichmentApplicator,
    ILogger<AppleEpisodeEnricher> logger)
    : PlatformEpisodeEnricherTemplate(enrichmentApplicator), IAppleEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (IsBypassedByDelayedYouTubePublishing(request, "Apple", logger))
        {
            return;
        }

        if (request.Podcast.AppleId == null)
        {
            await applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId == null)
        {
            return;
        }

        var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode);
        var probeEpisode = new Episode
        {
            Title = request.Episode.Title,
            Length = request.Episode.Length,
            Release = findAppleEpisodeRequest.Released ?? request.Episode.Release
        };
        var assignedAppleIds = request.Episodes
            .Where(x => x.AppleId is > 0)
            .Select(x => x.AppleId!.Value)
            .ToHashSet();

        var appleItem = await appleEpisodeResolver.FindEpisode(
            findAppleEpisodeRequest,
            indexingContext,
            y => !assignedAppleIds.Contains(y.Id) &&
                 findAppleEpisodeRequest.Released.HasValue &&
                 platformMatcher.CatalogueReleaseMatches(
                     probeEpisode,
                     new Episode
                     {
                         Title = y.Title,
                         Length = y.Duration,
                         Release = y.Release,
                         AppleId = y.Id
                     },
                     request.Podcast));
        if (appleItem == null || request.Episodes.Any(x => x.AppleId == appleItem.Id))
        {
            return;
        }

        logger.LogInformation(
            "Episode.Release.TimeOfDay: '{ReleaseTimeOfDay:G}' podcast-id '{PodcastId}' with episode with apple-id '{AppleItemId}'."
            , request.Episode.Release.TimeOfDay, request.Podcast.Id, appleItem.Id);

        var catalogueInput = new AppleCatalogueInput(
            appleItem.Id,
            appleItem.Title,
            appleItem.Description,
            appleItem.Duration,
            appleItem.Release,
            appleItem.Url.CleanAppleUrl(),
            appleItem.Image);
        ApplyResolvedCandidate(request, appleAdapter.Adapt(catalogueInput), enrichmentContext);
    }
}
