using Api.Models;

namespace Api.Dtos.Extensions;

public static class FilmCreateExtensions
{
    public static Models.FilmCreateRequest ToModel(this FilmCreateRequest? request) => new()
    {
        TmdbId = request?.TmdbId,
        ImdbId = request?.ImdbId,
        Title = request?.Title,
        Publisher = request?.Publisher,
        IdentityDecision = request?.IdentityDecision,
        SelectedIdentityKey = request?.SelectedIdentityKey
    };

    public static FilmCreateResponse ToDto(this FilmCreateResult result) =>
        new()
        {
            FilmId = result.FilmId,
            Name = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing,
            IdentityDecision = result.IdentityHold is { } hold
                ? EpisodeFilmMoveExtensions.ToIdentityDecision(hold)
                : null,
            Cancelled = result.Status == FilmCreateStatus.Cancelled
        };
}
