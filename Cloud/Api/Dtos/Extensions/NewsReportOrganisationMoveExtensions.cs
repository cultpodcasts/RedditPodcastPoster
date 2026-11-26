using Api.Models;

namespace Api.Dtos.Extensions;

public static class NewsReportOrganisationMoveExtensions
{
    public static NewsReportOrganisationMoveCommand ToCommand(
        this NewsReportOrganisationMoveRequest? request,
        Guid sourceNewsOrganisationId) =>
        new(
            sourceNewsOrganisationId,
            request?.TargetNewsOrganisationId ?? Guid.Empty,
            request?.ReportIds,
            request?.MoveYouTubeChannels == true);

    public static NewsReportOrganisationMoveResponse ToDto(this NewsReportOrganisationMoveResult result) =>
        new()
        {
            SourceNewsOrganisationId = result.SourceNewsOrganisationId,
            TargetNewsOrganisationId = result.TargetNewsOrganisationId,
            TargetNewsOrganisationName = result.TargetNewsOrganisationName,
            MovedReportIds = [.. result.MovedReportIds ?? []],
            AlreadyMovedReportIds = [.. result.AlreadyMovedReportIds ?? []],
            NotFoundReportIds = [.. result.NotFoundReportIds ?? []],
            ConflictReportIds = [.. result.ConflictReportIds ?? []],
            RemainingReportCount = result.RemainingReportCount,
            EmptiedNewsOrganisationId = result.EmptiedNewsOrganisationId,
            MovedYouTubeChannelIds = [.. result.MovedYouTubeChannelIds ?? []],
            YouTubeChannelMoveFailed = result.YouTubeChannelMoveFailed,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing,
            Retryable = result.Retryable ? true : null
        };
}
