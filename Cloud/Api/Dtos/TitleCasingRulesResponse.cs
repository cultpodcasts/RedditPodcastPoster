using System.Text.Json.Serialization;

namespace Api.Dtos;

public class LanguageTitleCasingRulesResponse
{
    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("lowerCaseTerms")]
    public required IReadOnlyList<string> LowerCaseTerms { get; init; }

    [JsonPropertyName("knownTerms")]
    public required IReadOnlyList<KnownTermDto> KnownTerms { get; init; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; init; }

    public class KnownTermDto
    {
        [JsonPropertyName("literal")]
        public required string Literal { get; init; }

        [JsonPropertyName("pattern")]
        public required string Pattern { get; init; }

        [JsonPropertyName("options")]
        public string? Options { get; init; }
    }
}
