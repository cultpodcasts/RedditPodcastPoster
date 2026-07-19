using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Low-confidence guest match returned on submit-url for toast display.
/// Name is sufficient because People are unique on normalized name; full PersonMatchDto is reserved for episode edit.
/// </summary>
public class SubmitGuestSuggestionDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("matchResults")]
    public required PersonMatchResultDto[] MatchResults { get; set; }
}
