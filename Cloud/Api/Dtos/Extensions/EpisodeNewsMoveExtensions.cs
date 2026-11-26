using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeNewsMoveExtensions
{
    public static Models.EpisodeNewsMoveRequest ToModel(this EpisodeNewsMoveRequest? request) => new()
    {
        NewsOrganisationId = request?.NewsOrganisationId,
        NewsOrganisationName = request?.NewsOrganisationName,
        CreateOrganisation = request?.CreateOrganisation == true,
        Publisher = request?.Publisher
    };

    public static PodcastNewsMoveCommand ToCommand(this PodcastNewsMoveRequest? request, Guid podcastId) =>
        new(podcastId, request?.EpisodeIds ?? [], request.ToModel());

    public static EpisodeNewsMoveResponse ToDto(this EpisodeNewsMoveResult result) =>
        new()
        {
            NewsOrganisationId = result.NewsOrganisationId,
            NewsOrganisationName = result.NewsOrganisationName,
            CreatedOrganisation = result.CreatedOrganisation,
            EpisodeId = result.EpisodeId,
            MovedEpisodeIds = [.. result.MovedEpisodeIds ?? []],
            AlreadyMovedEpisodeIds = [.. result.AlreadyMovedEpisodeIds ?? []],
            NotFoundEpisodeIds = [.. result.NotFoundEpisodeIds ?? []],
            ConflictEpisodeIds = [.. result.ConflictEpisodeIds ?? []],
            Error = result.Error,
            FailureIndexing = result.FailureIndexing
        };
}
