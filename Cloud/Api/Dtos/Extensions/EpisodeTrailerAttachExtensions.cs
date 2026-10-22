using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeTrailerAttachExtensions
{
    public static Models.EpisodeTrailerAttachRequest ToModel(this EpisodeTrailerAttachRequest? request) => new()
    {
        FilmId = request?.FilmId ?? Guid.Empty,
        Title = request?.Title
    };

    public static EpisodeTrailerAttachResponse ToDto(this EpisodeTrailerAttachResult result) =>
        new()
        {
            FilmId = result.FilmId,
            Name = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing
        };
}
