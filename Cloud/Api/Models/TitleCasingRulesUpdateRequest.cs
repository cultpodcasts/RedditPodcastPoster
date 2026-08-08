using System.Text.Json.Serialization;

namespace Api.Models;

public class KnownTermUpdate
{
    [JsonPropertyName("literal")]
    public required string Literal { get; init; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("options")]
    public string? Options { get; init; }
}
