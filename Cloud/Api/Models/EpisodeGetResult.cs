using Api.Dtos;

namespace Api.Models;

public enum EpisodeGetStatus
{
    Ok,
    EpisodeNotFound,
    PodcastNotFound,
    Failed
}

public record EpisodeGetResult(
    EpisodeGetStatus Status,
    DiscreteEpisode? Episode = null);
