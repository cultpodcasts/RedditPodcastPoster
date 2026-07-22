namespace Api.Models;

public enum EpisodeOutgoingStatus
{
    Ok,
    Failed
}

public record EpisodeOutgoingResult(
    EpisodeOutgoingStatus Status,
    IReadOnlyList<EpisodePodcastPair>? Episodes = null);
