using Api.Models;
using Api.Services.Films;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Attaches one podcast episode to a Film as a trailer ("Attach as trailer"): an existing film by id, or with
/// <c>createFilm</c> the film named <c>filmName</c>, created first when no film has the name (an unreleased film the
/// trailer comes out before; see <see cref="ResolveNamedFilm"/>). The trailer lives on the film
/// document only, so the episode stops being a playable. The order mirrors <see cref="EpisodeFilmMoveService"/>, so
/// every failure stops before anything is lost:
/// <list type="number">
/// <item>Claim the episode for the film in LookUps (<see cref="IFilmTrailerClaimStore"/>, a conditional create). Of
/// two attaches of one episode to different films exactly one wins; the other stops with 409 before writing any film,
/// so nothing is ever undone and no film is deleted.</item>
/// <item>Add a <see cref="FilmTrailer"/> (id = episode id) to the film and save it, ETag-guarded (re-read on a conflict).
/// Before the add, films are queried across partitions for that id in <c>trailers[].id</c>. Another film that already
/// holds it returns 409 naming that film and the episode stays, so one episode cannot be a trailer of two films.</item>
/// <item>Push the film's search document (its <c>trailers</c> list). On failure the episode is kept.</item>
/// <item>Delete the episode's search document. On failure the episode is kept.</item>
/// <item>Re-read the film. If it was deleted, best-effort re-upload the episode search document and stop with the episode kept. If it no longer holds the trailer (a concurrent write dropped it), stop with the episode kept.</item>
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
    ICatalogueIdentityEnricher identityEnricher,
    IFileKeyAllocator fileKeyAllocator,
    IFilmTrailerClaimStore trailerClaims,
    TimeProvider timeProvider,
    ILogger<EpisodeTrailerAttachService> logger) : IEpisodeTrailerAttachService
{
    /// <summary>
    /// A claim whose film does not hold the trailer is an attach still running (seconds) or one that failed. Only a
    /// claim older than this is taken over.
    /// </summary>
    internal static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(10);

    public async Task<EpisodeTrailerAttachResult> AttachAsync(
        EpisodeTrailerAttachCommand command,
        CancellationToken cancellationToken)
    {
        var createFilm = command.Request.CreateFilm;
        if (createFilm && command.Request.FilmId != Guid.Empty)
        {
            return BadRequest("Give filmId or createFilm with filmName, not both");
        }

        if (!createFilm && command.Request.FilmId == Guid.Empty)
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
            Film? film;
            var filmCreated = false;
            if (createFilm)
            {
                var (target, created, stop) = await ResolveNamedFilm(command, episode, cancellationToken);
                if (stop is not null)
                {
                    return stop;
                }

                film = target;
                filmCreated = created;
            }
            else
            {
                film = await filmRepository.GetFilm(command.Request.FilmId);
            }

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

            // A film created by this call already holds the trailer (it was in the film's only write).
            if (film.HasTrailer(episode.Id) && !filmCreated)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' already holds trailer '{episodeId}' still in podcast '{podcastId}'; resuming.",
                    nameof(AttachAsync), film.Id, episode.Id, podcast.Id);
            }
            else if (!film.HasTrailer(episode.Id))
            {
                var trailer = FilmTrailerFactory.From(episode, command.Request.Title);
                if (trailer is null)
                {
                    return BadRequest("The episode has no link to play the trailer from");
                }

                var claimStop = await ClaimTrailer(episode.Id, film.Id);
                if (claimStop is not null)
                {
                    return claimStop;
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
            if (current is null)
            {
                // The episode search document is already gone. A re-send finds no film and returns 404, so nothing
                // else puts the episode back into search. Restore it now; the episode row stays.
                logger.LogWarning(
                    "TrailerAttachFilmGone: Film '{filmId}' was deleted after the trailer save. Re-uploading the search document for episode '{episodeId}'.",
                    film.Id, episode.Id);
                await RestoreEpisodeSearchDocument(podcast, episode, film.Id, cancellationToken);
                return Failed(film);
            }

            if (!current.HasTrailer(episode.Id))
            {
                logger.LogError(
                    "{method}: Film '{filmId}' no longer holds trailer '{episodeId}' (changed concurrently). Episode row kept; re-send to resume.",
                    nameof(AttachAsync), film.Id, episode.Id);
                // Search already lists the trailer on the film and no longer has the episode, so report it as an
                // indexing failure: the client prompts a re-send, which puts both back in step.
                return Failed(film);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Attached episode '{episodeId}' of podcast '{podcastId}' to film '{filmId}' as a trailer.",
                episode.Id, podcast.Id, film.Id);
            await RedirectShortLink(podcast, episode, film);
            return new EpisodeTrailerAttachResult(
                EpisodeTrailerAttachStatus.Accepted, film.Id, film.Name, FilmCreated: filmCreated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to attach episode '{episodeId}' of podcast '{podcastId}' to film '{filmId}'.",
                nameof(AttachAsync), command.EpisodeId, command.PodcastId, command.Request.FilmId);
            return new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Failed);
        }
    }

    /// <summary>
    /// The <c>createFilm</c> target: the film named <c>filmName</c>, created when no film has the name. Every episode
    /// check that can stop the attach runs first, so a refused attach never leaves a new, empty film behind.
    /// <list type="bullet">
    /// <item>A film that already holds this trailer is the interrupted (or completed) attach: it is reused when its name
    /// matches, and is a 409 naming that film otherwise.</item>
    /// <item>One film with the name (case-insensitive) is reused, the way Move to TV show's <c>createShow</c> joins an
    /// existing show. More than one is a 409.</item>
    /// <item>Otherwise the new film goes through the same name and identity gate as Move to film
    /// (<see cref="FilmMoveTarget"/>): an ambiguous IMDb/TMDB match returns the candidates and writes nothing. The film
    /// has no release, services or video (an unreleased film); the curator can add them later.</item>
    /// <item>The new film is written once, already holding the trailer, with a conditional create under an id derived
    /// from the name (<see cref="NewFilmId"/>). Overlapping creates of the same name converge on that one document
    /// (the loser re-reads it and resumes), and no empty film is ever stored: any later failure leaves a film holding
    /// the trailer with the episode row kept, which a re-send resumes. A film that took the same episode while this one
    /// was created wins, and the film just created is deleted (409).</item>
    /// </list>
    /// Re-sending after a failure finds the film by its trailer or its name, so a film is never created twice.
    /// </summary>
    private async Task<(Film? Film, bool Created, EpisodeTrailerAttachResult? Stop)> ResolveNamedFilm(
        EpisodeTrailerAttachCommand command,
        Episode? episode,
        CancellationToken cancellationToken)
    {
        var name = command.Request.FilmName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return (null, false, BadRequest("filmName is required with createFilm"));
        }

        var holder = await FirstFilmHoldingTrailer(command.EpisodeId);
        if (episode is null)
        {
            // A re-send after a completed attach: the episode row is gone and the film holds the trailer.
            return holder is not null
                ? (null, false, new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Accepted, holder.Id, holder.Name))
                : (null, false, new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.NotFound, Error: "Episode not found"));
        }

        if (episode.Removed)
        {
            return (null, false, BadRequest("A removed episode cannot be attached as a trailer"));
        }

        var trailer = FilmTrailerFactory.From(episode, command.Request.Title);
        if (trailer is null)
        {
            return (null, false, BadRequest("The episode has no link to play the trailer from"));
        }

        if (holder is not null)
        {
            if (string.Equals(holder.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return (holder, false, null);
            }

            return (null, false, Conflict(holder.Id, "This episode is already a trailer of another film"));
        }

        var matches = await PublisherNameAttachLookup.FindByName(filmRepository, name, cancellationToken);
        if (matches.Count == 1)
        {
            logger.LogInformation(
                "{method}: Film '{filmId}' already has the name '{name}'; attaching the trailer to it.",
                nameof(ResolveNamedFilm), matches[0].Id, name);
            return (matches[0], false, null);
        }

        if (matches.Count > 1)
        {
            return (null, false, Conflict(matches[0].Id, "More than one film has this name; attach to one of them by id"));
        }

        var (stop, filmName, admission) = await FilmMoveTarget.PrepareNewFilm(
            null,
            null,
            new EpisodeFilmMoveRequest
            {
                Title = name,
                IdentityDecision = command.Request.IdentityDecision,
                SelectedIdentityKey = command.Request.SelectedIdentityKey
            },
            filmRepository,
            identityEnricher,
            logger,
            cancellationToken);
        if (stop is { Status: EpisodeFilmMoveStatus.Conflict, FilmId: Guid clashId } &&
            await filmRepository.GetFilm(clashId) is { } created)
        {
            // Another request created the film between the name query above and this check: use it.
            logger.LogInformation(
                "{method}: Film '{filmId}' '{name}' appeared while checking the name; attaching to it.",
                nameof(ResolveNamedFilm), created.Id, created.Name);
            return (created, false, null);
        }

        if (stop is not null)
        {
            return (null, false, ToTrailerResult(stop));
        }

        // One write, already holding the trailer, under an id derived from the name: overlapping creates for the same
        // name converge on one document, and no empty film is ever stored.
        // The owner check above is only a query. The claim is the one conditional write that decides which film gets
        // the episode; a call that loses it stops here, before writing anything else.
        var filmId = NewFilmId(filmName);
        var claimStop = await ClaimTrailer(episode.Id, filmId);
        if (claimStop is not null)
        {
            return (null, false, claimStop);
        }

        var film = new Film(filmName) { Id = filmId, Description = string.Empty, Trailers = [trailer] };
        film.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.Film, filmName, film.Id);
        FilmMoveTarget.ApplyIdentity(film, admission, identityEnricher, logger);

        if (!await filmRepository.TryCreate(film))
        {
            return await ResolveCreateConflict(film.Id, filmName);
        }

        logger.LogInformation(
            "{method}: Created film '{filmId}' '{name}' (imdb={imdbId}, tmdb={tmdbId}) holding trailer '{episodeId}'.",
            nameof(ResolveNamedFilm), film.Id, film.Name, film.ImdbId, film.TmdbId, episode.Id);

        return (film, true, null);
    }

    /// <summary>
    /// Claims the episode as a trailer of <paramref name="filmId"/> (see <see cref="IFilmTrailerClaimStore"/>). Null
    /// when this film owns it; otherwise a 409 naming the film that does. Called before the first write of the trailer
    /// to any film, so a loser writes nothing and nothing has to be undone.
    /// </summary>
    private async Task<EpisodeTrailerAttachResult?> ClaimTrailer(Guid episodeId, Guid filmId)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var attempt = await trailerClaims.TryClaim(episodeId, filmId, now);
        if (attempt.Claimed)
        {
            return null;
        }

        var holder = attempt.Holder!;
        var holderFilm = await filmRepository.GetFilm(holder.FilmId);
        if (holderFilm?.HasTrailer(episodeId) == true)
        {
            return Conflict(holder.FilmId, "This episode is already a trailer of another film");
        }

        if (now - holder.ClaimedUtc < ClaimStaleAfter || attempt.HolderETag is null ||
            !await trailerClaims.TryTakeOver(episodeId, filmId, now, attempt.HolderETag))
        {
            logger.LogWarning(
                "{method}: Episode '{episodeId}' is claimed as a trailer of film '{holderFilmId}' since {claimedUtc:O}; refusing film '{filmId}'.",
                nameof(ClaimTrailer), episodeId, holder.FilmId, holder.ClaimedUtc, filmId);
            return Conflict(holder.FilmId, "Another attach of this episode as a trailer is in progress; try again shortly");
        }

        return null;
    }

    /// <summary>
    /// Another call created the film for this name first (or the name's id belongs to a film renamed since). The same
    /// name goes on through the normal attach, which resumes when that film already holds the trailer.
    /// </summary>
    private async Task<(Film? Film, bool Created, EpisodeTrailerAttachResult? Stop)> ResolveCreateConflict(
        Guid filmId,
        string filmName)
    {
        var existing = await filmRepository.GetFilm(filmId);
        if (existing is null)
        {
            return (null, false, new EpisodeTrailerAttachResult(
                EpisodeTrailerAttachStatus.Failed, Error: "The film changed while it was created; send again"));
        }

        if (!string.Equals(existing.Name.Trim(), filmName, StringComparison.OrdinalIgnoreCase))
        {
            return (null, false, Conflict(existing.Id,
                $"A film first created as '{filmName}' is now named '{existing.Name}'; attach to it by id or choose another name"));
        }

        logger.LogInformation(
            "{method}: Film '{filmId}' '{name}' was created by another request; attaching to it.",
            nameof(ResolveCreateConflict), existing.Id, existing.Name);
        return (existing, false, null);
    }

    /// <summary>
    /// The id of a film created by name: a name-based GUID (RFC 4122 version 5 layout over SHA-256) of the trimmed,
    /// lower-case name, so every create of the same name targets the same document.
    /// </summary>
    internal static Guid NewFilmId(string filmName)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("cultpodcasts:film-by-name:" + filmName.Trim().ToLowerInvariant()));
        var id = bytes[..16];
        id[6] = (byte)((id[6] & 0x0F) | 0x50);
        id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id, bigEndian: true);
    }

    private static EpisodeTrailerAttachResult ToTrailerResult(EpisodeFilmMoveResult stop) => stop.Status switch
    {
        EpisodeFilmMoveStatus.IdentityDecisionRequired =>
            new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.IdentityDecisionRequired, IdentityHold: stop.IdentityHold),
        EpisodeFilmMoveStatus.Cancelled => new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Cancelled),
        EpisodeFilmMoveStatus.Conflict => Conflict(stop.FilmId ?? Guid.Empty, stop.Error ?? "A film with this name already exists"),
        EpisodeFilmMoveStatus.BadRequest => BadRequest(stop.Error ?? "Invalid film name"),
        _ => new EpisodeTrailerAttachResult(EpisodeTrailerAttachStatus.Failed, Error: stop.Error)
    };

    private async Task<Film?> FirstFilmHoldingTrailer(Guid trailerId)
    {
        foreach (var filmId in await filmRepository.GetFilmIdsByTrailerId(trailerId))
        {
            if (await filmRepository.GetFilm(filmId) is { } film)
            {
                return film;
            }
        }

        return null;
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

            var otherFilmId = await OtherFilmHoldingTrailer(filmId, trailer.Id);
            if (otherFilmId is Guid holdingFilmId)
            {
                logger.LogWarning(
                    "{method}: Episode '{episodeId}' is already a trailer of film '{otherFilmId}'; refusing to attach it to film '{filmId}'.",
                    nameof(AddTrailer), trailer.Id, holdingFilmId, filmId);
                return (null, Conflict(holdingFilmId, "This episode is already a trailer of another film"));
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

    /// <summary>
    /// A film other than <paramref name="filmId"/> that already lists <paramref name="trailerId"/>.
    /// The destination film is ignored: re-sending to the film that already holds the trailer still resumes.
    /// </summary>
    private async Task<Guid?> OtherFilmHoldingTrailer(Guid filmId, Guid trailerId)
    {
        var holders = await filmRepository.GetFilmIdsByTrailerId(trailerId);
        foreach (var holder in holders)
        {
            if (holder != filmId)
            {
                return holder;
            }
        }

        return null;
    }

    /// <summary>
    /// The film disappeared after its search document was pushed and the episode's was deleted. Putting the episode
    /// document back is best effort: a failure is logged and the attach still stops with the episode row kept.
    /// </summary>
    private async Task RestoreEpisodeSearchDocument(
        Podcast podcast,
        Episode episode,
        Guid filmId,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();
            if (await searchSwap.UploadAsync([document], cancellationToken))
            {
                return;
            }

            logger.LogError(
                "TrailerAttachFilmGone: Search re-upload failed for episode '{episodeId}' of deleted film '{filmId}'.",
                episode.Id, filmId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TrailerAttachFilmGone: Search re-upload failed for episode '{episodeId}' of deleted film '{filmId}'.",
                episode.Id, filmId);
        }
    }

    private static EpisodeTrailerAttachResult BadRequest(string error) =>
        new(EpisodeTrailerAttachStatus.BadRequest, Error: error);

    private static EpisodeTrailerAttachResult Conflict(Guid holdingFilmId, string error) =>
        new(EpisodeTrailerAttachStatus.Conflict, holdingFilmId, Error: error);

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
