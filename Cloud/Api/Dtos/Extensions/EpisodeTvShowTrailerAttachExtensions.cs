using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeTvShowTrailerAttachExtensions
{
    public static Models.EpisodeTvShowTrailerAttachRequest ToModel(this EpisodeTvShowTrailerAttachRequest? request) => new()
    {
        TvShowId = request?.TvShowId,
        TvShowName = request?.TvShowName,
        CreateShow = request?.CreateShow == true,
        Publisher = request?.Publisher,
        Title = request?.Title
    };

    public static EpisodeTvShowTrailerAttachResponse ToDto(this EpisodeTvShowTrailerAttachResult result) =>
        new()
        {
            TvShowId = result.TvShowId,
            TvShowName = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing,
            ShowCreated = result.ShowCreated
        };
}

