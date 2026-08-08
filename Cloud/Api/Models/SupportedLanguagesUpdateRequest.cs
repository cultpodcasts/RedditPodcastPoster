using System.Text.Json.Serialization;

namespace Api.Models;

public class SupportedLanguagesUpdateRequest
{
    [JsonPropertyName("languages")]
    public required List<SupportedLanguageUpdate> Languages { get; init; }
}

public class SupportedLanguageUpdate
{
    /// <summary>
    /// Optional. When present must match the code derived from <see cref="Name"/>;
    /// omitted/empty on add — the server always derives the code from the culture English name.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
