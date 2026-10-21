using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeFilmMoveExtensions
{
    public static Models.EpisodeFilmMoveRequest ToModel(this EpisodeFilmMoveRequest? request) => new()
    {
        Title = request?.Title
    };

    public static EpisodeFilmMoveResponse ToDto(this EpisodeFilmMoveResult result) =>
        new()
        {
            FilmId = result.FilmId,
            Name = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing
        };
}
