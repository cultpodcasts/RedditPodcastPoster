using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SupportedLanguageAddRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
