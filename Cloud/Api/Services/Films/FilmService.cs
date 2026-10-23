using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Films;

public class FilmService(
    IFilmRepository filmRepository,
    CataloguePlayableSearchSync searchSync,
    IPlayableSocialRetraction socialRetraction,
    ILogger<FilmService> logger) : IFilmService
{
    public async Task<FilmGetResult> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            if (Guid.TryParse(identifier, out var id))
            {
                var byId = await filmRepository.GetFilm(id);
                return byId is null
                    ? new FilmGetResult(FilmGetStatus.NotFound)
                    : new FilmGetResult(FilmGetStatus.Found, byId);
            }

            var name = PodcastRouteNameNormalizer.Normalize(identifier);
            var matches = await PublisherNameAttachLookup.FindByName(filmRepository, name, cancellationToken);
            if (matches.Count == 0)
            {
                return new FilmGetResult(FilmGetStatus.NotFound);
            }

            if (matches.Count == 1)
            {
                return new FilmGetResult(FilmGetStatus.Found, matches[0]);
            }

            return new FilmGetResult(
                FilmGetStatus.Conflict,
                AmbiguousIds: matches.Select(film => film.Id).ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get film '{identifier}'.", nameof(GetAsync), identifier);
            return new FilmGetResult(FilmGetStatus.Failed);
        }
    }

    public async Task<FilmUpdateResult> UpdateAsync(
        FilmChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var film = await filmRepository.GetFilm(request.FilmId);
            if (film is null)
            {
                return new FilmUpdateResult(FilmUpdateStatus.NotFound);
            }

            // Projected before the PATCH, so the match uses the release, length and AT URI the post was made with.
            var target = SocialPostRemovalTarget.For(film);
            var hadTrailers = film.Trailers is { Count: > 0 };
            var readTrailerIds = film.Trailers?.Select(trailer => trailer.Id).ToHashSet() ?? [];

            if (!TryApply(film, request.Change, out var postStatus, out var fieldError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, fieldError);
            }

            if (!CanonicalUriPatch.TryApply(request.Change.Imdb, uri => film.Imdb = uri, out var imdbError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, imdbError);
            }

            // A keep-list is rejected before any social delete when the stored film already has a trailer this
            // client did not list. The save loop checks again, for a trailer attached in the gap after this read.
            if (request.Change.Trailers is not null)
            {
                var stale = await ReadStaleTrailers(film.Id, request.Change.ExpectedTrailerIds);
                if (stale.Status != FilmUpdateStatus.Accepted)
                {
                    return stale;
                }
            }

            // Both deletes run before the save, so the stored flags only drop once a delete has succeeded:
            // Bluesky needs the AT URI still stored; tweeted is cleared only when the tweet was deleted.
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, film, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, film, target)
            };
            var persisted = await SaveKeepingAttachedTrailers(film, request.Change, readTrailerIds);
            if (persisted.Status != FilmUpdateStatus.Accepted)
            {
                return persisted;
            }

            // As the episode API: removed=true always deletes the search document and the short link (the film
            // indexer never deletes), so a retry finishes the job; any other edit pushes the document now.
            outcome.SearchIndexer = await searchSync.AfterSave(
                film,
                target.Projection,
                request.Change.Removed == true,
                cancellationToken,
                trailersCleared: hadTrailers && film.Trailers is not { Count: > 0 });

            return new FilmUpdateResult(FilmUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update film '{id}'.", nameof(UpdateAsync), request.FilmId);
            return new FilmUpdateResult(FilmUpdateStatus.Failed);
        }
    }

    private const int MaxSaveAttempts = FilmDocumentUpdate.MaxAttempts;

    private const string StaleTrailersMessage =
        "The film's trailers changed after this edit was loaded. Close and reopen the film to edit its trailers.";

    /// <summary>
    /// Null <paramref name="expectedTrailerIds"/> and an empty array both mean the client listed no trailer ids.
    /// </summary>
    private async Task<FilmUpdateResult> ReadStaleTrailers(Guid filmId, Guid[]? expectedTrailerIds)
    {
        var latest = await filmRepository.GetFilmForUpdate(filmId);
        if (latest is null)
        {
            logger.LogWarning(
                "{method}: Film '{filmId}' was deleted during the edit; not saved.",
                nameof(ReadStaleTrailers), filmId);
            return new FilmUpdateResult(FilmUpdateStatus.NotFound);
        }

        var unexpected = TrailerIdsTheClientDidNotList(latest.Film, expectedTrailerIds);
        return unexpected.Length == 0
            ? new FilmUpdateResult(FilmUpdateStatus.Accepted)
            : StaleTrailers(filmId, unexpected);
    }

    private FilmUpdateResult StaleTrailers(Guid filmId, Guid[] unexpected)
    {
        logger.LogWarning(
            "{method}: Film '{filmId}' has {count} trailer(s) this edit did not list; not saved.",
            nameof(StaleTrailers), filmId, unexpected.Length);
        return new FilmUpdateResult(
            FilmUpdateStatus.Conflict,
            StaleTrailersMessage,
            UnexpectedTrailerIds: unexpected);
    }

    private static Guid[] TrailerIdsTheClientDidNotList(Film film, Guid[]? expectedTrailerIds)
    {
        var expected = expectedTrailerIds is null ? [] : expectedTrailerIds.ToHashSet();
        return film.Trailers?
                   .Where(trailer => !expected.Contains(trailer.Id))
                   .Select(trailer => trailer.Id)
                   .ToArray()
               ?? [];
    }

    /// <summary>
    /// Saves the edited film. A trailer keep-list is a 409 when the latest film has a trailer id the client did not
    /// list: that trailer is not removed, and the edit is not saved. Any other edit still carries over a trailer
    /// attached after this request read the film, because that trailer's episode row is already deleted. The replace
    /// is ETag-guarded. NotFound when the film was deleted after this request read it, so the edit does not re-create it.
    /// </summary>
    private async Task<FilmUpdateResult> SaveKeepingAttachedTrailers(
        Film film,
        FilmChangeRequest change,
        IReadOnlySet<Guid> readTrailerIds)
    {
        var guardingTrailers = change.Trailers is not null;
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var latest = await filmRepository.GetFilmForUpdate(film.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' was deleted during the edit; not saved.",
                    nameof(SaveKeepingAttachedTrailers), film.Id);
                return new FilmUpdateResult(FilmUpdateStatus.NotFound);
            }

            if (guardingTrailers)
            {
                var unexpected = TrailerIdsTheClientDidNotList(latest.Film, change.ExpectedTrailerIds);
                if (unexpected.Length > 0)
                {
                    return StaleTrailers(film.Id, unexpected);
                }
            }
            else
            {
                var attached = latest.Film.Trailers?
                    .Where(trailer => !readTrailerIds.Contains(trailer.Id) && !film.HasTrailer(trailer.Id))
                    .ToList();
                if (attached is { Count: > 0 })
                {
                    logger.LogWarning(
                        "{method}: Keeping {count} trailer(s) attached to film '{filmId}' during this edit.",
                        nameof(SaveKeepingAttachedTrailers), attached.Count, film.Id);
                    film.Trailers ??= [];
                    film.Trailers.AddRange(attached);
                }
            }

            if (await filmRepository.TryReplace(film, latest.ETag))
            {
                return new FilmUpdateResult(FilmUpdateStatus.Accepted);
            }
        }

        throw new InvalidOperationException($"Film '{film.Id}' kept changing during the edit; not saved.");
    }

    /// <summary>
    /// Publisher fields (shared with news organisations and TV shows), then the playable fields a film has.
    /// Release may be a year or a date. Ids, links and trailers are <see cref="FilmLinkChangeApplier"/>.
    /// Validates first, so a 400 leaves the film untouched.
    /// </summary>
    internal static bool TryApply(
        Film film,
        FilmChangeRequest change,
        out PostStatusChange postStatus,
        out string? error)
    {
        postStatus = PostStatusChange.None;
        if (!PlayableChangeApplier.TryParseRelease(change.Release, allowYear: true, out var release, out error) ||
            !PlayableChangeApplier.TryParseDuration(change.Duration, out var length, out error) ||
            !FilmLinkChangeApplier.TryValidate(film, change, out error))
        {
            return false;
        }

        // IMDb / TMDB ids, links and trailers.
        FilmLinkChangeApplier.Apply(film, change);

        // Publisher fields include publisher, description and removed.
        PublisherChangeApplier.Apply(film, change);
        postStatus = PostStatusApplier.Apply(film, change.Posted, change.Tweeted, change.UnBluesky);

        if (release is not null)
        {
            film.SetRelease(release);
        }

        if (length is not null)
        {
            film.Length = length.Value;
        }

        if (change.Explicit != null)
        {
            film.Explicit = change.Explicit.Value;
        }

        if (change.Ignored != null)
        {
            film.Ignored = change.Ignored.Value;
        }

        if (change.Subjects != null)
        {
            film.ApplyUserSubjects(change.Subjects);
        }

        if (change.Guests != null)
        {
            film.Guests = PlayableChangeApplier.NormaliseGuests(change.Guests);
        }

        return true;
    }
}
