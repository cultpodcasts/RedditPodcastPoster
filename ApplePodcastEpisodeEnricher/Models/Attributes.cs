using System.Text.Json.Serialization;

namespace ApplePodcastEpisodeEnricher.Models;

public class Attributes
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("releaseDateTime")]
    public DateTime Released { get; set; }

    [JsonPropertyName("durationInMilliseconds")]
    public long LengthMs { get; set; }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(LengthMs);
}