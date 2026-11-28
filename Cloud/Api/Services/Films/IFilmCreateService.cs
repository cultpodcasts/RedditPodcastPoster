using Api.Models;

namespace Api.Services.Films;

public interface IFilmCreateService
{
    Task<FilmCreateResult> CreateAsync(FilmCreateCommand command, CancellationToken cancellationToken);
}
