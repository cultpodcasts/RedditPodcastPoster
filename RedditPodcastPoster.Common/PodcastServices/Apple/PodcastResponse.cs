using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class PodcastResponse
{
    [JsonPropertyName("next")]
    public string Next { get; set; } = "";

    [JsonPropertyName("data")]
    public List<Record> Records { get; set; } = new();
}