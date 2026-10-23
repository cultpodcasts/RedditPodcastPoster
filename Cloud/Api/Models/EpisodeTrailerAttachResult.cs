namespace Api.Models;

public enum EpisodeTrailerAttachStatus
{
    Accepted,
    /// <summary>The podcast, episode, or film does not exist.</summary>
    NotFound,
    /// <summary>The episode or film is removed, the episode has no link, the episode is the film, or another film already holds this trailer.</summary>
    BadRequest,
    Failed
}

public record EpisodeTrailerAttachResult(
    EpisodeTrailerAttachStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false);
