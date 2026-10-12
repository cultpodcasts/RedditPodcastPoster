using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Command outcome for a supervised curator's discovery submit: held for review, nothing applied.</summary>
public class DiscoverySubmissionHeldResponse
{
    [JsonPropertyName("submissionId")]
    [JsonPropertyOrder(10)]
    public required Guid SubmissionId { get; set; }

    [JsonPropertyName("status")]
    [JsonPropertyOrder(20)]
    public required string Status { get; set; }
}
