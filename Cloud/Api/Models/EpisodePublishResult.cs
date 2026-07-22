namespace Api.Models;

public enum EpisodePublishStatus
{
    Ok,
    BadRequest,
    Failed
}

public record EpisodePublishResult(
    EpisodePublishStatus Status,
    EpisodePublishOutcome? Outcome = null);
