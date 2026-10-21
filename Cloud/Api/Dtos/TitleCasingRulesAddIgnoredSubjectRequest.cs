using System.Text.Json.Serialization;

namespace Api.Dtos;

public class TitleCasingRulesAddIgnoredSubjectRequest
{
    [JsonPropertyName("term")]
    public required string Term { get; init; }
}
