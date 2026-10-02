using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Films;

public class FilmUpdateService(
    IFilmRepository filmRepository,
    ILogger<FilmUpdateService> logger) : IFilmUpdateService
{
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

            if (!CanonicalUriPatch.TryApply(request.Change.Imdb, uri => film.Imdb = uri, out var imdbError))
            {
                return new FilmUpdateResult(FilmUpdateStatus.BadRequest, imdbError);
            }

            await filmRepository.Save(film);
            return new FilmUpdateResult(FilmUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update film '{id}'.", nameof(UpdateAsync), request.FilmId);
            return new FilmUpdateResult(FilmUpdateStatus.Failed);
        }
    }
}
