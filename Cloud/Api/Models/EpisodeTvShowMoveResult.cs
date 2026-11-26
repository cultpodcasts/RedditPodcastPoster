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

/// <param name="EmptiedNewsOrganisationId">
/// Move of a news report only: the source news organisation when the move left it with no reports, so the curator can
/// retire it (PATCH <c>newsorganisation/{id}</c> with <c>removed: true</c>). Null otherwise.
/// </param>
public record EpisodeTvShowMoveResult(
    EpisodeTvShowMoveStatus Status,
    Guid? TvShowId = null,
    string? TvShowName = null,
    Guid? EpisodeId = null,
    bool CreatedShow = false,
    string? Error = null,
    bool FailureIndexing = false,
    Guid? EmptiedNewsOrganisationId = null);
