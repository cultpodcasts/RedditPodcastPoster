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

            if (!TryApply(film, request.Change, out var postStatus, out var fieldError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, fieldError);
            }

            if (!CanonicalUriPatch.TryApply(request.Change.Imdb, uri => film.Imdb = uri, out var imdbError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, imdbError);
            }

            // Both deletes run before the save, so the stored flags only drop once a delete has succeeded:
            // Bluesky needs the AT URI still stored; tweeted is cleared only when the tweet was deleted.
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, film, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, film, target)
            };
            await filmRepository.Save(film);

            // As the episode API: removed=true always deletes the search document and the short link (the film
            // indexer never deletes), so a retry finishes the job; any other edit pushes the document now.
            outcome.SearchIndexer = await searchSync.AfterSave(
                film, target.Projection, request.Change.Removed == true, cancellationToken);

            return new FilmUpdateResult(FilmUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update film '{id}'.", nameof(UpdateAsync), request.FilmId);
            return new FilmUpdateResult(FilmUpdateStatus.Failed);
        }
    }

    /// <summary>
    /// Publisher fields (shared with news organisations and TV shows), then the playable fields a film has.
    /// Release may be a year or a date. Validates first, so a 400 leaves the film untouched.
    /// </summary>
    internal static bool TryApply(
        Film film,
        FilmChangeRequest change,
        out PostStatusChange postStatus,
        out string? error)
    {
        postStatus = PostStatusChange.None;
        if (!PlayableChangeApplier.TryParseRelease(change.Release, allowYear: true, out var release, out error) ||
            !PlayableChangeApplier.TryParseDuration(change.Duration, out var length, out error))
        {
            return false;
        }

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
