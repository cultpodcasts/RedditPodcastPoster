using Api.Models;
using Api.Services.Films;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Attaches one podcast episode to an existing Film as a trailer ("Attach as trailer"). The trailer lives on the film
/// document only, so the episode stops being a playable. The order mirrors <see cref="EpisodeFilmMoveService"/>, so
/// every failure stops before anything is lost:
/// <list type="number">
/// <item>Add a <see cref="FilmTrailer"/> (id = episode id) to the film and save it, ETag-guarded (re-read on a conflict).</item>
/// <item>Push the film's search document (its <c>trailers</c> list). On failure the episode is kept.</item>
/// <item>Delete the episode's search document. On failure the episode is kept.</item>
/// <item>Re-read the film; if it no longer holds the trailer (a concurrent write dropped it), stop with the episode kept.</item>
/// <item>Delete the episode row.</item>
/// <item>Point the episode's short link at the film page (best effort).</item>
/// </list>
/// Re-sending resumes: a film that already holds the trailer while the episode row exists is the interrupted attach,
/// so the trailer is not added twice; with the episode row gone, a re-send is accepted again for that film.
/// The source podcast is never deleted or changed.
/// </summary>
public class EpisodeTrailerAttachService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ILogger<EpisodeTrailerAttachService> logger) : IEpisodeTrailerAttachService
{
    public async Task<EpisodeTrailerAttachResult> AttachAsync(
        EpisodeTrailerAttachCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.FilmId == Guid.Empty)
        {
            return BadRequest("filmId is required");
        }

        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            if (podcast is null)
            {
                return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.NotFound, Error: "Podcast not found");
            }

            var episode = await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            var film = await filmRepository.GetFilm(command.Request.FilmId);
            if (episode is null)
            {
                // A re-send after a completed attach (double click, or a lost 202): the film holds the trailer.
                if (film?.HasTrailer(command.EpisodeId) == true)
                {
                    logger.LogInformation(
                        "{method}: Episode '{episodeId}' is already a trailer of film '{filmId}'; accepting the re-send.",
                        nameof(AttachAsync), command.EpisodeId, film.Id);
                    return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Accepted, film.Id, film.Name);
                }

                return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.NotFound, Error: "Episode not found");
            }

            if (film is null)
            {
                return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.NotFound, Error: "Film not found");
            }

            if (episode.Removed)
            {
                return BadRequest("A removed episode cannot be attached as a trailer");
            }

            if (film.IsRemoved())
            {
                // Pushing a removed film's document would put it back in search.
                return BadRequest("A removed film cannot take trailers");
            }

            if (film.Id == episode.Id)
            {
                return BadRequest("An episode moved to this film cannot also be its trailer");
            }

            if (film.HasTrailer(episode.Id))
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' already holds trailer '{episodeId}' still in podcast '{podcastId}'; resuming.",
                    nameof(AttachAsync), film.Id, episode.Id, podcast.Id);
            }
            else
            {
                var trailer = FilmTrailerFactory.From(episode, command.Request.Title);
                if (trailer is null)
                {
                    return BadRequest("The episode has no link to play the trailer from");
                }

                var (saved, stop) = await AddTrailer(film.Id, trailer);
                if (stop is not null)
                {
                    return stop;
                }

                film = saved!;
            }

            if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(film)], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search push failed for film '{filmId}'. Episode row kept; re-send to resume.",
                    nameof(AttachAsync), film.Id);
                return Failed(film);
            }

            if (!await searchSwap.DeleteAsync([episode.Id], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search delete failed for episode '{episodeId}'. Episode row kept; re-send to resume.",
                    nameof(AttachAsync), episode.Id);
                return Failed(film);
            }

            // Last check before the only irreversible step: another film write since our save (one that read the film
            // before it) would have dropped the trailer, and deleting the episode then would lose it for good.
            var current = await filmRepository.GetFilm(film.Id);
            if (current?.HasTrailer(episode.Id) != true)
            {
                logger.LogError(
                    "{method}: Film '{filmId}' no longer holds trailer '{episodeId}' (changed concurrently). Episode row kept; re-send to resume.",
                    nameof(AttachAsync), film.Id, episode.Id);
                return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Failed, film.Id, film.Name);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Attached episode '{episodeId}' of podcast '{podcastId}' to film '{filmId}' as a trailer.",
                episode.Id, podcast.Id, film.Id);
            await RedirectShortLink(podcast, episode, film);
            return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Accepted, film.Id, film.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to attach episode '{episodeId}' of podcast '{podcastId}' to film '{filmId}'.",
                nameof(AttachAsync), command.EpisodeId, command.PodcastId, command.Request.FilmId);
            return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Failed);
        }
    }

    private const int MaxSaveAttempts = 3;

    /// <summary>
    /// Adds the trailer with an ETag-guarded replace, so a film write that lands between our read and our save (another
    /// attach, a film edit) is never overwritten: on a conflict the film is re-read and the trailer added again.
    /// </summary>
    private async Task<(Film? Film, EpisodeTrailerAttachResult? Stop)> AddTrailer(Guid filmId, FilmTrailer trailer)
    {
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var version = await filmRepository.GetFilmForUpdate(filmId);
            if (version is null)
            {
                return (null, new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.NotFound, Error: "Film not found"));
            }

            var latest = version.Film;
            if (latest.IsRemoved())
            {
                return (null, BadRequest("A removed film cannot take trailers"));
            }

            if (latest.HasTrailer(trailer.Id))
            {
                return (latest, null);
            }

            latest.Trailers ??= [];
            latest.Trailers.Add(trailer);
            if (await filmRepository.TryReplace(latest, version.ETag))
            {
                return (latest, null);
            }

            logger.LogWarning(
                "{method}: Film '{filmId}' changed while adding trailer '{trailerId}' (attempt {attempt} of {max}); re-reading.",
                nameof(AddTrailer), filmId, trailer.Id, attempt, MaxSaveAttempts);
        }

        logger.LogError(
            "{method}: Film '{filmId}' kept changing; trailer '{trailerId}' not added. Episode row kept; re-send to resume.",
            nameof(AddTrailer), filmId, trailer.Id);
        return (null, new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Failed, filmId));
    }

    private static EpisodeTrailerAttachResult BadRequest(string error) =>
        new(EpisodeTrailerAttachStatus.BadRequest, Error: error);

    private static EpisodeTrailerAttachResult Failed(Film film) =>
        new(EpisodeTrailerAttachStatus.Failed, film.Id, film.Name, FailureIndexing: true);

    /// <summary>
    /// The short-link key is the episode id (base 64). Unlike a film move the id leaves search, so deleting the
    /// record would break the link; instead the record points at the film page. The attach has already succeeded,
    /// so a failure is logged and does not fail the request.
    /// </summary>
    private async Task RedirectShortLink(Podcast podcast, Episode episode, Film film)
    {
        try
        {
            var path = CatalogueShortId.PlayablePath(film.Name.Trim(), film.Id, CatalogueShortId.Film);
            var result = await shortnerService.Redirect(new PodcastEpisode(podcast, episode), path);
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short link for trailer '{episodeId}' was not pointed at film '{filmId}'.",
                    nameof(RedirectShortLink), episode.Id, film.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link redirect failed for trailer '{episodeId}'.",
                nameof(RedirectShortLink), episode.Id);
        }
    }
}
