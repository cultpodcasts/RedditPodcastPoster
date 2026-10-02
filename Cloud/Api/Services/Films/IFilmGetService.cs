using Api.Models;

namespace Api.Services.Films;

public interface IFilmGetService
{
    Task<FilmGetResult> GetAsync(string identifier, CancellationToken cancellationToken);
}
