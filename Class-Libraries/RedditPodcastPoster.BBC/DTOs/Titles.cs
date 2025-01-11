using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Titles
{
    [JsonPropertyName("primary")]
    public String? Primary { get; set; }

    [JsonPropertyName("secondary")]
    public String? Secondary { get; set; }

    [JsonPropertyName("tertiary")]
    public String? Tertiary { get; set; }

    [JsonPropertyName("entity_title")]
    public String? EntityTitle { get; set; }

    public string Title => EntityTitle ?? Tertiary ?? Secondary ?? Primary ?? string.Empty;
}
