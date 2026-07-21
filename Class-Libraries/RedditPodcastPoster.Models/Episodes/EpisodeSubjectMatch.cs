using System.Text.Json.Serialization;

using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Models;

public class EpisodeSubjectMatch
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("term")]
    public string Term { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubjectMatchSource Source { get; set; }
}
