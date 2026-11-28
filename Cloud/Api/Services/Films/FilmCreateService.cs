using System.Text.RegularExpressions;
using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Films;

/// <summary>
/// Creates an empty film from a TMDB / IMDb identity (RPP #1413), without moving an episode, so a trailer for a film the
/// catalogue does not have yet can attach with <c>POST episode/{p}/{e}/trailer {filmId}</c>. The film has no services,
/// video, trailers or episode: it indexes and renders without a main video.
/// <list type="number">
/// <item>With <c>tmdbId</c> / <c>imdbId</c>: refuse when a film already has either id (409 naming that film), read the
/// TMDB movie (<see cref="IIdentityMetadataEnricher"/>; none is 404), then name, description, release, runtime, image,
/// publisher and alternative titles come from it (<see cref="IdentityMetadataFill"/>, the same fill as Move to film
/// and the film PATCH, so the release is the identity's date, or its year when only the year is known; RPP #1410).</item>
/// <item>With a <c>title</c> and no ids: the same identity gate as Move to film (<see cref="FilmMoveTarget"/>): a
/// confident match supplies ids; an ambiguous one returns the candidates and writes nothing; <c>select</c> /
/// <c>continue</c> / <c>cancel</c> are honoured on a re-POST.</item>
/// <item>The name is the request title, else the identity title (no '/'). A film with the same name (case-insensitive)
/// is a 409 naming it.</item>
/// <item>One conditional create under an id derived from the name (<see cref="EpisodeTrailerAttachService.NewFilmId"/>),
/// so overlapping creates converge on one document; the loser is a 409 naming the winner.</item>
/// <item>Push the film's search document. On failure the film is kept and the result reports
/// <c>FailureIndexing</c>; a film save, the Indexer's scheduled film push or FilmSearchPusher puts it in search.</item>
/// </list>
/// Re-sending the same identity is safe: it is a 409 carrying the existing film's id, and writes nothing.
/// </summary>
public partial class FilmCreateService(
    IFilmRepository filmRepository,
    ICatalogueIdentityEnricher identityEnricher,
    IIdentityMetadataEnricher identityMetadata,
    IFileKeyAllocator fileKeyAllocator,
    IPlayableSearchDocumentSwap searchSwap,
    ILogger<FilmCreateService> logger) : IFilmCreateService
{
    [GeneratedRegex(@"^tt\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdPattern();

    public async Task<FilmCreateResult> CreateAsync(FilmCreateCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var request = command.Request;
            var imdbId = string.IsNullOrWhiteSpace(request.ImdbId) ? null : request.ImdbId.Trim().ToLowerInvariant();
            var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
            if (request.TmdbId is <= 0)
            {
                return BadRequest("tmdbId must be a positive number");
            }

            if (imdbId is not null && !ImdbIdPattern().IsMatch(imdbId))
            {
                return BadRequest("imdbId must be 'tt' followed by digits");
            }

            var hasIdentity = request.TmdbId is not null || imdbId is not null;
            if (!hasIdentity && title is null)
            {
                return BadRequest("Give tmdbId or imdbId, or a title to look up");
            }

            var publisher = string.IsNullOrWhiteSpace(request.Publisher) ? null : request.Publisher.Trim();
            return hasIdentity
                ? await CreateFromIdentityIdsAsync(request.TmdbId, imdbId, title, publisher, cancellationToken)
                : await CreateFromTitleAsync(title!, publisher, request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{method}: Failed to create a film.", nameof(CreateAsync));
            return new FilmCreateResult(FilmCreateStatus.Failed);
        }
    }

    private async Task<FilmCreateResult> CreateFromIdentityIdsAsync(
        int? tmdbId,
        string? imdbId,
        string? title,
        string? publisher,
        CancellationToken cancellationToken)
    {
        // Cheap clash check first: a known identity never spends a TMDB call.
        if (await FindIdentityClash(tmdbId, imdbId, cancellationToken) is { } early)
        {
            return IdentityClash(early);
        }

        var metadata = await identityMetadata.FetchFilmAsync(tmdbId, imdbId, cancellationToken);
        if (metadata is null)
        {
            return new FilmCreateResult(
                FilmCreateStatus.IdentityNotFound,
                Error: "No film was found for that TMDB / IMDb id");
        }

        // An IMDb-only request learns its TMDB id from the lookup; check that one too.
        if (tmdbId is null && await FindIdentityClash(metadata.Id, null, cancellationToken) is { } late)
        {
            return IdentityClash(late);
        }

        var name = title ?? metadata.Title?.Trim() ?? string.Empty;
        var nameStop = ValidateName(name);
        if (nameStop is not null)
        {
            return nameStop;
        }

        if (await NameClash(name, cancellationToken) is { } nameClash)
        {
            return nameClash;
        }

        var film = new Film(name) { Description = string.Empty };
        film.TmdbId = tmdbId ?? (metadata.Id > 0 ? metadata.Id : null);
        film.ImdbId = imdbId;
        if (publisher is not null)
        {
            film.PublisherName = publisher;
        }

        IdentityMetadataFill.Fill(film, metadata);
        return await PersistAsync(film, cancellationToken);
    }

    private async Task<FilmCreateResult> CreateFromTitleAsync(
        string title,
        string? publisher,
        FilmCreateRequest request,
        CancellationToken cancellationToken)
    {
        var (stop, name, admission) = await FilmMoveTarget.PrepareNewFilm(
            null,
            null,
            new EpisodeFilmMoveRequest
            {
                Title = title,
                IdentityDecision = request.IdentityDecision,
                SelectedIdentityKey = request.SelectedIdentityKey
            },
            filmRepository,
            identityEnricher,
            logger,
            cancellationToken);
        if (stop is not null)
        {
            return FromMoveStop(stop);
        }

        var film = new Film(name) { Description = string.Empty };
        if (publisher is not null)
        {
            film.PublisherName = publisher;
        }

        FilmMoveTarget.ApplyIdentity(film, admission, identityEnricher, logger);
        if (await FindIdentityClash(film.TmdbId, film.ImdbId, cancellationToken) is { } clash)
        {
            return IdentityClash(clash);
        }

        await FilmMoveTarget.ApplyIdentityMetadataAsync(film, identityMetadata, cancellationToken);
        return await PersistAsync(film, cancellationToken);
    }

    private async Task<FilmCreateResult> PersistAsync(Film film, CancellationToken cancellationToken)
    {
        // Same deterministic id as a film created by Attach as trailer, so overlapping creates converge.
        film.Id = EpisodeTrailerAttachService.NewFilmId(film.Name);
        film.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.Film, film.Name, film.Id);

        if (!await filmRepository.TryCreate(film))
        {
            var existing = await filmRepository.GetFilm(film.Id);
            logger.LogWarning(
                "{method}: Film '{filmId}' '{name}' was created by another request.",
                nameof(PersistAsync), film.Id, film.Name);
            return new FilmCreateResult(
                FilmCreateStatus.Conflict,
                film.Id,
                existing?.Name ?? film.Name,
                "A film with this name already exists");
        }

        logger.LogInformation(
            "{method}: Created film '{filmId}' '{name}' (imdb={imdbId}, tmdb={tmdbId}) from identity.",
            nameof(PersistAsync), film.Id, film.Name, film.ImdbId, film.TmdbId);

        if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(film)], cancellationToken))
        {
            logger.LogError(
                "{method}: Search push failed for new film '{filmId}'. The film is kept; a film save or the film push indexes it.",
                nameof(PersistAsync), film.Id);
            return new FilmCreateResult(FilmCreateStatus.Failed, film.Id, film.Name, FailureIndexing: true);
        }

        return new FilmCreateResult(FilmCreateStatus.Accepted, film.Id, film.Name);
    }

    private async Task<Film?> FindIdentityClash(int? tmdbId, string? imdbId, CancellationToken cancellationToken)
    {
        if (tmdbId is { } tmdb)
        {
            await foreach (var film in filmRepository.GetAllBy(x => x.TmdbId == tmdb)
                               .WithCancellation(cancellationToken))
            {
                return film;
            }
        }

        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            var imdb = imdbId.Trim().ToLowerInvariant();
            await foreach (var film in filmRepository.GetAllBy(x => x.ImdbId == imdb)
                               .WithCancellation(cancellationToken))
            {
                return film;
            }
        }

        return null;
    }

    private async Task<FilmCreateResult?> NameClash(string name, CancellationToken cancellationToken)
    {
        var clashes = await PublisherNameAttachLookup.FindByName(filmRepository, name, cancellationToken);
        if (clashes.Count == 0)
        {
            return null;
        }

        logger.LogWarning("{method}: A film named '{name}' already exists.", nameof(NameClash), name);
        return new FilmCreateResult(
            FilmCreateStatus.Conflict,
            clashes[0].Id,
            clashes[0].Name,
            "A film with this name already exists");
    }

    private static FilmCreateResult? ValidateName(string name)
    {
        if (name.Length == 0)
        {
            return BadRequest("Film name is required");
        }

        return name.Contains('/')
            ? BadRequest("Film name cannot contain '/'; send a title")
            : null;
    }

    private static FilmCreateResult IdentityClash(Film existing) =>
        new(FilmCreateStatus.Conflict, existing.Id, existing.Name, "A film with this TMDB / IMDb id already exists");

    private static FilmCreateResult FromMoveStop(EpisodeFilmMoveResult stop) => stop.Status switch
    {
        EpisodeFilmMoveStatus.IdentityDecisionRequired =>
            new FilmCreateResult(FilmCreateStatus.IdentityDecisionRequired, IdentityHold: stop.IdentityHold),
        EpisodeFilmMoveStatus.Cancelled => new FilmCreateResult(FilmCreateStatus.Cancelled),
        EpisodeFilmMoveStatus.Conflict =>
            new FilmCreateResult(FilmCreateStatus.Conflict, stop.FilmId, stop.Name, stop.Error),
        EpisodeFilmMoveStatus.BadRequest => BadRequest(stop.Error ?? "Invalid film name"),
        _ => new FilmCreateResult(FilmCreateStatus.Failed, Error: stop.Error)
    };

    private static FilmCreateResult BadRequest(string error) => new(FilmCreateStatus.BadRequest, Error: error);
}
