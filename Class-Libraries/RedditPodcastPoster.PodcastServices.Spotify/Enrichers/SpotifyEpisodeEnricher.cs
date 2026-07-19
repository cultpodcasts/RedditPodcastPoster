using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Mapping;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public class SpotifyEpisodeEnricher(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IEpisodePlatformMatcher platformMatcher,
    IEpisodeCatalogueAdapter<SpotifyCatalogueInput> spotifyAdapter,
    IPlatformEnrichmentApplicator enrichmentApplicator,
    ISpotifyEnrichmentSideEffect enrichmentSideEffect,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifyEpisodeEnricher> logger)
    : PlatformEpisodeEnricherTemplate(enrichmentApplicator), ISpotifyEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (IsBypassedByDelayedYouTubePublishing(request, "Spotify", logger))
        {
            return;
        }

        var findSpotifyEpisodeRequest = FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode);
        var probeEpisode = new Episode
        {
            Title = request.Episode.Title,
            Length = request.Episode.Length,
            Release = findSpotifyEpisodeRequest.Released ?? request.Episode.Release
        };
        var assignedSpotifyIds = request.Episodes
            .Where(x => !string.IsNullOrWhiteSpace(x.SpotifyId))
            .Select(x => x.SpotifyId)
            .ToHashSet(StringComparer.Ordinal);

        var findEpisodeResult = await spotifyEpisodeResolver.FindEpisode(
            findSpotifyEpisodeRequest,
            indexingContext,
            y => !assignedSpotifyIds.Contains(y.Id) &&
                 findSpotifyEpisodeRequest.Released.HasValue &&
                 platformMatcher.CatalogueReleaseMatches(
                     probeEpisode,
                     new Episode
                     {
                         Title = y.Name,
                         Length = y.GetDuration(),
                         Release = y.GetReleaseDate(),
                         SpotifyId = y.Id
                     },
                     request.Podcast));

        if (findEpisodeResult.FullEpisode != null &&
            !findEpisodeResult.FullEpisode.IsSpotifyFree())
        {
            logger.LogWarning(
                "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false).",
                findEpisodeResult.FullEpisode.Id,
                findEpisodeResult.FullEpisode.Name);
        }
        else if (findEpisodeResult.FullEpisode != null &&
            request.Episodes.All(x => x.SpotifyId != findEpisodeResult.FullEpisode.Id))
        {
            logger.LogInformation(
                "{EnrichName} Found matching Spotify episode: '{FullEpisodeId}' with title '{FullEpisodeName}' and release-date '{FullEpisodeReleaseDate}'.",
                nameof(Enrich),
                findEpisodeResult.FullEpisode.Id,
                findEpisodeResult.FullEpisode.Name,
                findEpisodeResult.FullEpisode.ReleaseDate);

            var catalogueInput = findEpisodeResult.FullEpisode.ToCatalogueInput(htmlSanitiser);
            ApplyResolvedCandidate(request, spotifyAdapter.Adapt(catalogueInput), enrichmentContext);
        }

        enrichmentSideEffect.OnFindComplete(request.Podcast, findEpisodeResult.IsExpensiveQuery);
    }
}
