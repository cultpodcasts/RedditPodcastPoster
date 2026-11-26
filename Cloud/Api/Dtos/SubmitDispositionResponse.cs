using System.Text.Json.Serialization;

namespace Api.Dtos;

public sealed class SubmitDispositionResponse(
    string? contentKind,
    bool rejected = false,
    bool requiresCurator = false,
    string? message = null)
{
    [JsonPropertyName("contentKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentKind { get; } = contentKind;

    [JsonPropertyName("rejected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Rejected { get; } = rejected;

    [JsonPropertyName("requiresCurator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresCurator { get; } = requiresCurator;

    /// <summary>Why the submit was rejected, for the submitter; omitted when none is given.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; } = message;
}

public sealed class AmbiguousParentConflict(
    string contentKind,
    string? parentName,
    IEnumerable<Guid> parentIds)
{
    [JsonPropertyName("contentKind")]
    public string ContentKind { get; } = contentKind;

    [JsonPropertyName("parentName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentName { get; } = parentName;

    [JsonPropertyName("parentIds")]
    public IEnumerable<Guid> ParentIds { get; } = parentIds;
}
