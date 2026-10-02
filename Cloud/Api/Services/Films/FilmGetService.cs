using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Films;

public class FilmGetService(
    IFilmRepository filmRepository,
    ILogger<FilmGetService> logger) : IFilmGetService
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

            var matches = await filmRepository.GetAllBy(film => film.Name == identifier)
                .ToArrayAsync(cancellationToken);
            if (matches.Length == 0)
            {
                return new FilmGetResult(FilmGetStatus.NotFound);
            }

            if (matches.Length == 1)
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
}
