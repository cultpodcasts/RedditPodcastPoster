using Api.Models;

namespace Api.Services.Films;

public interface IFilmService
{
    Task<FilmGetResult> GetAsync(string identifier, CancellationToken cancellationToken);

    Task<FilmUpdateResult> UpdateAsync(
        FilmChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
