using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>A service link (url, image, language). JSON-identical to the domain <c>ServiceLink</c>.</summary>
public class ServiceLinkDto
{
    [JsonPropertyName("url")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Url { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }
}
