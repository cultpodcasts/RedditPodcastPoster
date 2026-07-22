using Api.Dtos;

namespace Api.Models;

public enum EpisodeOutgoingStatus
{
    Ok,
    Failed
}

public record EpisodeOutgoingResult(
    EpisodeOutgoingStatus Status,
    IReadOnlyList<DiscreteEpisode>? Episodes = null);
