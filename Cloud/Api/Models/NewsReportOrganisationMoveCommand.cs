namespace Api.Models;

/// <summary>
/// POST newsorganisation/{sourceId}/reports/move (#1397): move news reports from one news organisation to another,
/// keeping their ids.
/// </summary>
/// <param name="SourceNewsOrganisationId">The organisation the reports are in now.</param>
/// <param name="TargetNewsOrganisationId">The organisation they move to.</param>
/// <param name="ReportIds">
/// The report ids to move (1-500). Null moves the source's next
/// <see cref="NewsReportOrganisationMoveLimits.MaxReportsPerRequest"/> reports, so re-sending empties the source. An
/// empty list is refused (400), so an empty client selection never moves a batch.
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

    /// <summary>
    /// A requested id is a report of a third organisation, a TV episode or a film, or its YouTube video is already
    /// another report of the target; or either organisation is being indexed. Nothing written.
    /// </summary>
    Conflict,

    /// <summary>Invalid request: no target, target is the source, a removed target, or a bad id list.</summary>
    BadRequest,

    /// <summary>
    /// A write or the search swap failed. Reports not yet moved are kept in the source; any that did move are listed;
    /// re-send to resume.
    /// </summary>
    Failed
}

/// <summary>
/// Command outcome of a report move between news organisations. Only <see cref="NewsReportOrganisationMoveStatus.Accepted"/>
/// and <see cref="NewsReportOrganisationMoveStatus.Failed"/> can delete source rows. A Failed attempt lists in
/// <c>MovedReportIds</c> any reports that did move (some source deletes failed); the rest are still in the source,
/// possibly with copies in the target, which a re-send reuses.
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

    /// <summary>
    /// The YouTube channel ids this request added to the target's additional channels (channels it already held are
    /// left out).
    /// </summary>
    public IReadOnlyList<string>? MovedYouTubeChannelIds { get; init; }

    /// <summary>
    /// True when the channel move was asked for and failed or was refused (the report move itself succeeded);
    /// <c>Error</c> says whether to re-send or set the channels with PATCH.
    /// </summary>
    public bool YouTubeChannelMoveFailed { get; init; }

    /// <summary>
    /// True only for a <see cref="NewsReportOrganisationMoveStatus.Conflict"/> that is transient (an organisation is being
    /// indexed): re-send the same request shortly. False for id conflicts, which a re-send cannot fix.
    /// </summary>
    public bool Retryable { get; init; }
}
