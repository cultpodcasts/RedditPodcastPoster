namespace Api.Models;

public enum EpisodeAwaitingApprovalStatus
{
    Ok,
    Failed
}

public record EpisodeAwaitingApprovalResult(
    EpisodeAwaitingApprovalStatus Status,
    IReadOnlyList<EpisodePodcastPair>? Episodes = null);
