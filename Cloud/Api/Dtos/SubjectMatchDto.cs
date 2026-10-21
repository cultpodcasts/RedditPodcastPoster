using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Why a subject matched: the matched term and the field it matched in.
/// JSON shape: <c>{"subject":"…","term":"…","source":"Title"|"Description"|"PodcastDefault"}</c>.
/// </summary>
public class SubjectMatchDto
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("term")]
    public string Term { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}
