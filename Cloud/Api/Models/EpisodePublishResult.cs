using Api.Dtos;

namespace Api.Models;

public enum EpisodePublishStatus
{
    Ok,
    BadRequest,
    Failed
}

public record EpisodePublishResult(
    EpisodePublishStatus Status,
    EpisodePublishResponse? Response = null);
