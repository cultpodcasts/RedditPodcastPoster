using System.Text.Json.Serialization;

namespace Api.Models;

public class TitleCasingRulesAddLowerCaseTermRequest
{
    [JsonPropertyName("term")]
    public required string Term { get; init; }
}
