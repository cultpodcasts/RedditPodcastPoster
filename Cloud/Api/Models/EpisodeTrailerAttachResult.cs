namespace Api.Models;

public enum EpisodeTrailerAttachStatus
{
    Accepted,
    /// <summary>The podcast, episode, or film does not exist.</summary>
    NotFound,
    /// <summary>The episode or film is removed, the episode has no link, or the episode is the film.</summary>
    BadRequest,
    Failed
}

public record EpisodeTrailerAttachResult(
    EpisodeTrailerAttachStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false);
