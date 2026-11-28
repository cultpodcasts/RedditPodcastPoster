namespace Api.Models;

public enum EpisodeTvShowTrailerAttachStatus
{
    Accepted,
    /// <summary>The podcast, episode, or TV show does not exist.</summary>
    NotFound,
    /// <summary>The request is invalid, the episode or TV show is removed, or the episode has no link.</summary>
    BadRequest,
    /// <summary>
    /// Another TV show already holds this episode as a trailer, several TV shows have the name, the episode id is
    /// already a TV episode or a film, or another attach of this episode is in progress (<c>TvShowId</c> names the other show).
    /// </summary>
    Conflict,
    Failed
}

public record EpisodeTvShowTrailerAttachResult(
    EpisodeTvShowTrailerAttachStatus Status,
    Guid? TvShowId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false,
    bool ShowCreated = false);

