using Api.Models;

namespace Api.Services.Films;

public interface IFilmUpdateService
{
    Task<FilmUpdateResult> UpdateAsync(
        FilmChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
