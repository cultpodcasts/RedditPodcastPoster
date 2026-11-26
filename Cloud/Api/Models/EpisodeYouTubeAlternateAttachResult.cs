namespace Api.Models;

public enum EpisodeYouTubeAlternateAttachStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

public sealed record EpisodeYouTubeAlternateAttachResult(
    EpisodeYouTubeAlternateAttachStatus Status,
    Guid? TargetId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false);
