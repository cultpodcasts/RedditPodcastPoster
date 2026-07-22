using Api.Dtos;

namespace Api.Models;

public enum PodcastRenameStatus
{
    Ok,
    Conflict,
    NotFound,
    BadRequest,
    InvalidName,
    TooMany,
    Failed
}

public record PodcastRenameResult(
    PodcastRenameStatus Status,
    PodcastRenameResponse? Response = null);
