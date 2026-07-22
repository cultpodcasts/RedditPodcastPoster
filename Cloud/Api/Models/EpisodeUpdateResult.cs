namespace Api.Models;

public enum EpisodeUpdateStatus
{
    Accepted,
    NotFound,
    Failed
}

public record EpisodeUpdateResult(
    EpisodeUpdateStatus Status,
    EpisodeUpdateOutcome? Outcome = null);
