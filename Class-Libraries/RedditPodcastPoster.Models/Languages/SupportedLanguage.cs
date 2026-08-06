using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Languages;

public sealed class SupportedLanguage
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
