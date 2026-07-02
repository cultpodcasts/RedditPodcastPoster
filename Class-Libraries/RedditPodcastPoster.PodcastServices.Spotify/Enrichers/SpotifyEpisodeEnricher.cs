using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public class SpotifyEpisodeEnricher(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifyEpisodeEnricher> logger)
    : ISpotifyEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            var timeSpan = request.Podcast.YouTubePublishingDelay().ToString("g");
            logger.LogInformation(
                "'{method}': Bypassing enriching of '{requestEpisodeTitle}' with release-date of '{requestEpisodeRelease:R}' from Spotify as it is within the {nameof(request.Podcast.YouTubePublishingDelay)} which is '{timeSpan}'.",
                nameof(Enrich), request.Episode.Title, request.Episode.Release,
                nameof(request.Podcast.YouTubePublishingDelay), timeSpan);
            return;
        }

        var findSpotifyEpisodeRequest = FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode);
        var ticks = EpisodeReleaseMatchTolerance.GetToleranceTicks(request.Podcast, request.Episode.Length);
        var assignedSpotifyIds = request.Episodes
            .Where(x => !string.IsNullOrWhiteSpace(x.SpotifyId))
            .Select(x => x.SpotifyId)
            .ToHashSet(StringComparer.Ordinal);

        var findEpisodeResult = await spotifyEpisodeResolver.FindEpisode(
            findSpotifyEpisodeRequest,
            indexingContext,
            y => !assignedSpotifyIds.Contains(y.Id) &&
                 findSpotifyEpisodeRequest.Released.HasValue &&
                 EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
                     y.GetReleaseDate(),
                     findSpotifyEpisodeRequest.Released.Value,
                     ticks));

        if (findEpisodeResult.FullEpisode != null &&
            request.Episodes.All(x => x.SpotifyId != findEpisodeResult.FullEpisode.Id))
        {
            logger.LogInformation(
                "{EnrichName} Found matching Spotify episode: '{FullEpisodeId}' with title '{FullEpisodeName}' and release-date '{FullEpisodeReleaseDate}'.",
                nameof(Enrich),
                findEpisodeResult.FullEpisode.Id,
                findEpisodeResult.FullEpisode.Name,
                findEpisodeResult.FullEpisode.ReleaseDate);
            request.Episode.SpotifyId = findEpisodeResult.FullEpisode.Id;
            var url = findEpisodeResult.FullEpisode.GetUrl();
            request.Episode.Urls.Spotify = url;
            var image = findEpisodeResult.FullEpisode.GetBestImageUrl();
            if (image != null)
            {
                request.Episode.Images ??= new EpisodeImages();
                request.Episode.Images.Spotify = image;
            }

            enrichmentContext.Spotify = url;
            var description = htmlSanitiser.Sanitise(findEpisodeResult.FullEpisode.HtmlDescription);
            if (string.IsNullOrWhiteSpace(request.Episode.Description) &&
                !string.IsNullOrWhiteSpace(description))
            {
                request.Episode.Description = description;
            }
        }

        // Spotify-specific: episode list queries can hit API throttling. Apple has no equivalent concern.
        if (findEpisodeResult.IsExpensiveQuery)
        {
            request.Podcast.SpotifyEpisodesQueryIsExpensive = true;
        }
    }
}
