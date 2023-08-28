using System.Text.Json.Serialization;

namespace ApplePodcastEpisodeEnricher.Models;

public class PodcastResponse
{
    [JsonPropertyName("next")]
    public string Next { get; set; } = "";

    [JsonPropertyName("data")]
    public List<Record> Records { get; set; } = new();
}