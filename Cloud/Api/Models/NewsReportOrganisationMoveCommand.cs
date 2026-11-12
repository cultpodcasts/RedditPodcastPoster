namespace Api.Models;

/// <summary>
/// POST newsorganisation/{sourceId}/reports/move (#1397): move news reports from one news organisation to another,
/// keeping their ids.
/// </summary>
/// <param name="SourceNewsOrganisationId">The organisation the reports are in now.</param>
/// <param name="TargetNewsOrganisationId">The organisation they move to.</param>
/// <param name="ReportIds">
/// The report ids to move. Null or empty moves the source's next
/// <see cref="NewsReportOrganisationMoveLimits.MaxReportsPerRequest"/> reports, so re-sending empties the source.
/// </param>
/// <param name="MoveYouTubeChannels">
/// Also move the source's YouTube channels (main and additional) into the target's <c>additionalYouTubeChannels</c>,
/// so new uploads on them land on the target.
/// </param>
public record NewsReportOrganisationMoveCommand(
    Guid SourceNewsOrganisationId,
    Guid TargetNewsOrganisationId,
    IReadOnlyList<Guid>? ReportIds,
    bool MoveYouTubeChannels);

public static class NewsReportOrganisationMoveLimits
{
    /// <summary>Most reports one request moves (one HTTP request, well inside the host timeout).</summary>
    public const int MaxReportsPerRequest = 500;
}

public enum NewsReportOrganisationMoveStatus
{
    Accepted,

    /// <summary>The source or target organisation does not exist, or a requested id is not a report of the source.</summary>
    NotFound,

    /// <summary>A requested id is a report of a third organisation, a TV episode or a film.</summary>
    Conflict,

    /// <summary>Invalid request: no target, target is the source, a removed target, or a bad id list.</summary>
    BadRequest,

    /// <summary>A write or the search swap failed. Source rows that were not moved are kept; re-send to resume.</summary>
    Failed
}

/// <summary>
/// Command outcome of a report move between news organisations. Only <see cref="NewsReportOrganisationMoveStatus.Accepted"/>
/// deletes source rows; every other status leaves the source's reports where they were (a
/// <see cref="NewsReportOrganisationMoveStatus.Failed"/> attempt may leave copies in the target, which a re-send reuses).
/// </summary>
public record NewsReportOrganisationMoveResult(
    NewsReportOrganisationMoveStatus Status,
    Guid? SourceNewsOrganisationId = null,
    Guid? TargetNewsOrganisationId = null,
    string? TargetNewsOrganisationName = null,
    IReadOnlyList<Guid>? MovedReportIds = null,
    IReadOnlyList<Guid>? AlreadyMovedReportIds = null,
    IReadOnlyList<Guid>? NotFoundReportIds = null,
    IReadOnlyList<Guid>? ConflictReportIds = null,
    string? Error = null,
    bool FailureIndexing = false)
{
    /// <summary>Reports (removed ones included) still in the source after this request; null when not counted.</summary>
    public int? RemainingReportCount { get; init; }

    /// <summary>The source id when this request left it with no reports and it is not removed, so it can be retired.</summary>
    public Guid? EmptiedNewsOrganisationId { get; init; }

    /// <summary>The YouTube channel ids this request moved from the source to the target's additional channels.</summary>
    public IReadOnlyList<string>? MovedYouTubeChannelIds { get; init; }

    /// <summary>True when the channel move was asked for and failed (the report move itself succeeded).</summary>
    public bool YouTubeChannelMoveFailed { get; init; }
}
