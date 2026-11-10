namespace Api.Models;

public enum EpisodeNewsMoveStatus
{
    Accepted,
    /// <summary>The podcast or the news organisation does not exist, or an episode id is not on the podcast.</summary>
    NotFound,
    /// <summary>
    /// Several news organisations have the name, or an episode id is already a TV episode, a film, or a news report
    /// of another organisation.
    /// </summary>
    Conflict,
    /// <summary>The request is invalid or the news organisation is removed.</summary>
    BadRequest,
    /// <summary>A write or the search swap failed. Source episodes that were not moved are kept; re-send to resume.</summary>
    Failed
}

/// <summary>
/// Command outcome of a move to a news organisation (single or batch). Only <see cref="EpisodeNewsMoveStatus.Accepted"/>
/// deletes source episode rows; every other status leaves the podcast's episodes as they were (a
/// <see cref="EpisodeNewsMoveStatus.Failed"/> attempt may leave news reports or a created organisation, which a re-send
/// reuses).
/// </summary>
public record EpisodeNewsMoveResult(
    EpisodeNewsMoveStatus Status,
    Guid? NewsOrganisationId = null,
    string? NewsOrganisationName = null,
    bool CreatedOrganisation = false,
    IReadOnlyList<Guid>? MovedEpisodeIds = null,
    IReadOnlyList<Guid>? AlreadyMovedEpisodeIds = null,
    IReadOnlyList<Guid>? NotFoundEpisodeIds = null,
    IReadOnlyList<Guid>? ConflictEpisodeIds = null,
    string? Error = null,
    bool FailureIndexing = false)
{
    /// <summary>The single moved episode id, for POST episode/{podcastId}/{episodeId}/news.</summary>
    public Guid? EpisodeId { get; init; }
}
