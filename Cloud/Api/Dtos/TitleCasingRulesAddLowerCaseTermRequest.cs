using System.Text.Json.Serialization;

namespace Api.Dtos;

public class TitleCasingRulesAddLowerCaseTermRequest
{
    [JsonPropertyName("term")]
    public required string Term { get; init; }
}
