using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitUrlLookupResponse
{
    [JsonPropertyName("known")]
    public required bool Known { get; init; }

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    [JsonPropertyName("podcastId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? PodcastId { get; init; }

    [JsonPropertyName("podcastName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PodcastName { get; init; }

    [JsonPropertyName("ambiguous")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Ambiguous { get; init; }

    [JsonPropertyName("podcastIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Guid>? PodcastIds { get; init; }

    /// <summary>Streaming ServiceKeys value when kind is streaming; omitted otherwise.</summary>
    [JsonPropertyName("service")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Service { get; init; }

    [JsonPropertyName("contentKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentKind { get; init; }

    [JsonPropertyName("parentName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentName { get; init; }

    /// <summary>True when submit would reject this URL; omitted otherwise.</summary>
    [JsonPropertyName("rejected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Rejected { get; init; }

    /// <summary>Why the URL is rejected, for the submitter; omitted when not rejected.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
