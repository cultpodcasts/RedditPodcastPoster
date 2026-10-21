using System.Text.Json.Serialization;

namespace Api.Dtos;

public class KnownTermUpsertBody
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("options")]
    public string? Options { get; init; }
}
