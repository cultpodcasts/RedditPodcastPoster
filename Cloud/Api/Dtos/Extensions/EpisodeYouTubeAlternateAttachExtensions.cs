using Api.Models;
using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Dtos.Extensions;

public static class EpisodeYouTubeAlternateAttachExtensions
{
    public static Models.EpisodeYouTubeAlternateAttachRequest? ToModel(
        this EpisodeYouTubeAlternateAttachRequest? request)
    {
        if (request is null || request.TargetId == Guid.Empty)
        {
            return null;
        }

        if (!Enum.TryParse<ContentKind>(request.TargetKind, ignoreCase: true, out var kind) ||
            kind is not (ContentKind.Film or ContentKind.NewsReport or ContentKind.TvShowEpisode or ContentKind.Episode))
        {
            return null;
        }

        return new Models.EpisodeYouTubeAlternateAttachRequest(kind, request.TargetId);
    }

    public static EpisodeYouTubeAlternateAttachResponse ToDto(this EpisodeYouTubeAlternateAttachResult result) =>
        new()
        {
            TargetId = result.TargetId,
            Name = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing
        };
}
