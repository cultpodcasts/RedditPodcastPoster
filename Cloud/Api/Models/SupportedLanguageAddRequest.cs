using System.Text.Json.Serialization;

namespace Api.Models;

public class SupportedLanguageAddRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
