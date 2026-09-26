using System.Text.Json.Serialization;

namespace Api.Dtos;

public sealed class SubmitDispositionResponse(string? contentKind, bool rejected = false, bool requiresCurator = false)
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
