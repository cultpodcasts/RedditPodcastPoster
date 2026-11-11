using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// The new-film side of a move to a film, shared by Move to film for a podcast episode
/// (<see cref="EpisodeFilmMoveService"/>) and for a news report (<see cref="News.NewsReportFilmMoveService"/>): the
/// name (the request title, else the source title; required, no '/'), the curator's identity decision, the name clash
/// check and the IMDb/TMDB identity gate, all before anything is written.
/// </summary>
public static class FilmMoveTarget
{
    /// <summary>
    /// Checks a new film for <paramref name="sourceTitle"/> released <paramref name="release"/>. A non-null
    /// <c>Stop</c> is the command outcome and nothing may be written (bad name, cancelled, name clash, or an
    /// ambiguous identity waiting for the curator). Otherwise <c>Name</c> is the film name and <c>Admission</c> the
    /// identity to apply (null when the lookup failed: the move continues with blank identity).
    /// </summary>
    public static async Task<(EpisodeFilmMoveResult? Stop, string Name, CatalogueIdentityAdmission? Admission)> PrepareNewFilm(
        string? sourceTitle,
        CatalogueRelease? release,
        EpisodeFilmMoveRequest request,
        IFilmRepository filmRepository,
        ICatalogueIdentityEnricher identityEnricher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(request.Title)
            ? sourceTitle?.Trim() ?? string.Empty
            : request.Title.Trim();
        if (name.Length == 0)
        {
            return (new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.BadRequest, Error: "Film name is required"), name,
                null);
        }

        if (name.Contains('/'))
        {
            return (new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.BadRequest, Error: "Film name cannot contain '/'"),
                name, null);
        }

        var choice = CatalogueIdentityChoices.Parse(request.IdentityDecision);
        if (choice == CatalogueIdentityChoice.Cancel)
        {
            return (new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Cancelled), name, null);
        }

        var clashes = await PublisherNameAttachLookup.FindByName(filmRepository, name, cancellationToken);
        if (clashes.Count > 0)
        {
            logger.LogError("{method}: A film named '{name}' already exists.", nameof(PrepareNewFilm), name);
            return (new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.Conflict,
                clashes[0].Id,
                clashes[0].Name,
                "A film with this name already exists"), name, null);
        }

        // Identity gate first (name + release year only) so IdentityDecisionRequired never spends
        // Spotify / Apple / YouTube quota; the curator's select / continue re-POST materialises once.
        var year = release is { } value ? value.Year : (int?)null;
        var (identityStop, admission) = await TryResolveNewFilmIdentity(
            name, year, choice, request.SelectedIdentityKey, identityEnricher, logger, cancellationToken);
        return (identityStop, name, admission);
    }

    /// <summary>Fills missing IMDb/TMDB ids on a new film from a confident admission.</summary>
    public static void ApplyIdentity(
        Film film,
        CatalogueIdentityAdmission? admission,
        ICatalogueIdentityEnricher identityEnricher,
        ILogger logger)
    {
        if (admission is not null && identityEnricher.ApplyMissing(film, admission.Ids))
        {
            logger.LogInformation(
                "{method}: Filled identity for new film '{name}' (imdb={imdbId}, tmdb={tmdbId}).",
                nameof(ApplyIdentity),
                film.Name,
                film.ImdbId,
                film.TmdbId);
        }
    }

    /// <summary>
    /// Same identity gate as URL-submit film create: confident → proceed with ids; ambiguous → return candidates and
    /// write nothing; <c>select</c>/<c>continue</c> honour the curator's answer; lookup failure → leave blank and still
    /// move. Does not need Services — only name and release year — so Materialise runs only after this gate succeeds.
    /// </summary>
    private static async Task<(EpisodeFilmMoveResult? Stop, CatalogueIdentityAdmission? Admission)> TryResolveNewFilmIdentity(
        string name,
        int? year,
        CatalogueIdentityChoice choice,
        string? selectedIdentityKey,
        ICatalogueIdentityEnricher identityEnricher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        CatalogueIdentityAdmission admission;
        try
        {
            admission = await identityEnricher.ResolveFilmAsync(
                name,
                year,
                choice,
                selectedIdentityKey,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "{method}: Identity lookup failed for new film '{name}'; leaving blank.",
                nameof(TryResolveNewFilmIdentity),
                name);
            // Null admission: move continues with blank identity and ApplyMissing is not called.
            return (null, null);
        }

        if (admission.Hold && admission.Prompt is { Candidates.Count: > 0 } prompt)
        {
            logger.LogInformation(
                "{method}: Film '{name}' identity is ambiguous ({count} candidates); waiting for curator choice.",
                nameof(TryResolveNewFilmIdentity),
                name,
                prompt.Candidates.Count);
            return (
                new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.IdentityDecisionRequired,
                    IdentityHold: prompt),
                null);
        }

        if (admission.Hold)
        {
            // Empty hold (e.g. cancel echoed from enricher) — treat as cancelled, nothing written.
            return (new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Cancelled), null);
        }

        return (null, admission);
    }
}
