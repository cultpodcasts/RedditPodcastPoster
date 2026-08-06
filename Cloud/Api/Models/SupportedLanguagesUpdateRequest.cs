using System.Text.Json.Serialization;

namespace Api.Models;

public class SupportedLanguagesUpdateRequest
{
    [JsonPropertyName("languages")]
    public required List<SupportedLanguageUpdate> Languages { get; init; }
}

public class SupportedLanguageUpdate
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
