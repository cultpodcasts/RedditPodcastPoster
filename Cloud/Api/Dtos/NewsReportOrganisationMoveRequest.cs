using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Body of POST newsorganisation/{sourceId}/reports/move (#1397).</summary>
public class NewsReportOrganisationMoveRequest
{
    /// <summary>The news organisation the reports move to. Required, and not the source.</summary>
    [JsonPropertyName("targetNewsOrganisationId")]
    public Guid? TargetNewsOrganisationId { get; set; }

    /// <summary>Report ids to move (1-500). Omitted or null: the source's next 500 reports. An empty array is a 400.</summary>
    [JsonPropertyName("reportIds")]
    public Guid[]? ReportIds { get; set; }

    /// <summary>Also move the source's YouTube channels into the target's <c>additionalYouTubeChannels</c>.</summary>
    [JsonPropertyName("moveYouTubeChannels")]
    public bool? MoveYouTubeChannels { get; set; }
}
