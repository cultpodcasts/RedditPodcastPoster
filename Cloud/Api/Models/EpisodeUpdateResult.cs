using Api.Dtos;

namespace Api.Models;

public enum EpisodeUpdateStatus
{
    Accepted,
    NotFound,
    Failed
}

public record EpisodeUpdateResult(
    EpisodeUpdateStatus Status,
    EpisodeUpdateResponse? Response = null);
