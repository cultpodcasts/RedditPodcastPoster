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

            if (!PlayableChangeApplier.TryParseRelease(request.Change.Release, allowYear: true, out _, out var fieldError) ||
                !PlayableChangeApplier.TryParseDuration(request.Change.Duration, out _, out fieldError) ||
                !FilmLinkChangeApplier.TryValidate(film, request.Change, out fieldError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, fieldError);
            }

            var imdb = CanonicalUriPatch.Read(request.Change.Imdb);
            if (imdb.Kind == CanonicalUriPatchKind.Invalid)
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, imdb.Error);
            }

            // Same decision as PostStatusApplier, without writing the film this request first read.
            // The replace below applies the request onto the film that wins the ETag race.
            var postStatus = new PostStatusChange(
                request.Change.Tweeted == false && film.Tweeted,
                request.Change.UnBluesky == true && film.BlueskyPosted);

            // A keep-list is rejected before any write when the stored film already has a trailer this client did
            // not list. The save loop checks again, for a trailer attached in the gap after this read.
            if (request.Change.Trailers is not null)
            {
                var stale = await ReadStaleTrailers(film.Id, request.Change.ExpectedTrailerIds);
                if (stale.Status != FilmUpdateStatus.Accepted)
                {
                    return stale;
                }
            }

            var persisted = await SaveKeepingAttachedTrailers(film.Id, request.Change, readTrailerIds);
            if (persisted.Result.Status != FilmUpdateStatus.Accepted || persisted.Film is null)
            {
                return persisted.Result;
            }

            // Deletes run only after the replace. A 409 then leaves the live posts in place, and the stored flags
            // still match them. The flags drop on a later re-read, so a rename during the delete is not overwritten.
            var saved = persisted.Film;
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, saved, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, saved, target)
            };
            if (outcome.TweetDeleted == true || outcome.BlueskyPostDeleted == true)
            {
                var cleared = await filmRepository.Update(saved.Id, stored =>
                {
                    if (outcome.TweetDeleted == true)
                    {
                        stored.Tweeted = false;
                    }

                    if (outcome.BlueskyPostDeleted == true)
                    {
                        stored.ClearBlueskyPostState();
                    }

                    return true;
                });
                if (cleared is not null)
                {
                    saved = cleared;
                }
            }

            // As the episode API: removed=true always deletes the search document and the short link (the film
            // indexer never deletes), so a retry finishes the job; any other edit pushes the document now.
            outcome.SearchIndexer = await searchSync.AfterSave(
                saved,
                target.Projection,
                request.Change.Removed == true,
                cancellationToken,
                trailersCleared: hadTrailers && saved.Trailers is not { Count: > 0 });

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
    /// The film this replace wrote, or a result and no film when the edit was not saved.
    /// </summary>
    private readonly record struct FilmPersist(FilmUpdateResult Result, Film? Film);

    /// <summary>
    /// Re-reads the film and applies this request onto that instance, then replaces it with that instance's ETag.
    /// A trailer keep-list is a 409, before the apply, when that read has a trailer id the client did not list.
    /// Any other edit keeps the name, social flags and trailers already on that read. NotFound when the film was
    /// deleted after this request read it, so the edit does not re-create it.
    /// </summary>
    private async Task<FilmPersist> SaveKeepingAttachedTrailers(
        Guid filmId,
        FilmChangeRequest change,
        IReadOnlySet<Guid> readTrailerIds)
    {
        var guardingTrailers = change.Trailers is not null;
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var latest = await filmRepository.GetFilmForUpdate(filmId);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' was deleted during the edit; not saved.",
                    nameof(SaveKeepingAttachedTrailers), filmId);
                return new FilmPersist(new FilmUpdateResult(FilmUpdateStatus.NotFound), null);
            }

            if (guardingTrailers)
            {
                var unexpected = TrailerIdsTheClientDidNotList(latest.Film, change.ExpectedTrailerIds);
                if (unexpected.Length > 0)
                {
                    return new FilmPersist(StaleTrailers(filmId, unexpected), null);
                }
            }
            else
            {
                var attached = latest.Film.Trailers?.Count(trailer => !readTrailerIds.Contains(trailer.Id)) ?? 0;
                if (attached > 0)
                {
                    logger.LogWarning(
                        "{method}: Keeping {count} trailer(s) attached to film '{filmId}' during this edit.",
                        nameof(SaveKeepingAttachedTrailers), attached, filmId);
                }
            }

            if (!TryApply(latest.Film, change, out _, out var applyError) ||
                !CanonicalUriPatch.TryApply(change.Imdb, uri => latest.Film.Imdb = uri, out applyError))
            {
                return new FilmPersist(new FilmUpdateResult(FilmUpdateStatus.BadRequest, applyError), null);
            }

            if (await filmRepository.TryReplace(latest.Film, latest.ETag))
            {
                return new FilmPersist(new FilmUpdateResult(FilmUpdateStatus.Accepted), latest.Film);
            }
        }

        throw new InvalidOperationException($"Film '{filmId}' kept changing during the edit; not saved.");
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
