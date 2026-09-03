using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Mapping;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

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
        if (IsAudioCatalogueEnrichmentBypassedByDelayedYouTubePublishing(request, "Spotify", logger))
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
            .Select(EpisodeServicePresence.SpotifyEpisodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var findEpisodeResult = await spotifyEpisodeResolver.FindEpisode(
            findSpotifyEpisodeRequest,
            indexingContext,
            y => !assignedSpotifyIds.Contains(y.Id) &&
                 findSpotifyEpisodeRequest.Released.HasValue &&
                 platformMatcher.CatalogueReleaseMatches(
                     probeEpisode,
                     CreateSpotifyCandidate(y),
                     request.Podcast));

        if (findEpisodeResult.FullEpisode != null &&
            !findEpisodeResult.FullEpisode.IsSpotifyFree())
        {
            SpotifyNonPlayableSkipLogger.Log(
                logger,
                findEpisodeResult.FullEpisode,
                findSpotifyEpisodeRequest.Market ?? Market.CountryCode);
        }
        else if (findEpisodeResult.FullEpisode != null &&
            request.Episodes.All(x =>
                EpisodeServicePresence.SpotifyEpisodeId(x) != findEpisodeResult.FullEpisode.Id))
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
        else if (findEpisodeResult.FullEpisode == null)
        {
            logger.LogWarning(
                "Spotify enrich miss: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' podcast-name='{PodcastName}' spotify-show-id='{SpotifyShowId}' expected-release='{ExpectedRelease}' length='{Length}' youtube-discovered='{YouTubeDiscovered}' release-authority='{ReleaseAuthority}' delay='{Delay}' expensive-query='{ExpensiveQuery}'",
                request.Episode.Id,
                request.Episode.Title,
                request.Podcast.Id,
                request.Podcast.Name,
                request.Podcast.SpotifyId,
                findSpotifyEpisodeRequest.Released ?? request.Episode.Release,
                request.Episode.Length,
                findSpotifyEpisodeRequest.EnrichingYouTubeDiscoveredEpisode,
                request.Podcast.ReleaseAuthority,
                findSpotifyEpisodeRequest.YouTubePublishingDelay,
                findEpisodeResult.IsExpensiveQuery);
        }

        enrichmentSideEffect.OnFindComplete(request.Podcast, findEpisodeResult.IsExpensiveQuery);
    }

    private static Episode CreateSpotifyCandidate(SimpleEpisode spotify)
    {
        var candidate = new Episode
        {
            Title = spotify.Name,
            Length = spotify.GetDuration(),
            Release = spotify.GetReleaseDate()
        };
        EpisodeServicePresence.SetSpotifyIdentity(candidate, spotify.Id);
        return candidate;
    }
}

