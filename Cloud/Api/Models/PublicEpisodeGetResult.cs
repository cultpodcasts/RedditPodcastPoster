using Api.Dtos;

namespace Api.Models;

public enum PublicEpisodeGetStatus
{
    Ok,
    NotFound,
    Failed
}

public record PublicEpisodeGetResult(
    PublicEpisodeGetStatus Status,
    PublicEpisode? Episode = null);
