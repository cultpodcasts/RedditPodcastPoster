using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeTvShowMoveExtensions
{
    public static Models.EpisodeTvShowMoveRequest ToModel(this EpisodeTvShowMoveRequest? request) => new()
    {
        TvShowId = request?.TvShowId,
        TvShowName = request?.TvShowName,
        CreateShow = request?.CreateShow == true,
        Publisher = request?.Publisher,
        Title = request?.Title
    };

    public static EpisodeTvShowMoveResponse ToDto(this EpisodeTvShowMoveResult result) =>
        new()
        {
            TvShowId = result.TvShowId,
            TvShowName = result.TvShowName,
            EpisodeId = result.EpisodeId,
            CreatedShow = result.CreatedShow,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing
        };
}
