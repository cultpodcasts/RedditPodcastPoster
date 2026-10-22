namespace Api.Models;

public enum EpisodeTvShowMoveStatus
{
    Accepted,
    /// <summary>The podcast, the episode, or the TV show does not exist.</summary>
    NotFound,
    /// <summary>
    /// Several TV shows have the name, or the episode id is already a TV episode of another show or a film.
    /// </summary>
    Conflict,
    /// <summary>The request is invalid, the episode is removed, or the TV show is removed.</summary>
    BadRequest,
    Failed
}

public record EpisodeTvShowMoveResult(
    EpisodeTvShowMoveStatus Status,
    Guid? TvShowId = null,
    string? TvShowName = null,
    Guid? EpisodeId = null,
    bool CreatedShow = false,
    string? Error = null,
    bool FailureIndexing = false);
