using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SupportedLanguagesResponse
{
    [JsonPropertyName("languages")]
    public required IReadOnlyList<SupportedLanguageDto> Languages { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    public class SupportedLanguageDto
    {
        [JsonPropertyName("code")]
        public required string Code { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }
}
