namespace Api.Models;

public enum EpisodeDeleteStatus
{
    Deleted,
    NotFound,
    AlreadySocial,
    Failed
}

public record EpisodeDeleteResult(
    EpisodeDeleteStatus Status,
    bool Posted = false,
    bool Tweeted = false);
