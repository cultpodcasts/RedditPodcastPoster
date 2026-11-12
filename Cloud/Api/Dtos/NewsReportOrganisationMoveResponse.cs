using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome of POST newsorganisation/{sourceId}/reports/move. The client GETs the organisations for the read
/// model.
/// </summary>
public sealed class NewsReportOrganisationMoveResponse
{
    [JsonPropertyName("sourceNewsOrganisationId")]
    public Guid? SourceNewsOrganisationId { get; init; }

    [JsonPropertyName("targetNewsOrganisationId")]
    public Guid? TargetNewsOrganisationId { get; init; }

    [JsonPropertyName("targetNewsOrganisationName")]
    public string? TargetNewsOrganisationName { get; init; }

    /// <summary>Report ids moved by this request (same ids, now in the target).</summary>
    [JsonPropertyName("movedReportIds")]
    public Guid[] MovedReportIds { get; init; } = [];

    /// <summary>Requested ids an earlier request already moved to the target (a re-send).</summary>
    [JsonPropertyName("alreadyMovedReportIds")]
    public Guid[] AlreadyMovedReportIds { get; init; } = [];

    /// <summary>Requested ids that are not reports of the source. Any of these refuses the whole request.</summary>
    [JsonPropertyName("notFoundReportIds")]
    public Guid[] NotFoundReportIds { get; init; } = [];

    /// <summary>Requested ids that are a report of another organisation, a TV episode or a film. Refuses the request.</summary>
    [JsonPropertyName("conflictReportIds")]
    public Guid[] ConflictReportIds { get; init; } = [];

    /// <summary>Reports still in the source (removed ones included); omitted when not counted.</summary>
    [JsonPropertyName("remainingReportCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingReportCount { get; init; }

    /// <summary>The source id when it has no reports left, so it can be retired (DELETE newsorganisation/{id}, admin).</summary>
    [JsonPropertyName("emptiedNewsOrganisationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EmptiedNewsOrganisationId { get; init; }

    /// <summary>YouTube channel ids moved to the target's additional channels by this request.</summary>
    [JsonPropertyName("movedYouTubeChannelIds")]
    public string[] MovedYouTubeChannelIds { get; init; } = [];

    /// <summary>True when the channel move was asked for and failed; the report move still succeeded.</summary>
    [JsonPropertyName("youTubeChannelMoveFailed")]
    public bool YouTubeChannelMoveFailed { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("failureIndexing")]
    public bool FailureIndexing { get; init; }
}
